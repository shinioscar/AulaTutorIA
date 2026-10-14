using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

internal static class LessonModel
{
    public static void ConfigureLessons(this ModelBuilder b)
    {
        b.Entity<Lesson>(e =>
        {
            e.HasKey(x => x.Id); e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => new { x.TeacherId, x.CreationKey }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.TeacherId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => { t.HasCheckConstraint("CK_Lesson_State", "\"State\" BETWEEN 0 AND 5"); t.HasCheckConstraint("CK_Lesson_Revision", "\"Revision\" >= 0"); });
        });
        b.Entity<LessonDraft>(e => { e.HasKey(x => x.LessonId); e.Property(x => x.ContentJson).HasColumnType("jsonb"); e.Property(x => x.Revision).IsConcurrencyToken(); e.HasOne<Lesson>().WithOne().HasForeignKey<LessonDraft>(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<LessonDraftRevision>(e => { e.HasKey(x => new { x.LessonId, x.Revision }); e.Property(x => x.ContentJson).HasColumnType("jsonb"); e.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<LessonVersion>(e =>
        {
            e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.LessonId }); e.HasIndex(x => new { x.LessonId, x.Number }).IsUnique(); e.HasIndex(x => new { x.LessonId, x.DraftRevision }).IsUnique();
            e.Property(x => x.ContentJson).HasColumnType("jsonb"); e.Property(x => x.ContentHash).HasMaxLength(64);
            e.HasOne<LessonDraftRevision>().WithMany().HasForeignKey(x => new { x.LessonId, Revision = x.DraftRevision }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LessonPublication>(e => { e.HasKey(x => x.VersionId); e.HasOne<LessonVersion>().WithOne().HasForeignKey<LessonPublication>(x => new { x.VersionId, x.LessonId }).HasPrincipalKey<LessonVersion>(x => new { x.Id, x.LessonId }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<LessonProposal>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.LessonId, x.RequestId }).IsUnique(); e.Property(x => x.ContentJson).HasColumnType("jsonb"); e.Property(x => x.Explanation).HasMaxLength(1000); e.HasOne<LessonDraftRevision>().WithMany().HasForeignKey(x => new { x.LessonId, Revision = x.DraftRevision }).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<LessonSimulation>(e =>
        {
            e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.LessonId }); e.Property(x => x.Revision).IsConcurrencyToken(); e.Property(x => x.ContentJson).HasColumnType("jsonb");
            e.Property(x => x.State).HasMaxLength(20); e.Property(x => x.ProviderId).HasMaxLength(40); e.Property(x => x.ModelId).HasMaxLength(200);
            e.HasOne<LessonDraftRevision>().WithMany().HasForeignKey(x => new { x.LessonId, Revision = x.DraftRevision }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_LessonSimulation_State", "\"State\" IN ('Active','Stopped')"));
        });
        b.Entity<LessonOperation>(e =>
        {
            e.HasKey(x => new { x.LessonId, x.RequestId }); e.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LessonSimulation>().WithMany().HasForeignKey(x => new { Id = x.SimulationId, x.LessonId }).HasPrincipalKey(x => new { x.Id, x.LessonId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ContextJson).HasColumnType("jsonb"); e.Property(x => x.Action).HasMaxLength(40); e.Property(x => x.Text).HasMaxLength(2000); e.Property(x => x.InputHash).HasMaxLength(64);
            e.Property(x => x.ProviderId).HasMaxLength(40); e.Property(x => x.ModelId).HasMaxLength(200); e.Property(x => x.ErrorCode).HasMaxLength(40); e.Property(x => x.Reply).HasMaxLength(6000);
            e.Property(x => x.Outcome).HasMaxLength(40); e.Property(x => x.ValidationType).HasMaxLength(40);
            e.ToTable(t => { t.HasCheckConstraint("CK_LessonOperation_State", "\"State\" BETWEEN 0 AND 3"); t.HasCheckConstraint("CK_LessonOperation_Attempts", "\"Attempts\" BETWEEN 1 AND 3"); });
        });
        b.Entity<LessonCommand>(e => { e.HasKey(x => new { x.LessonId, x.RequestId }); e.Property(x => x.InputHash).HasMaxLength(64); e.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); });
    }
}
