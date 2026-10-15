namespace AulaTutor.Domain.Lessons;

public enum AssignmentState { Active, Inactive, Withdrawn }

// Version and destination are immutable. A replacement is a new assignment.
public sealed class LessonAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Guid LessonVersionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid GroupId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid CreationKey { get; set; }
    public string CreationHash { get; set; } = "";
    public AssignmentState State { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? AvailableFrom { get; set; }
    public DateTimeOffset? AvailableUntil { get; set; }
}

public sealed class AssignmentChange
{
    public Guid AssignmentId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string InputHash { get; set; } = "";
    public string Action { get; set; } = "";
    public AssignmentState State { get; set; }
    public DateTimeOffset? AvailableFrom { get; set; }
    public DateTimeOffset? AvailableUntil { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
