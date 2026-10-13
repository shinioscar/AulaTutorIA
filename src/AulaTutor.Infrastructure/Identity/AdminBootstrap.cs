using System.ComponentModel.DataAnnotations;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AulaTutor.Infrastructure.Identity;

public sealed class AdminBootstrap(AulaTutorDbContext db, UserManager<AppUser> users)
{
    // Shared transactional lock: bootstrap, role changes and last-admin protection.
    internal static Task LockAsync(AulaTutorDbContext db) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(841901)");

    public async Task<bool> CreateAsync(string email, string password, string displayName)
    {
        if (!new EmailAddressAttribute().IsValid(email) || email.Length > 254 ||
            string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length is < 2 or > 100 ||
            string.IsNullOrWhiteSpace(password) || password.Length is < 5 or > 128)
            throw new InvalidOperationException("Bootstrap requiere email, nombre y contraseña válidos mediante configuración privada.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockAsync(db);
        var existing = await users.FindByEmailAsync(email.Trim());
        var admins = await users.GetUsersInRoleAsync(RoleNames.Admin);
        if (admins.Any())
        {
            if (existing is null || !existing.IsActive || !await users.IsInRoleAsync(existing, RoleNames.Admin))
                throw new InvalidOperationException("Bootstrap ya inicializado para otra cuenta; usa la gestión administrativa.");
            return false; // Never reset an existing password or promote a public registration.
        }
        if (existing is not null) throw new InvalidOperationException("Bootstrap no puede promover una cuenta registrada existente.");
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email.Trim(), Email = email.Trim(),
            DisplayName = displayName.Trim(), LockoutEnabled = true };
        IdentityService.Ensure(await users.CreateAsync(user, password));
        IdentityService.Ensure(await users.AddToRoleAsync(user, RoleNames.Admin));
        await tx.CommitAsync();
        return true;
    }

    public Task<bool> FromConfigurationAsync(IConfiguration config) => CreateAsync(
        config["BootstrapAdmin:Email"] ?? "", config["BootstrapAdmin:Password"] ?? "",
        config["BootstrapAdmin:DisplayName"] ?? "");
}
