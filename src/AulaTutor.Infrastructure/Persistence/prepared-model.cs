using AulaTutor.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class PreparedModel
{
    public static void ConfigurePrepared(this ModelBuilder b)
    {
        b.Entity<TutoringSession>(e =>
        {
            e.Property(x => x.LearningExperience).HasMaxLength(20).HasDefaultValue("Tutoring");
            e.Property(x => x.LearningModality).HasMaxLength(20).HasDefaultValue("Conversational");
            e.HasOne<PersonalizedLearningPlan>().WithMany().HasForeignKey(x => x.SourcePlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SourcePlanId, x.SourcePlanObjectiveOrder }).IsUnique().HasFilter("\"SourcePlanId\" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("CK_Session_Learning", "\"LearningExperience\" IN ('Tutoring','Diagnostic') AND \"LearningModality\" IN ('Conversational','Prepared') AND (\"LearningModality\" <> 'Prepared' OR \"Mode\" = 0)"));
        });
        b.Entity<PreparedLearning>(e =>
        {
            e.HasKey(x => x.SessionId);
            e.HasOne<TutoringSession>().WithOne().HasForeignKey<PreparedLearning>(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Length).HasMaxLength(10); e.Property(x => x.ContractVersion).HasMaxLength(40);
            e.Property(x => x.ContentJson).HasMaxLength(64000); e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.EvaluationJson).HasMaxLength(48000); e.Property(x => x.EvaluationEvidenceHash).HasMaxLength(64);
            e.ToTable(t => t.HasCheckConstraint("CK_Prepared_Limits", "\"Length\" IN ('Short','Medium','Long') AND \"CurrentActivity\" >= 0 AND \"ContextWindow\" >= 0 AND \"MaxOutputTokens\" >= 0 AND ((\"ContentJson\" IS NULL AND \"ContentHash\" IS NULL) OR (\"ContentJson\" IS NOT NULL AND length(\"ContentHash\") = 64))"));
        });
    }
}
