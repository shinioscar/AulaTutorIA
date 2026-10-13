using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class LearningModel
{
    public static void ConfigureLearning(this ModelBuilder b)
    {
        b.Entity<TutoringSession>().HasAlternateKey(x => new { x.Id, x.StudentId });
        b.Entity<LearningExercise>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PersonalizedLearningPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.SessionId }).HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Prompt).HasMaxLength(1500); e.Property(x => x.Rubric).HasMaxLength(600);
            e.Property(x => x.OptionsJson).HasMaxLength(2000); e.Property(x => x.AcceptedAnswersJson).HasMaxLength(2000);
            e.HasIndex(x => new { x.SessionId, x.PlanId, x.Order }).IsUnique().HasFilter("\"PlanId\" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("CK_Exercise_Limits", "\"Version\" > 0 AND \"HintCount\" >= 0 AND \"Kind\" BETWEEN 0 AND 4"));
        });
        b.Entity<ExerciseAttempt>(e =>
        {
            e.HasOne<LearningExercise>().WithMany().HasForeignKey(x => new { x.ExerciseId, x.SessionId }).HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TutoringTurn>().WithMany().HasForeignKey(x => new { x.SessionId, x.RequestId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.StudentId }).HasPrincipalKey(x => new { x.Id, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.RequestId }).IsUnique();
            e.HasIndex(x => new { x.ExerciseId, x.Number }).IsUnique();
            e.Property(x => x.Answer).HasMaxLength(2000); e.Property(x => x.Feedback).HasMaxLength(3000);
            e.Property(x => x.ValidationType).HasMaxLength(40);
            e.ToTable(t => t.HasCheckConstraint("CK_Attempt_Limits", "\"Number\" BETWEEN 1 AND 10 AND \"HintsUsed\" >= 0 AND \"Outcome\" BETWEEN 0 AND 4"));
        });
        b.Entity<DiagnosticAssessment>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.Version }).IsUnique(); e.Property(x => x.ContentJson).HasMaxLength(12000);
        });
        b.Entity<PersonalizedLearningPlan>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.StudentId }).HasPrincipalKey(x => new { x.Id, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DiagnosticAssessment>().WithMany().HasForeignKey(x => new { x.DiagnosticId, x.SessionId }).HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.Version }).IsUnique(); e.Property(x => x.ContentJson).HasMaxLength(24000);
            e.ToTable(t => t.HasCheckConstraint("CK_Plan_State", "\"State\" BETWEEN 0 AND 3 AND \"CurrentActivity\" >= 0"));
        });
        b.Entity<LearningReport>(e =>
        {
            e.HasAlternateKey(x => new { x.Id, x.SessionId });
            e.HasOne<TutoringSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.Version }).IsUnique(); e.Property(x => x.ContentJson).HasMaxLength(12000);
            e.Property(x => x.StatisticsJson).HasMaxLength(2000);
        });
        b.Entity<LearningRecommendation>(e =>
        {
            e.HasOne<LearningReport>().WithMany().HasForeignKey(x => new { x.ReportId, x.SessionId }).HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Goal).HasMaxLength(500); e.Property(x => x.EvidenceIdsJson).HasMaxLength(2000);
        });
        b.Entity<ReportShare>(e =>
        {
            e.HasOne<LearningReport>().WithMany().HasForeignKey(x => new { x.ReportId, x.SessionId }).HasPrincipalKey(x => new { x.Id, x.SessionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.RequestId }).IsUnique(); e.Property(x => x.ConsentVersion).HasMaxLength(40);
        });
        b.Entity<TutoringSession>().HasOne<LearningRecommendation>().WithMany().HasForeignKey(x => x.SourceRecommendationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TutoringSession>().HasOne<LearningReport>().WithMany().HasForeignKey(x => x.SourceReportId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TutoringTurn>().Property(x => x.ContextJson).HasMaxLength(28000);
        b.Entity<TutoringTurn>().Property(x => x.RequestText).HasMaxLength(2000);
    }
}
