using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

public static class PersonalAiModelConfiguration
{
    public static void ConfigurePersonalAi(this ModelBuilder b)
    {
        b.Entity<PersonalAiConnection>(e => {
            e.HasAlternateKey(x => new { x.Id, x.OwnerId, x.OrganizationId });
            e.HasIndex(x => new { x.OwnerId, x.Provider }).IsUnique();
            e.Property(x => x.Provider).HasMaxLength(16); e.Property(x => x.Status).HasMaxLength(24);
            e.Property(x => x.Method).HasMaxLength(16); e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.ProtectedCredential).HasMaxLength(16000); e.Property(x => x.PreferredModel).HasMaxLength(120);
            e.Property(x => x.ExternalAccount).HasMaxLength(128); e.Property(x => x.Workspace).HasMaxLength(100);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OwnerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => { t.HasCheckConstraint("CK_PersonalConnection_Provider", "\"Provider\" IN ('OpenRouter','Groq','Gemini','Anthropic')");
                t.HasCheckConstraint("CK_PersonalConnection_State", "\"Status\" IN ('Connected','Invalid','Disconnected') AND \"CredentialRevision\" > 0 AND \"Method\" IN ('Manual','OAuth')"); });
        });
        b.Entity<PersonalAiSelection>(e => {
            e.HasKey(x => x.OwnerId); e.Property(x => x.Mode).HasMaxLength(16); e.Property(x => x.Model).HasMaxLength(120);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OwnerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PersonalAiConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_PersonalSelection_Mode", "(\"Mode\" = 'Personal' AND \"ConnectionId\" IS NOT NULL AND \"Model\" IS NOT NULL) OR (\"Mode\" IN ('Institutional','None') AND \"ConnectionId\" IS NULL AND \"Model\" IS NULL)"));
        });
        b.Entity<AiInferenceBinding>(e => {
            e.HasAlternateKey(x => new { x.Id, x.OwnerId, x.OrganizationId });
            e.Property(x => x.Provider).HasMaxLength(16); e.Property(x => x.Model).HasMaxLength(120);
            e.HasOne<PersonalAiConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.OwnerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PersonalAiOAuth>(e => {
            e.HasIndex(x => x.StateHash).IsUnique(); e.HasIndex(x => new { x.OwnerId, x.ExpiresAt }); e.HasIndex(x => x.BrowserHash);
            foreach(var p in new[] { nameof(PersonalAiOAuth.StateHash), nameof(PersonalAiOAuth.BrowserHash), nameof(PersonalAiOAuth.SessionHash), nameof(PersonalAiOAuth.StampHash) }) e.Property<string>(p).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(24); e.Property(x => x.ProtectedVerifier).HasMaxLength(16000); e.Property(x => x.ProtectedCode).HasMaxLength(16000);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OwnerId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_PersonalOAuth_State", "\"Status\" IN ('Pending','Returned','Exchanging','Succeeded','Failed','Canceled','Expired')"));
        });
        b.Entity<PersonalAiModel>(e => {
            e.HasKey(x => new { x.Provider, x.Id }); e.Property(x => x.Provider).HasMaxLength(16); e.Property(x => x.Id).HasMaxLength(120);
            e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Availability).HasMaxLength(24);
        });
        b.Entity<TutoringSession>().HasOne<AiInferenceBinding>().WithMany().HasForeignKey(x => new { x.AiBindingId, x.StudentId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LessonOperation>().HasOne<AiInferenceBinding>().WithMany().HasForeignKey(x => x.AiBindingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LessonSimulation>().HasOne<AiInferenceBinding>().WithMany().HasForeignKey(x => x.AiBindingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiReservation>(e => {
            e.Property(x => x.Scope).HasMaxLength(16); e.Property(x => x.CredentialFingerprint).HasMaxLength(64);
            e.Property(x => x.ExternalAccount).HasMaxLength(128); e.Property(x => x.Operation).HasMaxLength(80);
            e.HasOne<PersonalAiConnection>().WithMany().HasForeignKey(x => new { x.ConnectionId, x.ActorId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Scope, x.Provider, x.CreatedAt });
        });
    }
}
