namespace AulaTutor.Domain.Lessons;

public enum LessonState { Draft, Simulation, InReview, Approved, Published, Archived }
public enum ProposalState { Pending, Applied, Discarded }
public enum LessonOperationState { Pending, Succeeded, Failed, Cancelled }

public sealed class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreationKey { get; set; }
    public LessonState State { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class LessonDraft
{
    public Guid LessonId { get; set; }
    public long Revision { get; set; }
    public string ContentJson { get; set; } = "{}";
}
public sealed class LessonDraftRevision
{
    public Guid LessonId { get; set; }
    public long Revision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset SavedAt { get; set; }
}
// Approval freezes every academic field. Publication is stored separately.
public sealed class LessonVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public int Number { get; set; }
    public long DraftRevision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string ContentHash { get; set; } = "";
    public Guid ApprovedBy { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
}
public sealed class LessonPublication
{
    public Guid VersionId { get; set; }
    public Guid LessonId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
public sealed class LessonProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Guid RequestId { get; set; }
    public long DraftRevision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string Explanation { get; set; } = "";
    public ProposalState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class LessonSimulation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public long DraftRevision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public Guid? AiBindingId { get; set; }
    public string State { get; set; } = "Active";
    public long Revision { get; set; }
    public int SectionIndex { get; set; }
    public int ExerciseIndex { get; set; }
    public int HintCount { get; set; }
    public int Attempts { get; set; }
    public bool Answered { get; set; }
    public Guid? PendingRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
}
public sealed class LessonOperation
{
    public Guid LessonId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? SimulationId { get; set; }
    public string Action { get; set; } = "";
    public string Text { get; set; } = "";
    public string InputHash { get; set; } = "";
    public long DraftRevision { get; set; }
    public string ContextJson { get; set; } = "{}";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public Guid? AiBindingId { get; set; }
    public LessonOperationState State { get; set; }
    public Guid LeaseToken { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
    public DateTimeOffset? RetryNotBefore { get; set; }
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
    public string? Reply { get; set; }
    public string? Outcome { get; set; }
    public string? ValidationType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}
public sealed class LessonCommand
{
    public Guid LessonId { get; set; }
    public Guid RequestId { get; set; }
    public string InputHash { get; set; } = "";
}
