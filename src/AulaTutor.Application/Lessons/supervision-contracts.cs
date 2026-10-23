using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public sealed record ReviewRequest(Guid RequestId, long Revision, long EvidenceRevision,
    Guid? ExerciseId, string State, string Attention, string Outcome, bool Flagged,
    string Comment, string Observation, string Recommendation, Guid[] EvidenceIds);
public sealed record ReviewEntryView(Guid Id, long Revision, long EvidenceRevision,
    Guid AuthorId, string AuthorName, DateTimeOffset CreatedAt, string State,
    string Attention, string Outcome, bool Flagged, string Comment, string Observation,
    string Recommendation, Guid[] EvidenceIds);
public sealed record TeacherReviewView(Guid Id, Guid? ExerciseId, string Target, long Revision, ReviewEntryView[] History);
public sealed record SupervisionGroupView(Guid Id, string Name, int EligibleStudents);
public sealed record ParticipationCounts(int EligibleStudents, int Opened, int Started, int NotOpened,
    int Active, int Paused, int Completed, int AllRequiredSubmitted, int AllRequiredDelivered,
    int WithPendingRequired, int PendingReviews, int AttentionCases);
public sealed record AssignmentOverview(Guid AssignmentId, Guid GroupId, string GroupName,
    Guid LessonVersionId, int VersionNumber, string ContentHash, string Title, string State,
    DateTimeOffset CreatedAt, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil,
    ParticipationCounts Counts);
public sealed record SupervisionDashboard(SupervisionGroupView[] Groups, AssignmentOverview[] Assignments,
    int PublishedVersions, int PendingReviews, int AttentionCases);
public sealed record ObjectiveMetrics(string Id, string Text, int EligibleStudents, int WorkedStudents,
    int RequiredExercises, int EligibleRequiredSlots, int SubmittedExercises, int DeliveredRequired,
    int VerifiedWithoutHelp, int VerifiedWithHelp, int Attempts, int IncorrectAttempts,
    int NeedsReviewAttempts, int NotEvaluableAttempts, int PendingReviews);
public sealed record ExerciseMetrics(string Id, string ObjectiveId, string Prompt, bool Required,
    int EligibleStudents, int SubmittedStudents, int IncorrectStudents, int IncorrectAttempts,
    int Attempts, int PendingReviews);
public sealed record SupervisedStudent(Guid StudentId, string Name, Guid? SessionId, string State,
    int WorkedObjectives, int SubmittedRequired, int DeliveredRequired, int RequiredExercises,
    int Attempts, int Hints, int Simplifications, int PendingReviews, int AttentionCases,
    DateTimeOffset? OpenedAt, DateTimeOffset? CompletedAt);
public sealed record AttentionCaseView(Guid SessionId, Guid StudentId, string StudentName,
    Guid ExerciseId, string? ApprovedExerciseId, string? ObjectiveId, string Prompt, string Answer,
    string[] Options, int Attempts, int Hints, int Simplifications, string AutomaticOutcome, string[] Reasons,
    Guid[] EvidenceIds, DateTimeOffset DetectedAt, string ReviewState, string Attention,
    bool PendingReview, bool Resolved);
public sealed record ClassSupervision(AssignmentOverview Assignment, SupervisedStudent[] Students,
    ObjectiveMetrics[] Objectives, ExerciseMetrics[] Exercises, AttentionCaseView[] Cases,
    string[] ReinforcementFocus);
public sealed record SupervisedExercise(Guid Id, string? ApprovedExerciseId, string? ObjectiveId,
    bool Required, bool IsComplementary, string Kind, string Prompt, string[] Options, string Rubric,
    bool Presented, int Hints, int Simplifications, SupervisedAttempt[] Attempts);
public sealed record SupervisedAttempt(Guid Id, int ExerciseVersion, string Answer, int Number,
    int HintsUsed, int SimplificationsUsed, string Outcome, string ValidationType,
    string AutomaticFeedback, string? AiFeedback, string? ProviderId, string? ModelId,
    string? PromptVersion, DateTimeOffset SubmittedAt);
public sealed record ReferencedAiObservation(Guid ReportId, int ReportVersion, string ProviderId,
    string ModelId, string PromptVersion, DateTimeOffset GeneratedAt, bool Outdated,
    string Text, Guid[] EvidenceIds);
public sealed record IndividualObjective(string Id, string Text, bool Worked, int DeliveredRequired, int RequiredExercises);
public sealed record IndividualSupervision(AssignmentOverview Assignment, SupervisedStudent Student,
    long EvidenceRevision, IndividualObjective[] Objectives, SupervisedExercise[] Exercises, AttentionCaseView[] Cases,
    TeacherReviewView[] Reviews, ReferencedAiObservation[] AiObservations);

public interface ISupervisionService
{
    Task<SupervisionDashboard> DashboardAsync(Guid? groupId, CancellationToken ct);
    Task<ClassSupervision> ClassAsync(Guid id, CancellationToken ct);
    Task<IndividualSupervision> IndividualAsync(Guid id, Guid studentId, CancellationToken ct);
    Task<TeacherReviewView> ReviewAsync(Guid id, Guid studentId, ReviewRequest request, CancellationToken ct);
    Task<TeacherReviewView[]> FeedbackAsync(Guid assignmentId, CancellationToken ct);
}
