namespace AulaTutor.Domain.Lessons;

public enum TeacherReviewState { Pending, InReview, Reviewed, FollowUpNeeded }
public enum AttentionState { Pending, InProgress, Closed }
public enum HumanReviewOutcome { NotAssessed, Supported, NeedsReinforcement, InsufficientEvidence }

// A target identifies either one exercise or the assigned session as a whole.
public sealed class TeacherReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? ExerciseId { get; set; }
    public Guid TargetId { get; set; }
    public long Revision { get; set; }
}

// Append-only decisions: never rewrite the student's answer or automatic outcome.
public sealed class TeacherReviewEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewId { get; set; }
    public Guid SessionId { get; set; }
    public Guid RequestId { get; set; }
    public string InputHash { get; set; } = "";
    public long Revision { get; set; }
    public long EvidenceRevision { get; set; }
    public Guid AuthorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TeacherReviewState State { get; set; }
    public AttentionState Attention { get; set; }
    public HumanReviewOutcome Outcome { get; set; }
    public bool Flagged { get; set; }
    public string Comment { get; set; } = "";
    public string Observation { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class ReviewEvidence
{
    public Guid EntryId { get; set; }
    public Guid SessionId { get; set; }
    public Guid AttemptId { get; set; }
}
