using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class TutoringModel
{
    public static void ConfigureTutoring(this ModelBuilder builder)
    {
        builder.Entity<TutoringSession>(e =>
        {
            e.ToTable("TutoringSessions", t =>
            {
                t.HasCheckConstraint("CK_TutoringSession_State", "\"State\" BETWEEN 0 AND 3");
                t.HasCheckConstraint("CK_TutoringSession_Sequence", "\"NextSequence\" BETWEEN 0 AND 500 AND (\"LearningModality\" = 'Prepared' OR \"NextSequence\" <= 100)");
                t.HasCheckConstraint("CK_TutoringSession_Revision", "\"Revision\" >= 0");
            });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.StudentId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.StudentId, x.CreationKey }).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.LastActivityAt });
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.Topic).HasMaxLength(160); e.Property(x => x.Goal).HasMaxLength(500);
            e.Property(x => x.Level).HasMaxLength(20); e.Property(x => x.PolicyVersion).HasMaxLength(40);
            e.Property(x => x.ProviderId).HasMaxLength(40); e.Property(x => x.ModelId).HasMaxLength(120);
            e.Property(x => x.CurrentFocus).HasMaxLength(200); e.Property(x => x.Activity).HasMaxLength(30);
            e.Property(x => x.Summary).HasMaxLength(2000);
        });
        builder.Entity<TutoringTurn>(e =>
        {
            e.ToTable("TutoringTurns", t =>
            {
                t.HasCheckConstraint("CK_TutoringTurn_State", "\"State\" BETWEEN 0 AND 3");
                t.HasCheckConstraint("CK_TutoringTurn_Action", "\"Action\" BETWEEN 0 AND 12");
                t.HasCheckConstraint("CK_TutoringTurn_Attempts", "\"Attempts\" BETWEEN 1 AND 3");
            });
            e.HasKey(x => new { x.SessionId, x.RequestId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.InputHash).HasMaxLength(64); e.Property(x => x.ErrorCode).HasMaxLength(40);
        });
        builder.Entity<TutoringMessage>(e =>
        {
            e.ToTable("TutoringMessages", t => t.HasCheckConstraint("CK_TutoringMessage_Role", "\"Role\" IN (0, 1)"));
            e.HasOne<TutoringTurn>().WithMany().HasForeignKey(x => new { x.SessionId, x.RequestId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.SessionId, x.RequestId, x.Role }).IsUnique();
            e.Property(x => x.Text).HasMaxLength(6000); e.Property(x => x.ProviderId).HasMaxLength(40);
            e.Property(x => x.ModelId).HasMaxLength(120); e.Property(x => x.PromptVersion).HasMaxLength(40);
        });
    }
}
