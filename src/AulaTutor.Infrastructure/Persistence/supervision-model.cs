using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class SupervisionModel
{
    public static void ConfigureSupervision(this ModelBuilder b)
    {
        b.Entity<ExerciseAttempt>().HasAlternateKey(x => new { x.Id, x.SessionId });
        b.Entity<TeacherReview>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LearningExercise>().WithMany().HasForeignKey(x => new { x.ExerciseId, x.SessionId })
                .HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.TargetId }).IsUnique();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t => t.HasCheckConstraint("CK_Review_Target", "\"Revision\" > 0 AND ((\"ExerciseId\" IS NULL AND \"TargetId\" = \"SessionId\") OR (\"ExerciseId\" IS NOT NULL AND \"TargetId\" = \"ExerciseId\"))"));
        });
        b.Entity<TeacherReviewEntry>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TeacherReview>().WithMany().HasForeignKey(x => new { x.ReviewId, x.SessionId })
                .HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ReviewId, x.Revision }).IsUnique();
            e.HasIndex(x => new { x.SessionId, x.RequestId }).IsUnique();
            e.Property(x => x.InputHash).HasMaxLength(64);
            e.Property(x => x.Comment).HasMaxLength(1000); e.Property(x => x.Observation).HasMaxLength(1000); e.Property(x => x.Recommendation).HasMaxLength(1000);
            e.ToTable(t => t.HasCheckConstraint("CK_ReviewEntry_State", "\"Revision\" > 0 AND \"EvidenceRevision\" >= 0 AND \"State\" BETWEEN 0 AND 3 AND \"Attention\" BETWEEN 0 AND 2 AND \"Outcome\" BETWEEN 0 AND 3"));
        });
        b.Entity<ReviewEvidence>(e =>
        {
            e.HasKey(x => new { x.EntryId, x.AttemptId });
            e.HasOne<TeacherReviewEntry>().WithMany().HasForeignKey(x => new { x.EntryId, x.SessionId })
                .HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ExerciseAttempt>().WithMany().HasForeignKey(x => new { Id = x.AttemptId, x.SessionId })
                .HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
