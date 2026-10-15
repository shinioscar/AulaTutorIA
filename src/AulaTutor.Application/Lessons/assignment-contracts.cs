using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public sealed record CreateAssignmentRequest(Guid RequestId, Guid LessonVersionId, Guid GroupId,
    DateTimeOffset? AvailableFrom = null, DateTimeOffset? AvailableUntil = null);
public sealed record AssignmentCommandRequest(Guid RequestId, long Revision,
    DateTimeOffset? AvailableFrom = null, DateTimeOffset? AvailableUntil = null);
public sealed record StartAssignedClassRequest(Guid RequestId);
public sealed record AssignmentGroupView(Guid Id, string Name);
public sealed record AssignmentChangeView(string Action, string State, DateTimeOffset ChangedAt,
    DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil);
public sealed record PublishedClassView(Guid LessonId, Guid VersionId, int Number, string Title,
    string Subject, DateTimeOffset PublishedAt, string ContentHash);
public sealed record AssignmentView(Guid Id, Guid LessonId, Guid LessonVersionId, int VersionNumber,
    string Title, string Subject, string ContentHash, Guid GroupId, string GroupName,
    Guid TeacherId, string TeacherName, string State, long Revision, DateTimeOffset CreatedAt,
    DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil, bool Available,
    AssignmentChangeView[] Changes);
// Public academic projection: never accepted answers or reference solutions.
public sealed record AssignedSectionView(string Id, string Title, string Content);
public sealed record AssignedObjectiveView(string Id, string Text, int Delivered, int Required);
public sealed record AssignedClassView(Guid AssignmentId, Guid LessonVersionId, int VersionNumber,
    string ContentHash, string Title, string Subject, string TeacherName, string GroupName,
    string State, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil, bool Available,
    Guid? SessionId, string StudentState, int DeliveredRequired, int TotalRequired,
    int VerifiedRequired, string Methodology, string Instructions, string Prerequisites,
    AssignedObjectiveView[] Objectives, AssignedSectionView[] Sections);
public sealed record AssignedSessionView(AssignedClassView Class, Guid? CurrentExerciseId,
    string SectionTitle, string SectionContent, bool CanComplete, string? UnavailableReason);

public interface IAssignmentService
{
    Task<PublishedClassView[]> PublishedAsync(CancellationToken ct);
    Task<AssignmentGroupView[]> GroupsAsync(CancellationToken ct);
    Task<AssignmentView[]> ListAsync(CancellationToken ct);
    Task<AssignmentView> GetAsync(Guid id, CancellationToken ct);
    Task<AssignmentView> CreateAsync(CreateAssignmentRequest request, CancellationToken ct);
    Task<AssignmentView> CommandAsync(Guid id, string action, AssignmentCommandRequest request, CancellationToken ct);
    Task<AssignedClassView[]> StudentListAsync(CancellationToken ct);
    Task<AssignedClassView> StudentGetAsync(Guid id, CancellationToken ct);
    Task<TutoringSessionView> StartAsync(Guid id, StartAssignedClassRequest request, CancellationToken ct);
}
