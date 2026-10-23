using System.Data;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using AulaTutor.Infrastructure.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Lessons;

public sealed partial class SupervisionService(AulaTutorDbContext db, ICurrentActor current,
    TimeProvider clock, AssignedClassAccess studentAccess) : ISupervisionService
{
    private static AppException Hidden() => new(404, "La clase o evidencia no está disponible para tu cuenta.");
    private async Task<Actor> TeacherAsync(bool listing = false)
    {
        var a = await current.GetAsync();
        if (!a.IsTeacher || !await db.Users.AnyAsync(u => u.Id == a.Id && u.IsActive && u.OrganizationId == a.OrganizationId && u.TeacherApproval == TeacherApproval.Approved) ||
            !await db.UserRoles.AnyAsync(ur => ur.UserId == a.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Teacher)))
            throw listing ? new AppException(403, "Se requiere docente aprobado.") : Hidden();
        return a;
    }
    private IQueryable<AcademicGroup> Groups(Actor a) => db.Groups.Where(g => g.OrganizationId == a.OrganizationId && !g.IsArchived &&
        db.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == a.Id && m.Kind == GroupMemberKind.Teacher && m.OrganizationId == a.OrganizationId));
    private IQueryable<LessonAssignment> Assignments(Actor a) => db.LessonAssignments.Where(x => x.TeacherId == a.Id && x.OrganizationId == a.OrganizationId &&
        x.State != AssignmentState.Withdrawn && Groups(a).Any(g => g.Id == x.GroupId) &&
        db.Users.Any(u => u.Id == a.Id && u.IsActive && u.OrganizationId == a.OrganizationId && u.TeacherApproval == TeacherApproval.Approved) &&
        db.UserRoles.Any(ur => ur.UserId == a.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Teacher)));
    private IQueryable<AppUser> Cohort(LessonAssignment a) => db.Users.Where(u => u.OrganizationId == a.OrganizationId && u.IsActive &&
        db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Student)) &&
        db.GroupMemberships.Any(m => m.GroupId == a.GroupId && m.UserId == u.Id && m.Kind == GroupMemberKind.Student && m.OrganizationId == a.OrganizationId));
    public async Task<SupervisionDashboard> DashboardAsync(Guid? groupId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var actor = await TeacherAsync(true);
        var groups = await Groups(actor).AsNoTracking().OrderBy(g => g.Name).ToArrayAsync(ct);
        if (groupId.HasValue && !groups.Any(g => g.Id == groupId)) throw Hidden();
        var groupViews = new List<SupervisionGroupView>();
        foreach (var g in groups)
            groupViews.Add(new(g.Id, g.Name, await Cohort(new() { GroupId = g.Id, OrganizationId = actor.OrganizationId }).CountAsync(ct)));
        var assignments = await Assignments(actor).AsNoTracking().Where(a => !groupId.HasValue || a.GroupId == groupId).OrderByDescending(a => a.CreatedAt).ToArrayAsync(ct);
        var overviews = new List<AssignmentOverview>();
        foreach (var a in assignments) overviews.Add((await BuildClassAsync(a, ct)).Assignment);
        var published = await db.LessonPublications.CountAsync(p => p.ArchivedAt == null && db.Lessons.Any(l => l.Id == p.LessonId && l.TeacherId == actor.Id && l.OrganizationId == actor.OrganizationId), ct);
        await tx.CommitAsync(ct);
        return new(groupViews.ToArray(), overviews.ToArray(), published, overviews.Sum(a => a.Counts.PendingReviews), overviews.Sum(a => a.Counts.AttentionCases));
    }
    public async Task<ClassSupervision> ClassAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var actor = await TeacherAsync();
        var assignment = await Assignments(actor).AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw Hidden();
        var result = await BuildClassAsync(assignment, ct); await tx.CommitAsync(ct); return result;
    }
    private sealed record Snapshot(LessonAssignment Assignment, LessonVersion Version, LessonContent Content,
        string GroupName, AppUser[] Students, TutoringSession[] Sessions, LearningExercise[] Exercises,
        ExerciseAttempt[] Attempts, TeacherReview[] Reviews, TeacherReviewEntry[] Entries, ReviewEvidence[] Evidence);
    private async Task<Snapshot> LoadAsync(LessonAssignment assignment, CancellationToken ct)
    {
        var version = await db.LessonVersions.AsNoTracking().SingleAsync(v => v.Id == assignment.LessonVersionId, ct);
        var students = await Cohort(assignment).AsNoTracking().OrderBy(s => s.DisplayName).ThenBy(s => s.Id).ToArrayAsync(ct);
        var eligible = Cohort(assignment).Select(u => u.Id);
        var sessionsQuery = db.TutoringSessions.Where(s => s.Mode == TutoringMode.AssignedClass && s.AssignmentId == assignment.Id &&
            s.LessonVersionId == assignment.LessonVersionId && eligible.Contains(s.StudentId));
        var sessions = await sessionsQuery.AsNoTracking().ToArrayAsync(ct);
        var ids = sessionsQuery.Select(s => s.Id);
        var exercises = await db.LearningExercises.AsNoTracking().Where(e => ids.Contains(e.SessionId)).OrderBy(e => e.Order).ToArrayAsync(ct);
        var attempts = await db.ExerciseAttempts.AsNoTracking().Where(a => ids.Contains(a.SessionId)).OrderBy(a => a.Number).ToArrayAsync(ct);
        var reviews = await db.TeacherReviews.AsNoTracking().Where(r => ids.Contains(r.SessionId)).ToArrayAsync(ct);
        var entries = await db.TeacherReviewEntries.AsNoTracking().Where(r => ids.Contains(r.SessionId)).OrderByDescending(r => r.Revision).ToArrayAsync(ct);
        var evidence = await db.ReviewEvidence.AsNoTracking().Where(r => ids.Contains(r.SessionId)).ToArrayAsync(ct);
        return new(assignment, version, LearningJson.Read<LessonContent>(version.ContentJson),
            await db.Groups.Where(g => g.Id == assignment.GroupId).Select(g => g.Name).SingleAsync(ct), students, sessions, exercises, attempts, reviews, entries, evidence);
    }
    private async Task<ClassSupervision> BuildClassAsync(LessonAssignment assignment, CancellationToken ct)
    { var data = await LoadAsync(assignment, ct); return ClassView(data); }
    private static ClassSupervision ClassView(Snapshot d)
    {
        var core = d.Exercises.Where(e => e.LessonVersionId == d.Version.Id && e.ApprovedExerciseId != null).ToArray();
        var approved = d.Content.Sections.SelectMany(s => s.Exercises).ToArray();
        var cases = Cases(d);
        var required = approved.Count(e => e.Required);
        var students = d.Students.Select(u => StudentView(d, u, cases, required)).ToArray();
        var counts = new ParticipationCounts(students.Length, students.Count(s => s.SessionId.HasValue), students.Count(s => s.SessionId.HasValue && s.State != "Created"),
            students.Count(s => !s.SessionId.HasValue), students.Count(s => s.State == "Active"), students.Count(s => s.State == "Paused"), students.Count(s => s.State == "Completed"),
            students.Count(s => s.SessionId.HasValue && s.State != "Created" && s.SubmittedRequired == required),
            students.Count(s => s.SessionId.HasValue && s.State != "Created" && s.DeliveredRequired == required),
            students.Count(s => !s.SessionId.HasValue || s.State == "Created" || s.DeliveredRequired < required), cases.Count(c => c.PendingReview), cases.Count(c => !c.Resolved));
        var overview = new AssignmentOverview(d.Assignment.Id, d.Assignment.GroupId, d.GroupName, d.Version.Id, d.Version.Number, d.Version.ContentHash,
            d.Content.Title, d.Assignment.State.ToString(), d.Assignment.CreatedAt, d.Assignment.AvailableFrom, d.Assignment.AvailableUntil, counts);
        var objectives = d.Content.Objectives.Select(o =>
        {
            var exercises = core.Where(e => e.ApprovedObjectiveId == o.Id).ToArray(); var exerciseIds = exercises.Select(e => e.Id).ToHashSet();
            var attempts = d.Attempts.Where(a => exerciseIds.Contains(a.ExerciseId)).ToArray();
            var without = attempts.Where(a => a.Outcome == AttemptOutcome.CorrectWithoutHelp).Select(a => a.ExerciseId).ToHashSet();
            var with = attempts.Where(a => a.Outcome == AttemptOutcome.CorrectWithHelp && !without.Contains(a.ExerciseId)).Select(a => a.ExerciseId).Distinct().Count();
            var requiredCount = approved.Count(e => e.ObjectiveId == o.Id && e.Required);
            return new ObjectiveMetrics(o.Id, o.Text, students.Length, attempts.Select(a => a.StudentId).Distinct().Count(), requiredCount, requiredCount * students.Length,
                attempts.Select(a => a.ExerciseId).Distinct().Count(), exercises.Count(e => e.Required && SupervisionRules.Delivered(attempts.Where(a => a.ExerciseId == e.Id))),
                without.Count, with, attempts.Length, attempts.Count(a => a.Outcome == AttemptOutcome.Incorrect), attempts.Count(a => a.Outcome == AttemptOutcome.NeedsReview),
                attempts.Count(a => a.Outcome == AttemptOutcome.NotEvaluable), cases.Count(c => c.ObjectiveId == o.Id && c.PendingReview));
        }).ToArray();
        var exerciseMetrics = approved.Select(e =>
        {
            var ids = core.Where(x => x.ApprovedExerciseId == e.Id).Select(x => x.Id).ToHashSet(); var attempts = d.Attempts.Where(a => ids.Contains(a.ExerciseId)).ToArray();
            return new ExerciseMetrics(e.Id, e.ObjectiveId, e.Activity.Prompt, e.Required, students.Length, attempts.Select(a => a.StudentId).Distinct().Count(),
                attempts.Where(a => a.Outcome == AttemptOutcome.Incorrect).Select(a => a.StudentId).Distinct().Count(), attempts.Count(a => a.Outcome == AttemptOutcome.Incorrect),
                attempts.Length, cases.Count(c => c.ApprovedExerciseId == e.Id && c.PendingReview));
        }).OrderByDescending(e => e.IncorrectStudents).ThenByDescending(e => e.PendingReviews).ToArray();
        var focus = objectives.Where(o => o.IncorrectAttempts > 0 || o.PendingReviews > 0).Select(o =>
            $"{o.Text}: {o.IncorrectAttempts} intentos incorrectos y {o.PendingReviews} ejercicios pendientes de revisión; consultar evidencias antes de decidir refuerzo.").ToArray();
        return new(overview, students, objectives, exerciseMetrics, cases, focus);
    }
    private static SupervisedStudent StudentView(Snapshot d, AppUser u, AttentionCaseView[] cases, int required)
    {
        var session = d.Sessions.SingleOrDefault(s => s.StudentId == u.Id);
        var exercises = d.Exercises.Where(e => e.SessionId == session?.Id).ToArray();
        var attempts = d.Attempts.Where(a => a.SessionId == session?.Id).ToArray();
        var submittedIds = attempts.Select(a => a.ExerciseId).ToHashSet();
        return new(u.Id, u.DisplayName, session?.Id, session?.State.ToString() ?? "NotOpened",
            exercises.Where(e => e.ApprovedObjectiveId != null && submittedIds.Contains(e.Id)).Select(e => e.ApprovedObjectiveId).Distinct().Count(),
            exercises.Count(e => e.Required && submittedIds.Contains(e.Id)), exercises.Count(e => e.Required && SupervisionRules.Delivered(attempts.Where(a => a.ExerciseId == e.Id))),
            required, attempts.Length, exercises.Sum(e => e.HintCount), exercises.Sum(e => e.SimplificationCount), cases.Count(c => c.StudentId == u.Id && c.PendingReview),
            cases.Count(c => c.StudentId == u.Id && !c.Resolved), session?.CreatedAt, session?.CompletedAt);
    }
    private static AttentionCaseView[] Cases(Snapshot d)
    {
        var result = new List<AttentionCaseView>();
        foreach (var e in d.Exercises)
        {
            var attempts = d.Attempts.Where(a => a.ExerciseId == e.Id).OrderBy(a => a.Number).ToArray();
            var last = attempts.LastOrDefault(); var review = d.Reviews.SingleOrDefault(r => r.ExerciseId == e.Id);
            var entry = d.Entries.FirstOrDefault(r => r.ReviewId == review?.Id);
            var examined = d.Evidence.Where(r => r.EntryId == entry?.Id).Select(r => r.AttemptId).ToArray();
            var covered = SupervisionRules.CoversLatest(entry, examined, attempts);
            var reasons = SupervisionRules.Reasons(attempts, entry?.Flagged == true || entry?.State == TeacherReviewState.FollowUpNeeded);
            if (reasons.Length == 0 && entry is null) continue;
            var resolved = reasons.Length == 0 || (covered && entry?.Attention == AttentionState.Closed);
            var pending = !resolved && (reasons.Contains("RequiresHumanReview") || reasons.Contains("NotEvaluable") || reasons.Contains("TeacherFlagged")) &&
                !(covered && entry?.State == TeacherReviewState.Reviewed);
            var session = d.Sessions.Single(s => s.Id == e.SessionId); var student = d.Students.Single(s => s.Id == session.StudentId);
            var dates = new List<DateTimeOffset>();
            if (reasons.Contains("RequiresHumanReview") || reasons.Contains("NotEvaluable")) dates.Add(last!.SubmittedAt);
            if (reasons.Contains("RepeatedErrors")) dates.Add(attempts.Where(a => a.Outcome == AttemptOutcome.Incorrect).ElementAt(2).SubmittedAt);
            if (reasons.Contains("AttemptsExhausted")) dates.Add(attempts[9].SubmittedAt);
            if (reasons.Contains("TeacherFlagged")) dates.Add(entry!.CreatedAt);
            result.Add(new(session.Id, student.Id, student.DisplayName, e.Id, e.ApprovedExerciseId, e.ApprovedObjectiveId, e.Prompt, last?.Answer ?? "",
                LearningJson.Read<string[]>(e.OptionsJson), attempts.Length, e.HintCount, e.SimplificationCount, last?.Outcome.ToString() ?? "NoAttempt", reasons,
                attempts.Select(a => a.Id).ToArray(), dates.Count > 0 ? dates.Min() : entry!.CreatedAt,
                covered ? entry!.State.ToString() : "Pending", resolved ? "Closed" : covered ? entry!.Attention.ToString() : "Pending", pending, resolved));
        }
        return result.OrderBy(c => c.Resolved).ThenBy(c => c.DetectedAt).ToArray();
    }
    public async Task<IndividualSupervision> IndividualAsync(Guid id, Guid studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var actor = await TeacherAsync();
        var assignment = await Assignments(actor).AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw Hidden();
        if (!await Cohort(assignment).AnyAsync(u => u.Id == studentId, ct)) throw Hidden();
        var d = await LoadAsync(assignment, ct); var report = ClassView(d); var student = report.Students.Single(s => s.StudentId == studentId);
        var session = d.Sessions.SingleOrDefault(s => s.Id == student.SessionId);
        var exercises = d.Exercises.Where(e => e.SessionId == session?.Id).Select(e => new SupervisedExercise(e.Id, e.ApprovedExerciseId, e.ApprovedObjectiveId,
            e.Required, e.IsComplementary, e.Kind.ToString(), e.Prompt, LearningJson.Read<string[]>(e.OptionsJson), e.Rubric, e.IsPresented, e.HintCount, e.SimplificationCount,
            d.Attempts.Where(a => a.ExerciseId == e.Id).OrderBy(a => a.Number).Select(Attempt).ToArray())).ToArray();
        var reviews = await ReviewViewsAsync(session?.Id, ct);
        var objectives = d.Content.Objectives.Select(o => new IndividualObjective(o.Id, o.Text,
            exercises.Any(e => e.ObjectiveId == o.Id && e.Attempts.Length > 0),
            exercises.Count(e => e.ObjectiveId == o.Id && e.Required && e.Attempts.Any(a => a.Outcome is "CorrectWithoutHelp" or "CorrectWithHelp" or "NeedsReview")),
            d.Content.Sections.SelectMany(s => s.Exercises).Count(e => e.ObjectiveId == o.Id && e.Required))).ToArray();
        var ai = new List<ReferencedAiObservation>();
        if (session is not null)
        {
            var ids = d.Attempts.Where(a => a.SessionId == session.Id).Select(a => a.Id).ToHashSet();
            var reports = await db.LearningReports.AsNoTracking().Where(r => r.SessionId == session.Id && r.ProviderId != "Application").OrderByDescending(r => r.Version).ToArrayAsync(ct);
            foreach (var r in reports)
            {
                var content = LearningJson.Read<ReportContent>(r.ContentJson);
                foreach (var o in content.Observations.Where(o => o.EvidenceIds.Length > 0 && o.EvidenceIds.All(ids.Contains)))
                    ai.Add(new(r.Id, r.Version, r.ProviderId, r.ModelId, r.PromptVersion, r.GeneratedAt, r.EvidenceRevision != session.EvidenceRevision, o.Text, o.EvidenceIds));
                foreach (var o in content.Recommendations.Where(o => o.EvidenceIds.Length > 0 && o.EvidenceIds.All(ids.Contains)))
                    ai.Add(new(r.Id, r.Version, r.ProviderId, r.ModelId, r.PromptVersion, r.GeneratedAt, r.EvidenceRevision != session.EvidenceRevision, o.Goal, o.EvidenceIds));
            }
        }
        await tx.CommitAsync(ct);
        return new(report.Assignment, student, session?.EvidenceRevision ?? 0, objectives, exercises, report.Cases.Where(c => c.StudentId == studentId).ToArray(), reviews, ai.ToArray());
    }
    private static SupervisedAttempt Attempt(ExerciseAttempt a)
    {
        const string marker = "\n\nInterpretación IA (no verificada): ";
        var index = a.Feedback.IndexOf(marker, StringComparison.Ordinal);
        return new(a.Id, a.ExerciseVersion, a.Answer, a.Number, a.HintsUsed, a.SimplificationsUsed,
            a.Outcome.ToString(), a.ValidationType, index < 0 ? a.Feedback : a.Feedback[..index],
            index < 0 ? null : a.Feedback[(index + marker.Length)..], a.ProviderId, a.ModelId, a.PromptVersion, a.SubmittedAt);
    }
}
