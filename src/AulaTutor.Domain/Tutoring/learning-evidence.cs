namespace AulaTutor.Domain.Tutoring;

public enum ExerciseKind { MultipleChoice, TrueFalse, ShortAnswer, OpenResponse, Code }
public enum AttemptOutcome { CorrectWithoutHelp, CorrectWithHelp, Incorrect, NotEvaluable, NeedsReview }
public enum LearningPlanState { Proposed, Active, Completed, Superseded }

public sealed class LearningExercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? PlanId { get; set; }
    public int ObjectiveOrder { get; set; }
    public int Order { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDiagnostic { get; set; }
    public bool IsPresented { get; set; }
    public ExerciseKind Kind { get; set; }
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public string AcceptedAnswersJson { get; set; } = "[]";
    public string Rubric { get; set; } = "";
    public int HintCount { get; set; }
    public int SimplificationCount { get; set; }
    public bool Completed { get; set; }
    public Guid OriginRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? LessonVersionId { get; set; }
    public string? ApprovedExerciseId { get; set; }
    public string? ApprovedObjectiveId { get; set; }
    public bool Required { get; set; }
    public bool IsComplementary { get; set; }
}

public sealed class ExerciseAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExerciseId { get; set; }
    public int ExerciseVersion { get; set; }
    public Guid RequestId { get; set; }
    public string Answer { get; set; } = "";
    public int Number { get; set; }
    public int HintsUsed { get; set; }
    public int SimplificationsUsed { get; set; }
    public AttemptOutcome Outcome { get; set; }
    public string ValidationType { get; set; } = "Unverified";
    public string Feedback { get; set; } = "";
    public string? ProviderId { get; set; }
    public string? ModelId { get; set; }
    public string? PromptVersion { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}

public sealed class DiagnosticAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int Version { get; set; }
    public long EvidenceRevision { get; set; }
    public bool Preliminary { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
}

// Objectives and activities are immutable validated value records in ContentJson.
// Exercises have their own identity; this is independent of future LessonVersion.
public sealed class PersonalizedLearningPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid DiagnosticId { get; set; }
    public int DiagnosticVersion { get; set; }
    public int Version { get; set; }
    public LearningPlanState State { get; set; }
    public int CurrentActivity { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class LearningReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int Version { get; set; }
    public long EvidenceRevision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string StatisticsJson { get; set; } = "{}";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
}

public sealed class LearningRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ReportId { get; set; }
    public string Goal { get; set; } = "";
    public bool IsGeneral { get; set; }
    public string EvidenceIdsJson { get; set; } = "[]";
}

public sealed class ReportShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ReportId { get; set; }
    public Guid RecipientId { get; set; }
    public Guid RequestId { get; set; }
    public string ConsentVersion { get; set; } = "report-only-v1";
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
