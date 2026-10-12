using AulaTutor.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace AulaTutor.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public string DisplayName { get; set; } = "";
    public TeacherApproval TeacherApproval { get; set; }
    public bool IsActive { get; set; } = true;
}
