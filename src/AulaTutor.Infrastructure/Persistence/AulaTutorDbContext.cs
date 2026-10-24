using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Persistence;

public sealed class AulaTutorDbContext(DbContextOptions<AulaTutorDbContext> options, Microsoft.AspNetCore.Http.IHttpContextAccessor? accessor = null)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    internal string? RequestSecurityStamp => accessor?.HttpContext?.User.FindFirst("AspNet.Identity.SecurityStamp")?.Value;
    public DbSet<AiReservation> AiReservations => Set<AiReservation>();
    public DbSet<PersonalAiConnection> PersonalAiConnections => Set<PersonalAiConnection>();
    public DbSet<PersonalAiSelection> PersonalAiSelections => Set<PersonalAiSelection>();
    public DbSet<AiInferenceBinding> AiInferenceBindings => Set<AiInferenceBinding>();
    public DbSet<PersonalAiOAuth> PersonalAiOAuths => Set<PersonalAiOAuth>();
    public DbSet<PersonalAiModel> PersonalAiModels => Set<PersonalAiModel>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var events = OperationalModel.Capture(this, accessor);
        AuditEvents.AddRange(events);
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch { foreach (var item in events) Entry(item).State = EntityState.Detached; throw; }
    }
    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var events = OperationalModel.Capture(this, accessor); AuditEvents.AddRange(events);
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch { foreach (var item in events) Entry(item).State = EntityState.Detached; throw; }
    }
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AcademicGroup> Groups => Set<AcademicGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<TutoringSession> TutoringSessions => Set<TutoringSession>();
    public DbSet<TutoringTurn> TutoringTurns => Set<TutoringTurn>();
    public DbSet<TutoringMessage> TutoringMessages => Set<TutoringMessage>();
    public DbSet<LearningExercise> LearningExercises => Set<LearningExercise>();
    public DbSet<ExerciseAttempt> ExerciseAttempts => Set<ExerciseAttempt>();
    public DbSet<DiagnosticAssessment> DiagnosticAssessments => Set<DiagnosticAssessment>();
    public DbSet<PersonalizedLearningPlan> LearningPlans => Set<PersonalizedLearningPlan>();
    public DbSet<LearningReport> LearningReports => Set<LearningReport>();
    public DbSet<LearningRecommendation> LearningRecommendations => Set<LearningRecommendation>();
    public DbSet<ReportShare> ReportShares => Set<ReportShare>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonDraft> LessonDrafts => Set<LessonDraft>();
    public DbSet<LessonDraftRevision> LessonDraftRevisions => Set<LessonDraftRevision>();
    public DbSet<LessonVersion> LessonVersions => Set<LessonVersion>();
    public DbSet<LessonPublication> LessonPublications => Set<LessonPublication>();
    public DbSet<LessonProposal> LessonProposals => Set<LessonProposal>();
    public DbSet<LessonSimulation> LessonSimulations => Set<LessonSimulation>();
    public DbSet<LessonOperation> LessonOperations => Set<LessonOperation>();
    public DbSet<LessonCommand> LessonCommands => Set<LessonCommand>();
    public DbSet<LessonAssignment> LessonAssignments => Set<LessonAssignment>();
    public DbSet<AssignmentChange> AssignmentChanges => Set<AssignmentChange>();
    public DbSet<TeacherReview> TeacherReviews => Set<TeacherReview>();
    public DbSet<TeacherReviewEntry> TeacherReviewEntries => Set<TeacherReviewEntry>();
    public DbSet<ReviewEvidence> ReviewEvidence => Set<ReviewEvidence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureOperations();
        builder.ConfigurePersonalAi();
        builder.ConfigureTutoring();
        builder.ConfigureLearning();
        builder.ConfigureLessons();
        builder.ConfigureAssignments();
        builder.ConfigureSupervision();
        builder.Entity<Organization>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.HasData(new Organization { Id = PilotOrganization.Id, Name = "Organización piloto" });
        });
        builder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.HasAlternateKey(x => new { x.Id, x.OrganizationId });
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AcademicGroup>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.HasAlternateKey(x => new { x.Id, x.OrganizationId });
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<GroupMembership>(entity =>
        {
            entity.HasKey(x => new { x.GroupId, x.UserId, x.Kind });
            entity.HasOne<AcademicGroup>().WithMany()
                .HasForeignKey(x => new { x.GroupId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany()
                .HasForeignKey(x => new { x.UserId, x.OrganizationId })
                .HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_GroupMembership_Kind", "\"Kind\" IN (0, 1)"));
        });
        var roleIds = new[] { "3fb13147-0f6d-45b0-b9f9-7a3651527001", "3fb13147-0f6d-45b0-b9f9-7a3651527002", "3fb13147-0f6d-45b0-b9f9-7a3651527003" };
        for (var i = 0; i < RoleNames.All.Length; i++)
        {
            builder.Entity<IdentityRole<Guid>>().HasData(new IdentityRole<Guid>
            {
                Id = Guid.Parse(roleIds[i]), Name = RoleNames.All[i],
                NormalizedName = RoleNames.All[i].ToUpperInvariant(), ConcurrencyStamp = roleIds[i]
            });
        }
    }
}
