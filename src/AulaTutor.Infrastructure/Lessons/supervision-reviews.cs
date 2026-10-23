using System.Security.Cryptography;
using System.Text;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Lessons;

public sealed partial class SupervisionService
{
    public async Task<TeacherReviewView> ReviewAsync(Guid id, Guid studentId, ReviewRequest r, CancellationToken ct)
    {
        var actor = await TeacherAsync();
        if (r.RequestId == Guid.Empty || r.Revision < 0 || r.EvidenceRevision < 0 || r.ExerciseId == Guid.Empty ||
            r.Comment is null || string.IsNullOrWhiteSpace(r.Comment) || r.Comment.Length > 1000 || r.Observation is null || r.Observation.Length > 1000 ||
            r.Recommendation is null || r.Recommendation.Length > 1000 || r.EvidenceIds is null || r.EvidenceIds.Length > 10 ||
            r.EvidenceIds.Distinct().Count() != r.EvidenceIds.Length || r.EvidenceIds.Contains(Guid.Empty) ||
            !Enum.TryParse<TeacherReviewState>(r.State, out var state) || !Enum.IsDefined(state) || state.ToString() != r.State ||
            !Enum.TryParse<AttentionState>(r.Attention, out var attention) || !Enum.IsDefined(attention) || attention.ToString() != r.Attention ||
            !Enum.TryParse<HumanReviewOutcome>(r.Outcome, out var outcome) || !Enum.IsDefined(outcome) || outcome.ToString() != r.Outcome)
            throw new AppException(400, "Revisa los campos y límites de la decisión docente.");
        if (outcome != HumanReviewOutcome.NotAssessed && (r.EvidenceIds.Length == 0 || string.IsNullOrWhiteSpace(r.Observation)))
            throw new AppException(400, "Un resultado humano requiere evidencias y una observación que lo fundamente.");
        if (attention == AttentionState.Closed && state != TeacherReviewState.Reviewed)
            throw new AppException(400, "Para cerrar el seguimiento, registra el estado Revisada y referencia la evidencia examinada.");
        if (!r.ExerciseId.HasValue && (r.Flagged || state == TeacherReviewState.FollowUpNeeded))
            throw new AppException(400, "Marca una actividad concreta para seguimiento; el comentario general no crea casos.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LearningJson.Write(r))));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize against withdrawal before taking the same session lock used by attempts.
        var assignment = await db.LessonAssignments.FromSqlInterpolated($"SELECT * FROM \"LessonAssignments\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Hidden();
        if (!await Assignments(actor).AnyAsync(a => a.Id == id, ct) || !await Cohort(assignment).AnyAsync(u => u.Id == studentId, ct)) throw Hidden();
        var session = await db.TutoringSessions.FromSqlInterpolated($"SELECT * FROM \"TutoringSessions\" WHERE \"AssignmentId\"={id} AND \"StudentId\"={studentId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw Hidden();
        if (session.Mode != TutoringMode.AssignedClass || session.LessonVersionId != assignment.LessonVersionId) throw Hidden();
        // A membership or role can be revoked while waiting for the session lock.
        if (!await Assignments(actor).AnyAsync(a => a.Id == id, ct) || !await Cohort(assignment).AnyAsync(u => u.Id == studentId, ct)) throw Hidden();
        var previous = await db.TeacherReviewEntries.AsNoTracking().SingleOrDefaultAsync(e => e.SessionId == session.Id && e.RequestId == r.RequestId, ct);
        if (previous is not null)
        {
            if (previous.InputHash != hash) throw new AppException(409, "La clave ya identifica otra decisión docente.");
            var replay = (await ReviewViewsAsync(session.Id, ct)).Single(v => v.Id == previous.ReviewId);
            await tx.CommitAsync(ct); return replay;
        }
        var targetId = r.ExerciseId ?? session.Id;
        if (r.ExerciseId.HasValue && !await db.LearningExercises.AnyAsync(e => e.Id == r.ExerciseId && e.SessionId == session.Id && e.IsPresented, ct)) throw Hidden();
        var review = await db.TeacherReviews.SingleOrDefaultAsync(v => v.SessionId == session.Id && v.TargetId == targetId, ct);
        if ((review?.Revision ?? 0) != r.Revision || session.EvidenceRevision != r.EvidenceRevision)
            throw new AppException(409, "La revisión o las evidencias cambiaron. Actualiza y vuelve a examinarlas antes de guardar.");
        if (review?.Revision >= 100) throw new AppException(409, "Límite de cien decisiones por actividad o clase del piloto.");
        var attempts = await db.ExerciseAttempts.AsNoTracking().Where(a => a.SessionId == session.Id && (!r.ExerciseId.HasValue || a.ExerciseId == r.ExerciseId)).ToArrayAsync(ct);
        if (r.EvidenceIds.Any(e => !attempts.Any(a => a.Id == e))) throw new AppException(400, "Las evidencias deben pertenecer a esta actividad y clase.");
        var last = attempts.OrderBy(a => a.Number).LastOrDefault();
        if (r.ExerciseId.HasValue && last is not null && (state == TeacherReviewState.Reviewed || attention == AttentionState.Closed) && !r.EvidenceIds.Contains(last.Id))
            throw new AppException(400, "Examina y referencia el último intento antes de resolver esta actividad.");
        if (review is null)
        { review = new() { SessionId = session.Id, ExerciseId = r.ExerciseId, TargetId = targetId }; db.TeacherReviews.Add(review); }
        review.Revision++;
        var entry = new TeacherReviewEntry { ReviewId = review.Id, SessionId = session.Id, RequestId = r.RequestId, InputHash = hash, Revision = review.Revision,
            EvidenceRevision = session.EvidenceRevision, AuthorId = actor.Id, CreatedAt = clock.GetUtcNow(), State = state, Attention = attention, Outcome = outcome,
            Flagged = r.Flagged, Comment = r.Comment.Trim(), Observation = r.Observation.Trim(), Recommendation = r.Recommendation.Trim() };
        db.TeacherReviewEntries.Add(entry);
        foreach (var evidenceId in r.EvidenceIds) db.ReviewEvidence.Add(new() { EntryId = entry.Id, SessionId = session.Id, AttemptId = evidenceId });
        await db.SaveChangesAsync(ct);
        var result = (await ReviewViewsAsync(session.Id, ct)).Single(v => v.Id == review.Id);
        await tx.CommitAsync(ct); return result;
    }
    private async Task<TeacherReviewView[]> ReviewViewsAsync(Guid? sessionId, CancellationToken ct)
    {
        if (!sessionId.HasValue) return [];
        var reviews = await db.TeacherReviews.AsNoTracking().Where(r => r.SessionId == sessionId).OrderBy(r => r.Id).ToArrayAsync(ct);
        var entries = await db.TeacherReviewEntries.AsNoTracking().Where(e => e.SessionId == sessionId).OrderByDescending(e => e.Revision).ToArrayAsync(ct);
        var evidence = await db.ReviewEvidence.AsNoTracking().Where(e => e.SessionId == sessionId).ToArrayAsync(ct);
        var targets = await db.LearningExercises.Where(e => e.SessionId == sessionId).Select(e => new { e.Id, e.Prompt }).ToArrayAsync(ct);
        var authors = await db.Users.Where(u => entries.Select(e => e.AuthorId).Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToArrayAsync(ct);
        return reviews.Select(r => new TeacherReviewView(r.Id, r.ExerciseId, r.ExerciseId.HasValue ? targets.Single(e => e.Id == r.ExerciseId).Prompt : "Clase completa", r.Revision, entries.Where(e => e.ReviewId == r.Id).Select(e => new ReviewEntryView(e.Id,
            e.Revision, e.EvidenceRevision, e.AuthorId, authors.Single(u => u.Id == e.AuthorId).DisplayName, e.CreatedAt, e.State.ToString(), e.Attention.ToString(), e.Outcome.ToString(),
            e.Flagged, e.Comment, e.Observation, e.Recommendation, evidence.Where(x => x.EntryId == e.Id).Select(x => x.AttemptId).Order().ToArray())).ToArray())).ToArray();
    }
    public async Task<TeacherReviewView[]> FeedbackAsync(Guid assignmentId, CancellationToken ct)
    {
        var actor = await current.GetAsync(); if (!actor.IsStudent) throw Hidden();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        if (!await studentAccess.ForStudent(actor.Id, actor.OrganizationId).AnyAsync(a => a.Id == assignmentId, ct)) throw Hidden();
        var sessionId = await db.TutoringSessions.Where(s => s.StudentId == actor.Id && s.AssignmentId == assignmentId && s.Mode == TutoringMode.AssignedClass)
            .Select(s => (Guid?)s.Id).SingleOrDefaultAsync(ct);
        var result = await ReviewViewsAsync(sessionId, ct); await tx.CommitAsync(ct); return result;
    }
}
