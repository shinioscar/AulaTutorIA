using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class AssignmentModel
{
    public static void ConfigureAssignments(this ModelBuilder b)
    {
        b.Entity<Lesson>().HasAlternateKey(x => new { x.Id, x.TeacherId, x.OrganizationId });
        b.Entity<LessonAssignment>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.LessonVersionId, x.OrganizationId });
            e.HasOne<Lesson>().WithMany().HasForeignKey(x => new { Id = x.LessonId, x.TeacherId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.TeacherId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LessonPublication>().WithMany().HasForeignKey(x => x.LessonVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LessonVersion>().WithMany().HasForeignKey(x => new { Id = x.LessonVersionId, x.LessonId })
                .HasPrincipalKey(x => new { x.Id, x.LessonId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AcademicGroup>().WithMany().HasForeignKey(x => new { Id = x.GroupId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TeacherId, x.CreationKey }).IsUnique();
            e.HasIndex(x => new { x.GroupId, x.LessonVersionId }).IsUnique().HasFilter("\"State\" = 0");
            e.Property(x => x.CreationHash).HasMaxLength(64); e.Property(x => x.Revision).IsConcurrencyToken();
            e.ToTable(t => t.HasCheckConstraint("CK_Assignment_StateDates", "\"State\" BETWEEN 0 AND 2 AND \"Revision\" >= 0 AND (\"AvailableFrom\" IS NULL OR \"AvailableUntil\" IS NULL OR \"AvailableUntil\" > \"AvailableFrom\")"));
        });
        b.Entity<AssignmentChange>(e =>
        {
            e.HasKey(x => new { x.AssignmentId, x.RequestId });
            e.HasOne<LessonAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Action).HasMaxLength(20); e.Property(x => x.InputHash).HasMaxLength(64);
        });
        b.Entity<TutoringSession>(e =>
        {
            e.HasOne<LessonAssignment>().WithMany().HasForeignKey(x => new { Id = x.AssignmentId, x.LessonVersionId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.LessonVersionId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AssignmentId, x.StudentId }).IsUnique().HasFilter("\"AssignmentId\" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("CK_Session_ModeSource", "\"Mode\" BETWEEN 0 AND 2 AND \"AssignedActivity\" >= 0 AND ((\"Mode\" = 2 AND \"AssignmentId\" IS NOT NULL AND \"LessonVersionId\" IS NOT NULL) OR (\"Mode\" <> 2 AND \"AssignmentId\" IS NULL AND \"LessonVersionId\" IS NULL))"));
        });
        b.Entity<LearningExercise>(e =>
        {
            e.HasOne<LessonVersion>().WithMany().HasForeignKey(x => x.LessonVersionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ApprovedExerciseId).HasMaxLength(40); e.Property(x => x.ApprovedObjectiveId).HasMaxLength(40);
            e.HasIndex(x => new { x.SessionId, x.ApprovedExerciseId }).IsUnique().HasFilter("\"ApprovedExerciseId\" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("CK_Exercise_ApprovedSource", "(\"ApprovedExerciseId\" IS NULL AND \"ApprovedObjectiveId\" IS NULL AND \"LessonVersionId\" IS NULL AND NOT \"Required\") OR (\"ApprovedExerciseId\" IS NOT NULL AND \"ApprovedObjectiveId\" IS NOT NULL AND \"LessonVersionId\" IS NOT NULL AND NOT \"IsComplementary\" AND NOT \"IsDiagnostic\" AND \"PlanId\" IS NULL)"));
        });
    }
}
