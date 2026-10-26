using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Tutoring;

public sealed partial class TutoringService
{
    public async Task<PreparedOptionsView> PreparedOptionsAsync(CancellationToken ct)
    { await StudentAsync(); return await prepared.OptionsAsync(ct); }

    public async Task<TutoringSessionView> StartPreparedPlanAsync(Guid id, StartPreparedPlanRequest request, CancellationToken ct)
    {
        var actor = await StudentAsync(true);
        if (request.RequestId == Guid.Empty || request.Modality is not ("Prepared" or "Conversational") || request.Length is not ("Short" or "Medium" or "Long"))
            throw new AppException(400, "Revisa la modalidad y la extensión del objetivo.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.Id.ToString()}, 0))", ct);
        var origin = await OwnedAsync(id, actor, true, ct);
        if (!PreparedRules.IsPrepared(origin) || origin.LearningExperience != "Diagnostic" || origin.State != TutoringState.Completed) throw Hidden();
        var plan = await db.LearningPlans.SingleOrDefaultAsync(p => p.Id == request.PlanId && p.SessionId == id && p.StudentId == actor.Id, ct) ?? throw Hidden();
        var content = LearningJson.Read<PlanContent>(plan.ContentJson);
        if (request.ObjectiveOrder < 0 || request.ObjectiveOrder >= content.Objectives.Length) throw new AppException(400, "Objetivo no disponible.");
        var existingKey = await db.TutoringSessions.SingleOrDefaultAsync(s => s.StudentId == actor.Id && s.CreationKey == request.RequestId, ct);
        var existingGoal = await db.TutoringSessions.SingleOrDefaultAsync(s => s.SourcePlanId == plan.Id && s.SourcePlanObjectiveOrder == request.ObjectiveOrder, ct);
        var existing = existingKey ?? existingGoal;
        if (existing is not null)
        {
            if (existing.SourcePlanId != plan.Id || existing.SourcePlanObjectiveOrder != request.ObjectiveOrder || existing.LearningModality != request.Modality ||
                request.Modality == "Prepared" && (await prepared.FindAsync(existing.Id, ct))?.Length != request.Length) throw Conflict("Este objetivo ya tiene una tutoría. Ábrela para continuar.");
            await tx.CommitAsync(ct); return await ViewAsync(existing, ct);
        }
        if (await db.TutoringSessions.CountAsync(s => s.StudentId == actor.Id, ct) >= 100) throw Conflict("Se alcanzó el límite de tutorías del piloto.");
        var health = await provider.CheckHealthAsync(ct);
        var objective = content.Objectives[request.ObjectiveOrder];
        var session = new TutoringSession { StudentId = actor.Id, OrganizationId = actor.OrganizationId, CreationKey = request.RequestId,
            Topic = origin.Topic, Goal = objective.Title, Level = objective.Activities[0].Difficulty, DirectReview = true,
            LearningModality = request.Modality, PolicyVersion = request.Modality == "Prepared" ? PreparedRules.Version : TutoringOrchestrator.PolicyVersion,
            ProviderId = health.ProviderId, ModelId = health.ModelId, AiBindingId = health.BindingId,
            SourcePlanId = plan.Id, SourcePlanObjectiveOrder = request.ObjectiveOrder, CreatedAt = clock.GetUtcNow(), LastActivityAt = clock.GetUtcNow() };
        if (request.Modality == "Prepared") await prepared.AddAsync(session, request.Length, health, ct);
        plan.State = LearningPlanState.Active; plan.AcceptedAt ??= clock.GetUtcNow();
        db.TutoringSessions.Add(session); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await ViewAsync(session, ct);
    }
    private async Task RecordPlanWorkAsync(TutoringSession session, CancellationToken ct)
    {
        if (session.SourcePlanId is not { } id || !await db.ExerciseAttempts.AnyAsync(a => a.SessionId == session.Id, ct)) return;
        var sourceId = await db.LearningPlans.Where(p => p.Id == id && p.StudentId == session.StudentId).Select(p => p.SessionId).SingleAsync(ct);
        // Parent serialization makes two independently completed goals update one durable plan.
        await db.TutoringSessions.FromSqlInterpolated($"SELECT * FROM \"TutoringSessions\" WHERE \"Id\"={sourceId} FOR UPDATE").SingleAsync(ct);
        var plan = await db.LearningPlans.SingleAsync(p => p.Id == id && p.StudentId == session.StudentId, ct);
        var exercises = await db.LearningExercises.Where(e => e.PlanId == id).ToArrayAsync(ct);
        foreach (var e in exercises.Where(e => e.ObjectiveOrder == session.SourcePlanObjectiveOrder)) e.Completed = true;
        plan.CurrentActivity = exercises.Count(e => e.Completed);
        if (exercises.All(e => e.Completed)) { plan.State = LearningPlanState.Completed; plan.CompletedAt = clock.GetUtcNow(); }
    }
}
