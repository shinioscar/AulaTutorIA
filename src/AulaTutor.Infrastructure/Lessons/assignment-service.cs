using System.Security.Cryptography;
using System.Text;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using AulaTutor.Infrastructure.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Lessons;

public sealed class AssignmentService(AulaTutorDbContext db, ICurrentActor current, TimeProvider clock,
    IAiProvider provider, ITutoringService tutoring, AssignedClassAccess access, AssignedClassWorkflow workflow) : IAssignmentService
{
    private static AppException Hidden() => new(404, "La asignación o versión no está disponible.");
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LearningJson.Write(value))));
    private static void Dates(DateTimeOffset? from, DateTimeOffset? until)
    { if (from.HasValue && until.HasValue && until <= from) throw new AppException(400, "La fecha de cierre debe ser posterior a la apertura."); }
    private async Task<Actor> ActorAsync(bool teacher)
    {
        var a = await current.GetAsync();
        if (teacher ? !a.IsTeacher : !a.IsStudent) throw new AppException(403, teacher ? "Se requiere docente aprobado." : "Se requiere estudiante activo.");
        return a;
    }
    private IQueryable<LessonAssignment> TeacherQuery(Actor a) => db.LessonAssignments.Where(x => x.TeacherId == a.Id && x.OrganizationId == a.OrganizationId &&
        db.GroupMemberships.Any(m => m.GroupId == x.GroupId && m.UserId == a.Id && m.Kind == GroupMemberKind.Teacher && m.OrganizationId == a.OrganizationId));
    public async Task<PublishedClassView[]> PublishedAsync(CancellationToken ct)
    {
        var a = await ActorAsync(true);
        var versions = await (from v in db.LessonVersions.AsNoTracking() join p in db.LessonPublications on v.Id equals p.VersionId
            join l in db.Lessons on v.LessonId equals l.Id where l.TeacherId == a.Id && l.OrganizationId == a.OrganizationId && p.ArchivedAt == null
            orderby p.PublishedAt descending select new { Version = v, p.PublishedAt }).Take(2000).ToArrayAsync(ct);
        return versions.Select(x => { var c = LearningJson.Read<LessonContent>(x.Version.ContentJson); return new PublishedClassView(x.Version.LessonId, x.Version.Id, x.Version.Number, c.Title, c.Subject, x.PublishedAt, x.Version.ContentHash); }).ToArray();
    }
    public async Task<AssignmentGroupView[]> GroupsAsync(CancellationToken ct)
    {
        var a = await ActorAsync(true);
        return await db.Groups.AsNoTracking().Where(g => g.OrganizationId == a.OrganizationId && !g.IsArchived &&
            db.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == a.Id && m.OrganizationId == a.OrganizationId && m.Kind == GroupMemberKind.Teacher))
            .OrderBy(g => g.Name).Select(g => new AssignmentGroupView(g.Id, g.Name)).ToArrayAsync(ct);
    }
    public async Task<AssignmentView[]> ListAsync(CancellationToken ct)
    {
        var a = await ActorAsync(true); var rows = await TeacherQuery(a).AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(300).ToArrayAsync(ct);
        var result = new List<AssignmentView>(); foreach (var row in rows) result.Add(await ViewAsync(row, ct)); return result.ToArray();
    }
    public async Task<AssignmentView> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await current.GetAsync(); if (!a.IsTeacher) throw Hidden();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var row = await TeacherQuery(a).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Hidden();
        var view = await ViewAsync(row, ct); await tx.CommitAsync(ct); return view;
    }
    public async Task<AssignmentView> CreateAsync(CreateAssignmentRequest r, CancellationToken ct)
    {
        r = r with { AvailableFrom = r.AvailableFrom?.ToUniversalTime(), AvailableUntil = r.AvailableUntil?.ToUniversalTime() };
        var a = await ActorAsync(true); Dates(r.AvailableFrom, r.AvailableUntil);
        if (r.RequestId == Guid.Empty || r.GroupId == Guid.Empty || r.LessonVersionId == Guid.Empty) throw new AppException(400, "Selecciona versión publicada y grupo.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({a.Id.ToString()}, 0))", ct);
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == r.GroupId && g.OrganizationId == a.OrganizationId && !g.IsArchived, ct) ?? throw Hidden();
        if (!await db.GroupMemberships.AnyAsync(m => m.GroupId == group.Id && m.UserId == a.Id && m.Kind == GroupMemberKind.Teacher && m.OrganizationId == a.OrganizationId, ct)) throw Hidden();
        var v = await db.LessonVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == r.LessonVersionId, ct) ?? throw Hidden();
        // Same lock as publication/archive: the publication cannot change during creation.
        var lesson = await db.Lessons.FromSqlInterpolated($"SELECT * FROM \"Lessons\" WHERE \"Id\"={v.LessonId} AND \"TeacherId\"={a.Id} AND \"OrganizationId\"={a.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Hidden();
        var p = await db.LessonPublications.SingleOrDefaultAsync(p => p.VersionId == v.Id && p.ArchivedAt == null, ct) ?? throw new AppException(409, "Solo se asignan versiones publicadas y no archivadas.");
        var hash = Hash(r); var row = await db.LessonAssignments.SingleOrDefaultAsync(x => x.TeacherId == a.Id && x.CreationKey == r.RequestId, ct);
        if (row is not null) { if (row.CreationHash != hash) throw new AppException(409, "La clave de asignación ya se usó con otros datos."); }
        else
        {
            if (await db.LessonAssignments.AnyAsync(x => x.GroupId == group.Id && x.LessonVersionId == v.Id && x.State == AssignmentState.Active, ct)) throw new AppException(409, "Ya existe una asignación activa de esta versión al grupo.");
            if (await db.LessonAssignments.CountAsync(x => x.TeacherId == a.Id, ct) >= 300) throw new AppException(409, "Límite de 300 asignaciones del piloto.");
            row = new() { LessonId = lesson.Id, LessonVersionId = v.Id, OrganizationId = a.OrganizationId, TeacherId = a.Id, GroupId = group.Id,
                CreationKey = r.RequestId, CreationHash = hash, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(), AvailableFrom = r.AvailableFrom, AvailableUntil = r.AvailableUntil };
            db.LessonAssignments.Add(row); Record(row, r.RequestId, a.Id, "create", hash); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return await ViewAsync(row, ct);
    }
    private void Record(LessonAssignment a, Guid key, Guid actor, string action, string hash) => db.AssignmentChanges.Add(new()
    { AssignmentId = a.Id, RequestId = key, ActorId = actor, Action = action, InputHash = hash, State = a.State, AvailableFrom = a.AvailableFrom, AvailableUntil = a.AvailableUntil, ChangedAt = clock.GetUtcNow() });
    public async Task<AssignmentView> CommandAsync(Guid id, string action, AssignmentCommandRequest r, CancellationToken ct)
    {
        r = r with { AvailableFrom = r.AvailableFrom?.ToUniversalTime(), AvailableUntil = r.AvailableUntil?.ToUniversalTime() };
        var a = await ActorAsync(true); Dates(r.AvailableFrom, r.AvailableUntil);
        if (r.RequestId == Guid.Empty || r.Revision < 0 || action is not ("schedule" or "deactivate" or "activate" or "withdraw")) throw new AppException(400, "Cambio de asignación no válido.");
        if (action != "schedule" && (r.AvailableFrom.HasValue || r.AvailableUntil.HasValue)) throw new AppException(400, "Solo programar admite fechas.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.LessonAssignments.FromSqlInterpolated($"SELECT * FROM \"LessonAssignments\" WHERE \"Id\"={id} AND \"TeacherId\"={a.Id} AND \"OrganizationId\"={a.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Hidden();
        if (!await TeacherQuery(a).AnyAsync(x => x.Id == id, ct) || await db.Groups.AnyAsync(g => g.Id == row.GroupId && g.IsArchived, ct)) throw Hidden();
        var hash = Hash(new { action, r }); var previous = await db.AssignmentChanges.SingleOrDefaultAsync(x => x.AssignmentId == id && x.RequestId == r.RequestId, ct);
        if (previous is not null) { if (previous.InputHash != hash) throw new AppException(409, "La clave ya corresponde a otro cambio."); }
        else
        {
            if (row.Revision != r.Revision || row.State == AssignmentState.Withdrawn) throw new AppException(409, "La asignación cambió o está retirada definitivamente.");
            if (await db.AssignmentChanges.CountAsync(x => x.AssignmentId == id, ct) >= 100) throw new AppException(409, "Límite de cambios de asignación del piloto.");
            if (action == "schedule") { row.AvailableFrom = r.AvailableFrom; row.AvailableUntil = r.AvailableUntil; }
            else row.State = action switch { "activate" => AssignmentState.Active, "deactivate" => AssignmentState.Inactive, _ => AssignmentState.Withdrawn };
            if (row.State == AssignmentState.Active && await db.LessonAssignments.AnyAsync(x => x.Id != id && x.GroupId == row.GroupId && x.LessonVersionId == row.LessonVersionId && x.State == AssignmentState.Active, ct)) throw new AppException(409, "Ya existe otra asignación activa equivalente.");
            row.Revision++; row.UpdatedAt = clock.GetUtcNow(); Record(row, r.RequestId, a.Id, action, hash); await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return await ViewAsync(row, ct);
    }
    private async Task<AssignmentView> ViewAsync(LessonAssignment a, CancellationToken ct)
    {
        var v = await db.LessonVersions.AsNoTracking().SingleAsync(x => x.Id == a.LessonVersionId, ct); var c = LearningJson.Read<LessonContent>(v.ContentJson);
        var p = await db.LessonPublications.AsNoTracking().SingleAsync(x => x.VersionId == v.Id, ct);
        var changes = await db.AssignmentChanges.AsNoTracking().Where(x => x.AssignmentId == a.Id).OrderBy(x => x.ChangedAt).ToArrayAsync(ct);
        return new(a.Id, a.LessonId, v.Id, v.Number, c.Title, c.Subject, v.ContentHash, a.GroupId, (await db.Groups.AsNoTracking().SingleAsync(g => g.Id == a.GroupId, ct)).Name,
            a.TeacherId, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == a.TeacherId, ct)).DisplayName, a.State.ToString(), a.Revision, a.CreatedAt, a.AvailableFrom, a.AvailableUntil, access.Available(a, p),
            changes.Select(x => new AssignmentChangeView(x.Action, x.State.ToString(), x.ChangedAt, x.AvailableFrom, x.AvailableUntil)).ToArray());
    }
    public async Task<AssignedClassView[]> StudentListAsync(CancellationToken ct)
    {
        var a = await ActorAsync(false); var rows = await access.ForStudent(a.Id, a.OrganizationId).AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(300).ToArrayAsync(ct);
        var result = new List<AssignedClassView>(); foreach (var row in rows) result.Add(await workflow.ClassViewAsync(row, a.Id, ct)); return result.ToArray();
    }
    public async Task<AssignedClassView> StudentGetAsync(Guid id, CancellationToken ct)
    {
        var a = await ActorAsync(false); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var row = await access.ForStudent(a.Id, a.OrganizationId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Hidden();
        var view = await workflow.ClassViewAsync(row, a.Id, ct); await tx.CommitAsync(ct); return view;
    }
    public async Task<TutoringSessionView> StartAsync(Guid id, StartAssignedClassRequest r, CancellationToken ct)
    {
        var a = await ActorAsync(false); if (r.RequestId == Guid.Empty) throw new AppException(400, "Clave de inicio no válida."); Guid sessionId;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({a.Id.ToString()}, 0))", ct);
            var row = await db.LessonAssignments.FromSqlInterpolated($"SELECT * FROM \"LessonAssignments\" WHERE \"Id\"={id} AND \"OrganizationId\"={a.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Hidden();
            if (!await access.ForStudent(a.Id, a.OrganizationId).AnyAsync(x => x.Id == id, ct)) throw Hidden();
            var existing = await db.TutoringSessions.SingleOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == a.Id, ct);
            if (existing is not null) sessionId = existing.Id; // Uniqueness survives new keys/reload.
            else
            {
                var p = await db.LessonPublications.AsNoTracking().SingleAsync(x => x.VersionId == row.LessonVersionId, ct);
                if (!access.Available(row, p)) throw new AppException(409, "La clase no está disponible para comenzar.");
                if (await db.TutoringSessions.AnyAsync(s => s.StudentId == a.Id && s.CreationKey == r.RequestId, ct)) throw new AppException(409, "La clave de inicio ya pertenece a otra sesión.");
                if (await db.TutoringSessions.CountAsync(s => s.StudentId == a.Id, ct) >= 100) throw new AppException(409, "Límite de 100 sesiones del piloto.");
                var v = await db.LessonVersions.AsNoTracking().SingleAsync(x => x.Id == row.LessonVersionId, ct); var c = LearningJson.Read<LessonContent>(v.ContentJson);
                var health = await provider.CheckHealthAsync(ct); // Configuration only; no quota.
                var s = new TutoringSession { StudentId = a.Id, OrganizationId = a.OrganizationId, CreationKey = r.RequestId, Mode = TutoringMode.AssignedClass,
                    AssignmentId = id, LessonVersionId = v.Id, Topic = c.Title, Goal = string.Join("; ", c.Objectives.Select(o => o.Text))[..Math.Min(500, string.Join("; ", c.Objectives.Select(o => o.Text)).Length)],
                    Level = c.Level, DirectReview = true, ProviderId = health.ProviderId, ModelId = health.ModelId, CreatedAt = clock.GetUtcNow(), LastActivityAt = clock.GetUtcNow() };
                // Teacher free-form level is bounded separately from tutoring's enum.
                s.Level = c.Level.Length <= 20 ? c.Level : c.Level[..20];
                if (c.Preloaded is not null) s.PolicyVersion = "preloaded-class-v1";
                db.TutoringSessions.Add(s); workflow.Seed(s, c, r.RequestId); await db.SaveChangesAsync(ct); sessionId = s.Id;
            }
            await tx.CommitAsync(ct);
        }
        return await tutoring.GetAsync(sessionId, ct);
    }
}
