using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Identity;

public sealed class IdentityService(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    AulaTutorDbContext db, ICurrentActor current) : IIdentityService
{
    public async Task RegisterAsync(RegisterRequest request)
    {
        if (request.RequestedRole is not (RoleNames.Student or RoleNames.Teacher))
            throw new AppException(400, "Tipo de registro no permitido.");
        var user = new AppUser
        {
            Id = Guid.NewGuid(), UserName = request.Email.Trim(), Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(), LockoutEnabled = true,
            TeacherApproval = request.RequestedRole == RoleNames.Teacher ? TeacherApproval.Pending : TeacherApproval.NotRequested
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = await users.CreateAsync(user, request.Password);
        // Same accepted response for duplicates; never expose whether an email is registered.
        if (!created.Succeeded)
        {
            if (created.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName")) return;
            throw new AppException(400, "No se pudo registrar. Revisa los campos y los requisitos de contraseña.");
        }
        if (request.RequestedRole == RoleNames.Student)
            Ensure(await users.AddToRoleAsync(user, RoleNames.Student));
        await transaction.CommitAsync();
    }

    public async Task<bool> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Perform a password hash verification even for unknown accounts.
            _ = users.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyPasswordHash.Value, request.Password);
            return false;
        }
        if (!user.IsActive || user.OrganizationId != PilotOrganization.Id) return false;
        var result = await signIn.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
        return result.Succeeded;
    }

    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString()));
    public async Task LogoutAsync()
    {
        var user = await FindAsync((await current.GetAsync()).Id);
        Ensure(await users.UpdateSecurityStampAsync(user));
        await signIn.SignOutAsync();
    }
    public async Task<UserView> MeAsync() => await ViewAsync(await FindAsync((await current.GetAsync()).Id));

    public async Task<UserView> GetUserAsync(Guid id)
    {
        var actor = await current.GetAsync();
        // Check owner/admin before loading the resource to avoid enumeration.
        if (!AccessRules.CanReadUser(actor, id, actor.OrganizationId)) throw Hidden();
        return await ViewAsync(await FindAsync(id));
    }

    public async Task<PageResult<UserView>> ListUsersAsync(int page, string? email = null)
    {
        await RequireAdminAsync();
        if (page < 1 || page > 10000) throw new AppException(400, "Página no válida.");
        if (email?.Length > 254) throw new AppException(400, "Búsqueda demasiado larga.");
        var query = db.Users.Where(x => x.OrganizationId == PilotOrganization.Id);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = users.NormalizeEmail(email.Trim())!;
            query = query.Where(x => x.NormalizedEmail != null && x.NormalizedEmail.Contains(normalized));
        }
        var pageUsers = await query
            .OrderBy(x => x.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync();
        var result = new List<UserView>();
        foreach (var user in pageUsers) result.Add(await ViewAsync(user));
        return new(result.ToArray(), page, 50);
    }

    public async Task DecideTeacherAsync(Guid id, bool approve)
    {
        await RequireAdminAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await AdminBootstrap.LockAsync(db);
        var user = await FindAsync(id);
        if (user.TeacherApproval == TeacherApproval.NotRequested)
            throw new AppException(409, "La cuenta no tiene solicitud docente.");
        user.TeacherApproval = approve ? TeacherApproval.Approved : TeacherApproval.Rejected;
        Ensure(await users.UpdateAsync(user));
        if (approve && !await users.IsInRoleAsync(user, RoleNames.Teacher))
            Ensure(await users.AddToRoleAsync(user, RoleNames.Teacher));
        if (!approve && await users.IsInRoleAsync(user, RoleNames.Teacher))
            Ensure(await users.RemoveFromRoleAsync(user, RoleNames.Teacher));
        Ensure(await users.UpdateSecurityStampAsync(user));
        await tx.CommitAsync();
    }

    public async Task ChangeRolesAsync(Guid id, string[] roles)
    {
        var actor = await RequireAdminAsync();
        if (roles is null || roles.Length > 3 || roles.Distinct().Count() != roles.Length || roles.Any(x => !RoleNames.All.Contains(x)))
            throw new AppException(400, "Roles no válidos.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await AdminBootstrap.LockAsync(db);
        var user = await FindAsync(id);
        if (roles.Contains(RoleNames.Teacher) && user.TeacherApproval != TeacherApproval.Approved)
            throw new AppException(409, "Aprueba primero la solicitud docente.");
        var existing = await users.GetRolesAsync(user);
        if (existing.Contains(RoleNames.Admin) && !roles.Contains(RoleNames.Admin))
            await ProtectAdminAsync(user, actor);
        Ensure(await users.RemoveFromRolesAsync(user, existing.Except(roles)));
        Ensure(await users.AddToRolesAsync(user, roles.Except(existing)));
        Ensure(await users.UpdateSecurityStampAsync(user));
        await tx.CommitAsync();
    }

    public async Task ChangeStatusAsync(Guid id, bool isActive)
    {
        var actor = await RequireAdminAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await AdminBootstrap.LockAsync(db);
        var user = await FindAsync(id);
        if (!isActive && await users.IsInRoleAsync(user, RoleNames.Admin)) await ProtectAdminAsync(user, actor);
        user.IsActive = isActive;
        Ensure(await users.UpdateAsync(user));
        Ensure(await users.UpdateSecurityStampAsync(user));
        await tx.CommitAsync();
    }

    private async Task ProtectAdminAsync(AppUser user, Actor actor)
    {
        // Do not let administrators remove their own access or the last active admin.
        var admins = await users.GetUsersInRoleAsync(RoleNames.Admin);
        if (user.Id == actor.Id || admins.Count(x => x.IsActive && x.OrganizationId == PilotOrganization.Id) <= 1)
            throw new AppException(409, "No puedes retirar tu propio acceso ni el del último administrador activo.");
    }

    private async Task<Actor> RequireAdminAsync()
    {
        var actor = await current.GetAsync();
        if (!actor.IsAdmin) throw new AppException(403, "Acceso administrativo requerido.");
        return actor;
    }
    private async Task<AppUser> FindAsync(Guid id) => await db.Users.SingleOrDefaultAsync(
        x => x.Id == id && x.OrganizationId == PilotOrganization.Id) ?? throw Hidden();
    private async Task<UserView> ViewAsync(AppUser user) => new(user.Id, user.DisplayName, user.Email!,
        user.OrganizationId, (await users.GetRolesAsync(user)).ToArray(), user.TeacherApproval.ToString(), user.IsActive);
    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new AppException(409, "No se pudo guardar el cambio. Actualiza e inténtalo nuevamente.");
    }
    internal static AppException Hidden() => new(404, "Recurso no disponible.");
}
