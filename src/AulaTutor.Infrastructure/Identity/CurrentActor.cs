using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace AulaTutor.Infrastructure.Identity;

public sealed class CurrentActor(IHttpContextAccessor accessor, UserManager<AppUser> users) : ICurrentActor
{
    private Actor? cached;
    public async Task<Actor> GetAsync()
    {
        if (cached is not null) return cached;
        var principal = accessor.HttpContext?.User;
        var user = principal is null ? null : await users.GetUserAsync(principal);
        if (user is null || !user.IsActive || user.OrganizationId != PilotOrganization.Id)
            throw new AppException(401, "La sesión no está disponible. Inicia sesión nuevamente.");
        return cached = new Actor(user.Id, user.OrganizationId,
            (await users.GetRolesAsync(user)).ToArray(), user.TeacherApproval, user.IsActive);
    }
}
