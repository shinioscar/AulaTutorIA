using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;

namespace AulaTutor.UnitTests;

public sealed class AccessRulesTests
{
    [Fact]
    public void GroupRoleRequiresRelationAndSameOrganization()
    {
        var teacher = new Actor(Guid.NewGuid(), PilotOrganization.Id, [RoleNames.Teacher], TeacherApproval.Approved, true);
        Assert.False(AccessRules.CanReadGroup(teacher, PilotOrganization.Id, false, false));
        Assert.True(AccessRules.CanReadGroup(teacher, PilotOrganization.Id, true, false));
        Assert.False(AccessRules.CanReadGroup(teacher, Guid.NewGuid(), true, false));
        Assert.False(AccessRules.CanReadGroup(teacher with {TeacherApproval = TeacherApproval.Pending}, PilotOrganization.Id, true, false));
    }
    [Fact]
    public void PrivateTutoringNeverFollowsAdminOrTeacherGroupRoles()
    {
        var owner = Guid.NewGuid();
        var actor = new Actor(Guid.NewGuid(), PilotOrganization.Id, RoleNames.All, TeacherApproval.Approved, true);
        Assert.False(AccessRules.CanReadPrivateTutoring(actor, owner, PilotOrganization.Id));
        Assert.True(AccessRules.CanReadPrivateTutoring(actor with {Id = owner}, owner, PilotOrganization.Id));
        Assert.False(AccessRules.CanReadPrivateTutoring(actor with {Id = owner, IsActive = false}, owner, PilotOrganization.Id));
    }
    [Fact]
    public void StudentProfileIsOwnerOrAdminOnly()
    {
        var student = new Actor(Guid.NewGuid(), PilotOrganization.Id, [RoleNames.Student], TeacherApproval.NotRequested, true);
        Assert.True(AccessRules.CanReadUser(student, student.Id, PilotOrganization.Id));
        Assert.False(AccessRules.CanReadUser(student, Guid.NewGuid(), PilotOrganization.Id));
        Assert.False(AccessRules.CanReadUser(student, student.Id, Guid.NewGuid()));
    }
}
