using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Tutoring;

public sealed class LearningService(AulaTutorDbContext db, ICurrentActor current,
    ITutoringService tutoring, TimeProvider clock) : ILearningService
{
    private static AppException Hidden() => new(404, "Reporte o tutoría no disponible.");
    private async Task<TutoringSession> OwnedAsync(Guid id, bool locked, CancellationToken ct)
    {
        var actor = await current.GetAsync(); if (!actor.IsStudent) throw Hidden();
        return await (locked
            ? db.TutoringSessions.FromSqlInterpolated($"SELECT * FROM \"TutoringSessions\" WHERE \"Id\"={id} AND \"StudentId\"={actor.Id} AND \"OrganizationId\"={actor.OrganizationId} FOR UPDATE")
            : db.TutoringSessions.AsNoTracking().Where(s => s.Id == id && s.StudentId == actor.Id && s.OrganizationId == actor.OrganizationId))
            .SingleOrDefaultAsync(ct) ?? throw Hidden();
    }
    private IQueryable<Infrastructure.Identity.AppUser> Recipients(TutoringSession s) => db.Users.Where(u =>
        u.OrganizationId == s.OrganizationId && u.IsActive && u.TeacherApproval == TeacherApproval.Approved &&
        db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Teacher)) &&
        db.GroupMemberships.Any(tm => tm.UserId == u.Id && tm.Kind == GroupMemberKind.Teacher && tm.OrganizationId == s.OrganizationId &&
            db.Groups.Any(g => g.Id == tm.GroupId && !g.IsArchived) &&
            db.GroupMemberships.Any(sm => sm.UserId == s.StudentId && sm.Kind == GroupMemberKind.Student && sm.GroupId == tm.GroupId && sm.OrganizationId == s.OrganizationId)));
    public async Task<ShareRecipient[]> RecipientsAsync(Guid sessionId, CancellationToken ct)
    { var s = await OwnedAsync(sessionId, false, ct); return await Recipients(s).OrderBy(u => u.DisplayName).Select(u => new ShareRecipient(u.Id, u.DisplayName)).ToArrayAsync(ct); }
    public async Task<ShareView> ShareAsync(Guid sessionId, ShareReportRequest request, CancellationToken ct)
    {
        if (!request.Consent || request.RequestId == Guid.Empty || request.ReportId == Guid.Empty || request.RecipientId == Guid.Empty)
            throw new AppException(400, "Selecciona reporte, destinatario y autorización explícita.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); var s = await OwnedAsync(sessionId, true, ct);
        var recipient = await Recipients(s).SingleOrDefaultAsync(u => u.Id == request.RecipientId, ct) ?? throw Hidden();
        var report = await db.LearningReports.SingleOrDefaultAsync(r => r.Id == request.ReportId && r.SessionId == sessionId, ct) ?? throw Hidden();
        var share = await db.ReportShares.SingleOrDefaultAsync(sh => sh.SessionId == sessionId && sh.RequestId == request.RequestId, ct);
        if (share is not null)
        {
            if (share.ReportId != request.ReportId || share.RecipientId != request.RecipientId) throw new AppException(409, "Clave de autorización usada con otros datos.");
        }
        else
        {
            if (report.EvidenceRevision != s.EvidenceRevision) throw new AppException(409, "Genera un reporte vigente antes de compartir.");
            if (await db.ReportShares.CountAsync(sh => sh.SessionId == sessionId, ct) >= 30) throw new AppException(409, "Límite de autorizaciones de compartición alcanzado.");
            share = new ReportShare { SessionId = sessionId, ReportId = request.ReportId, RecipientId = recipient.Id, RequestId = request.RequestId, GrantedAt = clock.GetUtcNow() };
            db.ReportShares.Add(share); s.Revision++; await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return new(share.Id, share.ReportId, share.RecipientId, recipient.DisplayName, share.GrantedAt, share.RevokedAt);
    }
    public async Task RevokeAsync(Guid sessionId, Guid shareId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var s = await OwnedAsync(sessionId, true, ct);
        var share = await db.ReportShares.SingleOrDefaultAsync(sh => sh.Id == shareId && sh.SessionId == sessionId, ct) ?? throw Hidden();
        if (share.RevokedAt is null) { share.RevokedAt = clock.GetUtcNow(); s.Revision++; await db.SaveChangesAsync(ct); }
        await tx.CommitAsync(ct);
    }
    private async Task<(LearningReport Report, TutoringSession Session)> SharedAsync(Guid reportId, CancellationToken ct)
    {
        var actor = await current.GetAsync(); if (!actor.IsTeacher) throw Hidden();
        var report = await db.LearningReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, ct) ?? throw Hidden();
        var s = await db.TutoringSessions.AsNoTracking().SingleAsync(s => s.Id == report.SessionId, ct);
        if (s.OrganizationId != actor.OrganizationId || !await db.ReportShares.AnyAsync(sh => sh.ReportId == reportId && sh.RecipientId == actor.Id && sh.RevokedAt == null, ct)
            || !await Recipients(s).AnyAsync(u => u.Id == actor.Id, ct)) throw Hidden();
        return (report, s);
    }
    public async Task<ReportView> SharedReportAsync(Guid reportId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var (report, s) = await SharedAsync(reportId, ct); var view = LearningWorkflow.PublicReport(report, s);
        await tx.CommitAsync(ct); return view;
    }
    public async Task<ReportView[]> SharedReportsAsync(CancellationToken ct)
    {
        var actor = await current.GetAsync(); if (!actor.IsTeacher) throw new AppException(403, "Se requiere docente aprobado.");
        var ids = await db.ReportShares.AsNoTracking().Where(sh => sh.RecipientId == actor.Id && sh.RevokedAt == null).Select(sh => sh.ReportId).Distinct().Take(100).ToArrayAsync(ct);
        var reports = new List<ReportView>();
        foreach (var id in ids) { try { reports.Add(await SharedReportAsync(id, ct)); } catch (AppException e) when (e.Status == 404) { } }
        return reports.ToArray();
    }
    public async Task<TutoringSessionView> ReinforceAsync(Guid sessionId, Guid recommendationId, ReinforcementRequest request, CancellationToken ct)
    {
        var s = await OwnedAsync(sessionId, false, ct);
        if (s.State != TutoringState.Completed) throw new AppException(409, "Finaliza la sesión antes de comenzar un refuerzo.");
        var recommendation = await db.LearningRecommendations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == recommendationId && r.SessionId == sessionId, ct) ?? throw Hidden();
        return await tutoring.CreateAsync(new(request.RequestId, s.Topic, recommendation.Goal, s.Level, true, recommendation.Id), ct);
    }
}
