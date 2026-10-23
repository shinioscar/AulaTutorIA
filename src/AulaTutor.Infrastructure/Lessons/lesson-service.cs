using System.Security.Cryptography;
using System.Text;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Lessons;

public sealed partial class LessonService(AulaTutorDbContext db, ICurrentActor current, IAiProvider provider,
    LessonOrchestrator orchestrator, TimeProvider clock, IOptions<AiProviderOptions> options) : ILessonService
{
    private static AppException Hidden() => new(404, "La clase no está disponible.");
    private static AppException Conflict(string text = "La revisión cambió. Actualiza antes de continuar.") => new(409, text);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Key(Guid id, long revision) { if (id == Guid.Empty || revision < 0) throw new AppException(400, "Solicitud no válida."); }
    private async Task<Actor> TeacherAsync(bool hide = false)
    {
        var actor = await current.GetAsync();
        if (!actor.IsTeacher || !await db.Users.AnyAsync(u => u.Id == actor.Id && u.OrganizationId == actor.OrganizationId &&
            u.IsActive && u.TeacherApproval == TeacherApproval.Approved && db.UserRoles.Any(ur => ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Teacher))))
            throw hide ? Hidden() : new AppException(403, "Se requiere un docente aprobado y activo.");
        return actor;
    }
    private async Task<Lesson> OwnedAsync(Guid id, Actor actor, bool locked, CancellationToken ct) =>
        await (locked ? db.Lessons.FromSqlInterpolated($"SELECT * FROM \"Lessons\" WHERE \"Id\"={id} AND \"TeacherId\"={actor.Id} AND \"OrganizationId\"={actor.OrganizationId} FOR UPDATE")
            : db.Lessons.AsNoTracking().Where(l => l.Id == id && l.TeacherId == actor.Id && l.OrganizationId == actor.OrganizationId)).SingleOrDefaultAsync(ct) ?? throw Hidden();
    private void Touch(Lesson lesson) { lesson.Revision++; lesson.UpdatedAt = clock.GetUtcNow(); }
    private static void Editable(Lesson l) { if (l.State is not (LessonState.Draft or LessonState.Simulation or LessonState.InReview)) throw Conflict("Crea un nuevo borrador para cambiar una clase aprobada o publicada."); }
    private async Task SaveContentAsync(Lesson lesson, LessonContent content, CancellationToken ct)
    {
        var draft = await db.LessonDrafts.SingleAsync(d => d.LessonId == lesson.Id, ct);
        draft.Revision++; draft.ContentJson = LearningJson.Write(content);
        db.LessonDraftRevisions.Add(new() { LessonId = lesson.Id, Revision = draft.Revision, ContentJson = draft.ContentJson, SavedAt = clock.GetUtcNow() });
        lesson.State = LessonState.Draft; Touch(lesson);
    }
    public Task<LessonView> CreateAsync(CreateLessonRequest request, CancellationToken ct) => CreateCoreAsync(request, false, ct);
    private async Task<LessonView> CreateCoreAsync(CreateLessonRequest request, bool imported, CancellationToken ct)
    {
        var actor = await TeacherAsync(); Key(request.RequestId, 0); LessonValidationRules.ValidateDraft(request.Content);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.Id.ToString()}, 0))", ct);
        actor = await TeacherAsync();
        var json = LearningJson.Write(request.Content);
        var lesson = await db.Lessons.SingleOrDefaultAsync(l => l.TeacherId == actor.Id && l.CreationKey == request.RequestId, ct);
        if (lesson is not null)
        {
            var original = await db.LessonDraftRevisions.SingleAsync(d => d.LessonId == lesson.Id && d.Revision == 0, ct);
            if (LearningJson.Write(LearningJson.Read<LessonContent>(original.ContentJson)) != json) throw Conflict("La clave de creación ya tiene otros datos.");
        }
        else
        {
            if (await db.Lessons.CountAsync(l => l.TeacherId == actor.Id, ct) >= 100) throw Conflict("Límite de 100 clases del piloto.");
            lesson = new() { TeacherId = actor.Id, OrganizationId = actor.OrganizationId, CreationKey = request.RequestId, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
            db.Lessons.Add(lesson); db.LessonDrafts.Add(new() { LessonId = lesson.Id, ContentJson = json });
            db.LessonDraftRevisions.Add(new() { LessonId = lesson.Id, ContentJson = json, SavedAt = clock.GetUtcNow() });
            if (imported) db.AuditEvents.Add(new() { ActorId = actor.Id, CreatedAt = clock.GetUtcNow(), Type = "LessonImported",
                ResourceId = lesson.Id.ToString(), CorrelationId = Guid.NewGuid().ToString("N"), Change = "Added" });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return await GetAsync(lesson.Id, ct);
    }
    public async Task<LessonItem[]> ListAsync(int page, CancellationToken ct)
    {
        var actor = await TeacherAsync(); if (page is < 1 or > 10000) throw new AppException(400, "Página no válida.");
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.TeacherId == actor.Id && l.OrganizationId == actor.OrganizationId).OrderByDescending(l => l.UpdatedAt).ThenBy(l => l.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
        var ids = lessons.Select(l => l.Id).ToArray(); var drafts = await db.LessonDrafts.AsNoTracking().Where(d => ids.Contains(d.LessonId)).ToDictionaryAsync(d => d.LessonId, ct);
        return lessons.Select(l => new LessonItem(l.Id, LearningJson.Read<LessonContent>(drafts[l.Id].ContentJson).Title, l.State.ToString(), l.Revision, l.UpdatedAt)).ToArray();
    }
    public async Task<LessonView> GetAsync(Guid id, CancellationToken ct)
    {
        var actor = await TeacherAsync(true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var l = await OwnedAsync(id, actor, false, ct); var draft = await db.LessonDrafts.AsNoTracking().SingleAsync(d => d.LessonId == id, ct);
        var content = LearningJson.Read<LessonContent>(draft.ContentJson);
        var proposals = await db.LessonProposals.AsNoTracking().Where(p => p.LessonId == id).OrderBy(p => p.CreatedAt).ToArrayAsync(ct);
        var versions = await db.LessonVersions.AsNoTracking().Where(v => v.LessonId == id).OrderBy(v => v.Number).ToArrayAsync(ct);
        var publications = await db.LessonPublications.AsNoTracking().Where(p => p.LessonId == id).ToDictionaryAsync(p => p.VersionId, ct);
        var operations = await db.LessonOperations.AsNoTracking().Where(o => o.LessonId == id && o.SimulationId == null).OrderBy(o => o.CreatedAt).ToArrayAsync(ct);
        var sims = await db.LessonSimulations.AsNoTracking().Where(s => s.LessonId == id).OrderByDescending(s => s.CreatedAt).ToArrayAsync(ct);
        var view = new LessonView(id, l.State.ToString(), l.Revision, draft.Revision, content, LessonValidationRules.Review(content),
            proposals.Select(p => new LessonProposalView(p.Id, p.DraftRevision, p.State.ToString(), p.Explanation, LearningJson.Read<LessonContent>(p.ContentJson), p.DraftRevision != draft.Revision)).ToArray(),
            versions.Select(v => new LessonVersionView(v.Id, v.Number, v.DraftRevision, LearningJson.Read<LessonContent>(v.ContentJson), v.ContentHash, v.ApprovedBy, v.ApprovedAt, publications.GetValueOrDefault(v.Id)?.PublishedAt, publications.GetValueOrDefault(v.Id)?.ArchivedAt)).ToArray(),
            operations.Select(OperationView).ToArray(), sims.Select(s => new LessonSimulationItem(s.Id, s.DraftRevision, s.State, s.Revision)).ToArray());
        await tx.CommitAsync(ct); return view;
    }
    private LessonOperationView OperationView(LessonOperation o) => new(o.RequestId, o.SimulationId, o.Action, o.Text, o.DraftRevision,
        o.State.ToString(), o.State == LessonOperationState.Pending && o.LeaseExpiresAt > clock.GetUtcNow(),
        o.State == LessonOperationState.Pending && o.LeaseExpiresAt <= clock.GetUtcNow() ? "Interrupted" : o.ErrorCode,
        o.Attempts, o.RetryNotBefore, o.Reply, o.Outcome, o.ValidationType, o.ProviderId, o.ModelId);
    public async Task<LessonView> SaveAsync(Guid id, SaveLessonRequest request, CancellationToken ct)
    {
        var actor = await TeacherAsync(true); LessonValidationRules.ValidateDraft(request.Content);
        await using var tx = await db.Database.BeginTransactionAsync(ct); var l = await OwnedAsync(id, actor, true, ct);
        Editable(l); if (request.Revision != l.Revision) throw Conflict();
        await SaveContentAsync(l, request.Content, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await GetAsync(id, ct);
    }
    private async Task<bool> ReplayAsync(Guid id, string action, LessonCommandRequest request, CancellationToken ct)
    {
        Key(request.RequestId, request.Revision);
        var hash = Hash(action + "\n" + request.Revision + "\n" + request.TargetId);
        var command = await db.LessonCommands.SingleOrDefaultAsync(c => c.LessonId == id && c.RequestId == request.RequestId, ct);
        if (command is not null) { if (command.InputHash != hash) throw Conflict("Clave ya usada para otra operación."); return true; }
        if (await db.LessonCommands.CountAsync(c => c.LessonId == id, ct) >= 300) throw Conflict("Límite de operaciones explícitas del piloto.");
        db.LessonCommands.Add(new() { LessonId = id, RequestId = request.RequestId, InputHash = hash }); return false;
    }
    public async Task<LessonView> CommandAsync(Guid id, string action, LessonCommandRequest request, CancellationToken ct)
    {
        var actor = await TeacherAsync(true);
        await using var tx = await db.Database.BeginTransactionAsync(ct); var l = await OwnedAsync(id, actor, true, ct);
        if (!await ReplayAsync(id, action, request, ct))
        {
            if (l.Revision != request.Revision) throw Conflict();
            var draft = await db.LessonDrafts.SingleAsync(d => d.LessonId == id, ct);
            switch (action)
            {
                case "apply-proposal": case "discard-proposal":
                    var proposal = await db.LessonProposals.SingleOrDefaultAsync(p => p.Id == request.TargetId && p.LessonId == id, ct) ?? throw Hidden();
                    if (proposal.State != ProposalState.Pending) throw Conflict("La propuesta ya fue resuelta.");
                    if (action == "apply-proposal") { Editable(l); if (proposal.DraftRevision != draft.Revision) throw Conflict("Propuesta desactualizada: genera otra basada en el borrador vigente."); await SaveContentAsync(l, LearningJson.Read<LessonContent>(proposal.ContentJson), ct); proposal.State = ProposalState.Applied; }
                    else { proposal.State = ProposalState.Discarded; Touch(l); }
                    break;
                case "review":
                    Editable(l); l.State = LessonState.InReview; Touch(l); break;
                case "approve":
                    if (l.State != LessonState.InReview) throw Conflict("Revisa la clase antes de aprobar.");
                    if (LessonValidationRules.Review(LearningJson.Read<LessonContent>(draft.ContentJson)).Errors.Length > 0) throw Conflict("La revisión contiene requisitos pendientes.");
                    var number = await db.LessonVersions.CountAsync(v => v.LessonId == id, ct) + 1;
                    if (number > 20) throw Conflict("Límite de veinte versiones del piloto.");
                    var canonical = LearningJson.Write(LearningJson.Read<LessonContent>(draft.ContentJson));
                    db.LessonVersions.Add(new() { LessonId = id, Number = number, DraftRevision = draft.Revision, ContentJson = canonical, ContentHash = Hash(canonical), ApprovedBy = actor.Id, ApprovedAt = clock.GetUtcNow() });
                    l.State = LessonState.Approved; Touch(l); break;
                case "publish":
                    var version = await db.LessonVersions.SingleOrDefaultAsync(v => v.Id == request.TargetId && v.LessonId == id, ct) ?? throw Hidden();
                    if (l.State != LessonState.Approved || version.DraftRevision != draft.Revision) throw Conflict("Publica solo la versión aprobada vigente.");
                    if (await db.LessonPublications.AnyAsync(p => p.VersionId == version.Id, ct)) throw Conflict("La versión ya tiene publicación.");
                    db.LessonPublications.Add(new() { LessonId = id, VersionId = version.Id, PublishedAt = clock.GetUtcNow() }); l.State = LessonState.Published; Touch(l); break;
                case "new-draft":
                    if (l.State is not (LessonState.Approved or LessonState.Published or LessonState.Archived)) throw Conflict("Ya existe un borrador editable.");
                    await SaveContentAsync(l, LearningJson.Read<LessonContent>(draft.ContentJson), ct); break;
                case "archive":
                    if (l.State == LessonState.Archived) throw Conflict("La clase ya está archivada.");
                    foreach (var publication in await db.LessonPublications.Where(p => p.LessonId == id && p.ArchivedAt == null).ToArrayAsync(ct)) publication.ArchivedAt = clock.GetUtcNow();
                    l.State = LessonState.Archived; Touch(l); break;
                default: throw new AppException(400, "Acción de clase no válida.");
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return await GetAsync(id, ct);
    }
}
