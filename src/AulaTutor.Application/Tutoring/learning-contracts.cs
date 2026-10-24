using AulaTutor.Domain.Tutoring;

namespace AulaTutor.Application.Tutoring;

public sealed record EvidenceObservation(string Text, Guid[] EvidenceIds);
public sealed record AssessmentContent(string Summary, EvidenceObservation[] Strengths,
    EvidenceObservation[] Difficulties, string[] NotAssessed, string[] Practice);
public sealed record ExerciseProposal(string Prompt, string Kind, string[] Options,
    string[] AcceptedAnswers, string Rubric);
public sealed record LearningPlanActivity(string Explanation, string Difficulty, int EstimatedMinutes, ExerciseProposal Exercise);
public sealed record LearningPlanObjective(string Title, Guid[] EvidenceIds, LearningPlanActivity[] Activities);
public sealed record PlanContent(string MainGoal, string Progression, LearningPlanObjective[] Objectives);
public sealed record RecommendationProposal(string Goal, Guid[] EvidenceIds);
public sealed record ReportContent(string Summary, EvidenceObservation[] Observations,
    string[] NotAssessed, RecommendationProposal[] Recommendations);
public sealed record LearningStatistics(int Exercises, int Attempts, int CorrectWithoutHelp,
    int CorrectWithHelp, int Incorrect, int NeedsReview, int NotEvaluable, int Hints, string[]? WorkedObjectives = null);
public sealed record ExerciseView(Guid Id, int Version, bool IsDiagnostic, string Kind, string Prompt,
    string[] Options, string Rubric, int Hints, bool Completed, int ObjectiveOrder, int Order, int Simplifications,
    Guid? LessonVersionId = null, string? ApprovedExerciseId = null, string? ApprovedObjectiveId = null,
    bool Required = false, bool IsComplementary = false);
public sealed record AttemptView(Guid Id, Guid RequestId, Guid ExerciseId, int ExerciseVersion, string Answer,
    int Number, int HintsUsed, string Outcome, string ValidationType, string Feedback,
    DateTimeOffset SubmittedAt, string? ProviderId, string? ModelId, int SimplificationsUsed);
public sealed record DiagnosticView(Guid Id, int Version, bool Preliminary, bool Outdated,
    AssessmentContent Content, string ProviderId, string ModelId, string PromptVersion);
public sealed record PlanView(Guid Id, int Version, Guid DiagnosticId, int DiagnosticVersion, string State,
    int CurrentActivity, PlanContentView Content, DateTimeOffset? AcceptedAt, DateTimeOffset? CompletedAt);
// Public projection deliberately excludes private answer keys.
public sealed record PlanContentView(string MainGoal, string Progression, ObjectiveView[] Objectives);
public sealed record ObjectiveView(string Title, Guid[] EvidenceIds, ActivityView[] Activities, string Progress);
public sealed record ActivityView(string Explanation, string Difficulty, int EstimatedMinutes);
public sealed record ReportView(Guid Id, int Version, bool Outdated, LearningStatistics Statistics,
    ReportContent Content, string ProviderId, string ModelId, string PromptVersion, DateTimeOffset GeneratedAt, string Topic, string Goal);
public sealed record RecommendationView(Guid Id, Guid ReportId, string Goal, bool IsGeneral);
public sealed record ShareView(Guid Id, Guid ReportId, Guid RecipientId, string RecipientName,
    DateTimeOffset GrantedAt, DateTimeOffset? RevokedAt);
public sealed record ShareRecipient(Guid Id, string DisplayName);
public sealed record LearningView(long EvidenceRevision, int DiagnosticQuestionLimit, int MinimumEvidence,
    ExerciseView[] Exercises, AttemptView[] Attempts, DiagnosticView? Diagnostic, PlanView? Plan,
    ReportView[] Reports, RecommendationView[] Recommendations, ShareView[] Shares);
public sealed record ShareReportRequest(Guid RequestId, Guid ReportId, Guid RecipientId, bool Consent);
public sealed record ReinforcementRequest(Guid RequestId);
public interface ILearningService
{
    Task<ShareRecipient[]> RecipientsAsync(Guid sessionId, CancellationToken ct);
    Task<ShareView> ShareAsync(Guid sessionId, ShareReportRequest request, CancellationToken ct);
    Task RevokeAsync(Guid sessionId, Guid shareId, CancellationToken ct);
    Task<ReportView> SharedReportAsync(Guid reportId, CancellationToken ct);
    Task<ReportView[]> SharedReportsAsync(CancellationToken ct);
    Task<TutoringSessionView> ReinforceAsync(Guid sessionId, Guid recommendationId, ReinforcementRequest request, CancellationToken ct);
}

public static class ExerciseValidators
{
    public static (AttemptOutcome Outcome, string Type, string Feedback) Validate(ExerciseKind kind,
        string answer, IReadOnlyList<string> accepted, int hints)
    {
        if (kind is ExerciseKind.OpenResponse or ExerciseKind.Code)
            return (AttemptOutcome.NeedsReview, "Unverified", "Respuesta registrada. Pendiente de revisión docente.");
        if (accepted.Count == 0) return (AttemptOutcome.NotEvaluable, "Unverified", "No existe una regla verificable para este ejercicio.");
        var correct = accepted.Any(a => string.Equals(a.Trim().Normalize(), answer.Trim().Normalize(), StringComparison.OrdinalIgnoreCase));
        return (correct ? hints > 0 ? AttemptOutcome.CorrectWithHelp : AttemptOutcome.CorrectWithoutHelp : AttemptOutcome.Incorrect,
            "Deterministic", correct ? "Coincide con la regla de este ejercicio." : "No coincide con las alternativas aceptadas. Puedes volver a intentar.");
    }
}
