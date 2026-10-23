using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Identity;

public sealed class GroupService(AulaTutorDbContext db, ICurrentActor current, UserManager<AppUser> users) : IGroupService
{
    public async Task<GroupView[]> ListAsync()
    {
        var actor = await current.GetAsync();
        if (!actor.IsAdmin && !actor.IsTeacher && !actor.IsStudent) throw new AppException(403, "Cuenta pendiente de habilitación.");
        var query = db.Groups.Where(x => x.OrganizationId == actor.OrganizationId);
        if (!actor.IsAdmin)
            query = query.Where(g => !g.IsArchived && db.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == actor.Id &&
                ((actor.IsTeacher && m.Kind == GroupMemberKind.Teacher) || (actor.IsStudent && m.Kind == GroupMemberKind.Student))));
        return await query.OrderBy(x => x.Name).Select(g => new GroupView(g.Id, g.Name, g.IsArchived,
            db.GroupMemberships.Count(m => m.GroupId == g.Id && m.Kind == GroupMemberKind.Student),
            db.GroupMemberships.Count(m => m.GroupId == g.Id && m.Kind == GroupMemberKind.Teacher),
            actor.IsAdmin || (actor.IsTeacher && db.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == actor.Id && m.Kind == GroupMemberKind.Teacher)))).ToArrayAsync();
    }

    public async Task<GroupView> GetAsync(Guid id)
    {
        var group = await ReadableAsync(id);
        var actor = await current.GetAsync();
        return new(group.Id, group.Name, group.IsArchived,
            await db.GroupMemberships.CountAsync(m => m.GroupId == id && m.Kind == GroupMemberKind.Student),
            await db.GroupMemberships.CountAsync(m => m.GroupId == id && m.Kind == GroupMemberKind.Teacher),
            actor.IsAdmin || (actor.IsTeacher && await db.GroupMemberships.AnyAsync(m => m.GroupId == id && m.UserId == actor.Id && m.Kind == GroupMemberKind.Teacher)));
    }

    public async Task<GroupView> CreateAsync(string name)
    {
        await AdminAsync();
        var group = new AcademicGroup { Name = CleanName(name) };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return new(group.Id, group.Name, false, 0, 0, true);
    }
    public async Task RenameAsync(Guid id, string name)
    {
        await AdminAsync();
        var group = await FindAsync(id);
        group.Name = CleanName(name);
        await db.SaveChangesAsync();
    }
    public async Task ArchiveAsync(Guid id)
    {
        await AdminAsync();
        (await FindAsync(id)).IsArchived = true;
        await db.SaveChangesAsync();
    }
    public async Task<MembershipView[]> MembersAsync(Guid id)
    {
        await ReadableAsync(id);
        var actor = await current.GetAsync();
        var assigned = await db.GroupMemberships.AnyAsync(m => m.GroupId == id && m.UserId == actor.Id && m.Kind == GroupMemberKind.Teacher);
        if (!actor.IsAdmin && !(actor.IsTeacher && assigned)) throw IdentityService.Hidden();
        return await (from membership in db.GroupMemberships
                      join user in db.Users on membership.UserId equals user.Id
                      where membership.GroupId == id && user.OrganizationId == actor.OrganizationId
                      orderby user.DisplayName
                      select new MembershipView(user.Id, user.DisplayName, membership.Kind.ToString())).ToArrayAsync();
    }
    public async Task AddMemberAsync(Guid id, MembershipRequest request)
    {
        await AdminAsync();
        if (!Enum.TryParse<GroupMemberKind>(request.Kind, out var kind) || !Enum.IsDefined(kind))
            throw new AppException(400, "Tipo de membresía no válido.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await AdminBootstrap.LockAsync(db);
        await AdminAsync();
        var group = await FindAsync(id);
        if (group.IsArchived) throw new AppException(409, "El grupo está archivado.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId && x.OrganizationId == group.OrganizationId);
        if (user is null || !user.IsActive) throw IdentityService.Hidden();
        if ((kind == GroupMemberKind.Teacher && (user.TeacherApproval != TeacherApproval.Approved || !await users.IsInRoleAsync(user, RoleNames.Teacher))) ||
            (kind == GroupMemberKind.Student && !await users.IsInRoleAsync(user, RoleNames.Student)))
            throw new AppException(409, "El usuario no tiene el rol aprobado para esa membresía.");
        if (!await db.GroupMemberships.AnyAsync(m => m.GroupId == id && m.UserId == user.Id && m.Kind == kind))
        {
            db.GroupMemberships.Add(new GroupMembership { GroupId = id, UserId = user.Id, Kind = kind, OrganizationId = group.OrganizationId });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }
    public async Task RemoveMemberAsync(Guid id, Guid userId, string kind)
    {
        await AdminAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await AdminBootstrap.LockAsync(db);
        await AdminAsync();
        await FindAsync(id);
        if (!Enum.TryParse<GroupMemberKind>(kind, out var parsed) || !Enum.IsDefined(parsed)) throw new AppException(400, "Membresía no válida.");
        var member = await db.GroupMemberships.SingleOrDefaultAsync(m => m.GroupId == id && m.UserId == userId && m.Kind == parsed);
        if (member is not null)
        {
            db.GroupMemberships.Remove(member);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }
    private async Task<AcademicGroup> ReadableAsync(Guid id)
    {
        var actor = await current.GetAsync();
        if (!actor.IsAdmin && !actor.IsTeacher && !actor.IsStudent) throw new AppException(403, "Cuenta pendiente de habilitación.");
        var group = await FindAsync(id);
        var kinds = await db.GroupMemberships.Where(m => m.GroupId == id && m.UserId == actor.Id).Select(m => m.Kind).ToArrayAsync();
        if ((!actor.IsAdmin && group.IsArchived) || !AccessRules.CanReadGroup(actor, group.OrganizationId,
            kinds.Contains(GroupMemberKind.Teacher), kinds.Contains(GroupMemberKind.Student))) throw IdentityService.Hidden();
        return group;
    }
    private async Task AdminAsync()
    {
        var actor = await current.GetAsync();
        if (!actor.IsAdmin || !await db.Users.AnyAsync(u => u.Id == actor.Id && u.OrganizationId == actor.OrganizationId && u.IsActive &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Admin))))
            throw new AppException(403, "Acceso administrativo requerido.");
    }
    private async Task<AcademicGroup> FindAsync(Guid id) =>
        await db.Groups.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == PilotOrganization.Id) ?? throw IdentityService.Hidden();
    private static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 100) throw new AppException(400, "Nombre de grupo no válido.");
        return name.Trim();
    }
}
