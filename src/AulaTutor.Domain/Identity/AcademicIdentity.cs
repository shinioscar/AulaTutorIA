namespace AulaTutor.Domain.Identity;

public static class PilotOrganization
{
    public static readonly Guid Id = Guid.Parse("ba70323d-37d1-44a9-8d93-a613fdb32dd0");
}

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public static readonly string[] All = [Admin, Teacher, Student];
}

public enum TeacherApproval { NotRequested, Pending, Approved, Rejected }
public enum GroupMemberKind { Student, Teacher }

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class AcademicGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public string Name { get; set; } = "";
    public bool IsArchived { get; set; }
}

public sealed class GroupMembership
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public GroupMemberKind Kind { get; set; }
}
