using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Lessons;

// One resource policy shared by session, evidence, report and assignment endpoints.
public sealed class AssignedClassAccess(AulaTutorDbContext db, TimeProvider clock)
{
    public IQueryable<LessonAssignment> ForStudent(Guid studentId, Guid organizationId) => db.LessonAssignments.Where(a =>
        a.OrganizationId == organizationId &&
        db.Users.Any(u => u.Id == studentId && u.OrganizationId == organizationId && u.IsActive) &&
        db.UserRoles.Any(ur => ur.UserId == studentId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Student)) &&
        db.Groups.Any(g => g.Id == a.GroupId && g.OrganizationId == organizationId && !g.IsArchived) &&
        db.GroupMemberships.Any(m => m.GroupId == a.GroupId && m.UserId == studentId && m.OrganizationId == organizationId && m.Kind == GroupMemberKind.Student) &&
        db.GroupMemberships.Any(m => m.GroupId == a.GroupId && m.UserId == a.TeacherId && m.OrganizationId == organizationId && m.Kind == GroupMemberKind.Teacher) &&
        db.Users.Any(u => u.Id == a.TeacherId && u.OrganizationId == organizationId && u.IsActive && u.TeacherApproval == TeacherApproval.Approved) &&
        db.UserRoles.Any(ur => ur.UserId == a.TeacherId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Teacher)));
    public async Task RequireReadAsync(TutoringSession s, CancellationToken ct)
    {
        if (s.Mode == TutoringMode.AssignedClass && !await ForStudent(s.StudentId, s.OrganizationId).AnyAsync(a => a.Id == s.AssignmentId, ct))
            throw new AppException(404, "La clase ya no está disponible para tu cuenta.");
    }
    public bool Available(LessonAssignment a, LessonPublication p) => a.State == AssignmentState.Active && p.ArchivedAt == null &&
        (!a.AvailableFrom.HasValue || a.AvailableFrom <= clock.GetUtcNow()) && (!a.AvailableUntil.HasValue || a.AvailableUntil > clock.GetUtcNow());
    public async Task<bool> CanWorkAsync(TutoringSession s, CancellationToken ct)
    {
        if (s.Mode != TutoringMode.AssignedClass) return true;
        var a = await ForStudent(s.StudentId, s.OrganizationId).AsNoTracking().SingleOrDefaultAsync(a => a.Id == s.AssignmentId, ct);
        if (a is null) return false;
        var p = await db.LessonPublications.AsNoTracking().SingleAsync(p => p.VersionId == a.LessonVersionId, ct);
        return Available(a, p);
    }
    public async Task RequireWorkAsync(TutoringSession s, CancellationToken ct)
    {
        if (!await CanWorkAsync(s, ct)) throw new AppException(409, "La asignación no está disponible para continuar. Tu progreso y evidencias se conservan.");
    }
}
