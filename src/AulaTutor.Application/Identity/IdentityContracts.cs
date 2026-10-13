using System.ComponentModel.DataAnnotations;
using AulaTutor.Domain.Identity;

namespace AulaTutor.Application.Identity;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 5)] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [Required, RegularExpression("^(Student|Teacher)$")] string RequestedRole);
public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128)] string Password);
public sealed record RolesRequest([Required, MaxLength(3)] string[] Roles);
public sealed record AccountStatusRequest(bool IsActive);
public sealed record TeacherDecisionRequest(bool Approve);
public sealed record GroupRequest([Required, StringLength(100, MinimumLength = 2)] string Name);
public sealed record MembershipRequest(Guid UserId, [Required, RegularExpression("^(Student|Teacher)$")] string Kind);
public sealed record UserView(Guid Id, string DisplayName, string Email,
    Guid OrganizationId, string[] Roles, string TeacherApproval, bool IsActive);
public sealed record GroupView(Guid Id, string Name, bool IsArchived, int Students, int Teachers, bool CanViewMembers = false);
public sealed record MembershipView(Guid UserId, string DisplayName, string Kind);
public sealed record PageResult<T>(T[] Items, int Page, int PageSize);

public sealed record Actor(Guid Id, Guid OrganizationId, string[] Roles,
    TeacherApproval TeacherApproval, bool IsActive)
{
    public bool IsAdmin => IsActive && Roles.Contains(RoleNames.Admin);
    public bool IsTeacher => IsActive && TeacherApproval == TeacherApproval.Approved
        && Roles.Contains(RoleNames.Teacher);
    public bool IsStudent => IsActive && Roles.Contains(RoleNames.Student);
}

public static class AccessRules
{
    public static bool CanReadUser(Actor actor, Guid userId, Guid organizationId) =>
        actor.IsActive && actor.OrganizationId == organizationId && (actor.Id == userId || actor.IsAdmin);

    public static bool CanReadGroup(Actor actor, Guid organizationId, bool teacherAssigned, bool studentMember) =>
        actor.IsActive && actor.OrganizationId == organizationId &&
        (actor.IsAdmin || (actor.IsTeacher && teacherAssigned) || (actor.IsStudent && studentMember));

    // Group membership never grants access to future private learning conversations.
    public static bool CanReadPrivateTutoring(Actor actor, Guid ownerId, Guid organizationId) =>
        actor.IsActive && actor.OrganizationId == organizationId && actor.Id == ownerId;
}

public sealed class AppException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public interface ICurrentActor { Task<Actor> GetAsync(); }
public interface IIdentityService
{
    Task RegisterAsync(RegisterRequest request);
    Task<bool> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<UserView> MeAsync();
    Task<UserView> GetUserAsync(Guid id);
    Task<PageResult<UserView>> ListUsersAsync(int page, string? email = null);
    Task DecideTeacherAsync(Guid id, bool approve);
    Task ChangeRolesAsync(Guid id, string[] roles);
    Task ChangeStatusAsync(Guid id, bool isActive);
}
public interface IGroupService
{
    Task<GroupView[]> ListAsync();
    Task<GroupView> GetAsync(Guid id);
    Task<GroupView> CreateAsync(string name);
    Task RenameAsync(Guid id, string name);
    Task ArchiveAsync(Guid id);
    Task<MembershipView[]> MembersAsync(Guid id);
    Task AddMemberAsync(Guid id, MembershipRequest request);
    Task RemoveMemberAsync(Guid id, Guid userId, string kind);
}
