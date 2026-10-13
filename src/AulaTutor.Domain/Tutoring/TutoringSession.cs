using AulaTutor.Domain.Identity;

namespace AulaTutor.Domain.Tutoring;

public enum TutoringState { Created, Active, Paused, Completed }
public enum TutoringAction { Start, Message, Hint, Simplify }
public enum TutoringTurnState { Pending, Succeeded, Failed, Cancelled }
public enum TutoringMessageRole { Student, Tutor }

public sealed class TutoringSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public Guid CreationKey { get; set; }
    public string Topic { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Level { get; set; } = "Beginner";
    public bool DirectReview { get; set; }
    public string PolicyVersion { get; set; } = "tutoring-v1";
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
    public TutoringState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long Revision { get; set; }
    public int NextSequence { get; set; }
    public Guid? PendingRequestId { get; set; }
    public string CurrentFocus { get; set; } = "";
    public string Activity { get; set; } = "NotStarted";
    public string? Summary { get; set; }
}

public sealed class TutoringTurn
{
    public Guid SessionId { get; set; }
    public Guid RequestId { get; set; }
    public TutoringAction Action { get; set; }
    public string InputHash { get; set; } = "";
    public TutoringTurnState State { get; set; }
    public int Attempts { get; set; }
    public Guid LeaseToken { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset? RetryNotBefore { get; set; }
    public long? DurationMs { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

public sealed class TutoringMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid RequestId { get; set; }
    public int Sequence { get; set; }
    public TutoringMessageRole Role { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string? ProviderId { get; set; }
    public string? ModelId { get; set; }
    public string? PromptVersion { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}
