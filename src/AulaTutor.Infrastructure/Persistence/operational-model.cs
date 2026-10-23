using System.Security.Claims;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

public static class OperationalModel
{
    public static void ConfigureOperations(this ModelBuilder b)
    {
        b.Entity<AiReservation>(e =>
        {
            e.Property(x => x.Provider).HasMaxLength(16); e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.CorrelationId).HasMaxLength(64); e.Property(x => x.State).HasMaxLength(32);
            e.HasIndex(x => new { x.Provider, x.CreatedAt }); e.HasIndex(x => new { x.ActorId, x.CreatedAt });
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt }); e.HasIndex(x => new { x.GroupId, x.CreatedAt });
            e.HasIndex(x => x.LeaseUntil);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.ActorId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AcademicGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("AiReservations", t => {
                t.HasCheckConstraint("CK_AiReservation_State", "\"State\" IN ('Reserved','Succeeded','Failed','Uncertain')");
                t.HasCheckConstraint("CK_AiReservation_Usage", "\"EstimatedTokens\" >= 0 AND (\"InputTokens\" IS NULL OR \"InputTokens\" >= 0) AND (\"OutputTokens\" IS NULL OR \"OutputTokens\" >= 0)");
            });
        });
        b.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(64); e.Property(x => x.ResourceId).HasMaxLength(200);
            e.Property(x => x.Result).HasMaxLength(32); e.Property(x => x.CorrelationId).HasMaxLength(64); e.Property(x => x.Change).HasMaxLength(16);
            e.HasIndex(x => new { x.CreatedAt, x.Id }); e.HasIndex(x => new { x.ActorId, x.CreatedAt });
            e.ToTable("AuditEvents");
        });
    }

    public static AuditEvent[] Capture(AulaTutorDbContext db, IHttpContextAccessor? accessor)
    {
        db.ChangeTracker.DetectChanges();
        var http = accessor?.HttpContext;
        Guid? actor = Guid.TryParse(http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var correlation = http?.TraceIdentifier;
        if (correlation is null || !Guid.TryParseExact(correlation, "N", out _)) correlation = Guid.NewGuid().ToString("N");
        return db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is GroupMembership or AcademicGroup or LessonPublication or LessonAssignment or AssignmentChange or ReportShare or TeacherReviewEntry or IdentityUserRole<Guid> ||
                e.Entity is AppUser && (e.State == EntityState.Added || e.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue) && p.Metadata.Name is "IsActive" or "TeacherApproval" or "SecurityStamp")))
            .Select(e => new AuditEvent { ActorId = actor, CreatedAt = DateTimeOffset.UtcNow,
                Type = e.Entity switch {
                    AppUser when e.State == EntityState.Added => "AccountCreated",
                    AppUser when !Equals(e.Property("TeacherApproval").OriginalValue, e.Property("TeacherApproval").CurrentValue) => "TeacherApprovalChanged",
                    AppUser when !Equals(e.Property("IsActive").OriginalValue, e.Property("IsActive").CurrentValue) => "AccountStatusChanged",
                    AppUser => "SessionsRevoked",
                    IdentityUserRole<Guid> => "RoleChanged", GroupMembership => "MembershipChanged", AcademicGroup => "GroupChanged",
                    LessonPublication => "LessonPublicationChanged", LessonAssignment => "LessonAssignmentChanged", AssignmentChange => "AssignmentStateChanged",
                    ReportShare => "ReportShareChanged", TeacherReviewEntry => "TeacherReviewRecorded", _ => "StateChanged"
                }, ResourceId = string.Join(":", e.Metadata.FindPrimaryKey()!.Properties.Select(p => e.Property(p.Name).CurrentValue)),
                CorrelationId = correlation, Change = e.State.ToString() }).ToArray();
    }
}
