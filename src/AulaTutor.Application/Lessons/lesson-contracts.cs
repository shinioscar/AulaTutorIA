using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public sealed record LessonObjective(string Id, string Text);
public sealed record LessonRubric(string[] Criteria, string ReferenceSolution, string HintRules);
public sealed record LessonExercise(string Id, string ObjectiveId, bool Required, ExerciseProposal Activity, LessonRubric Rubric);
public sealed record LessonSection(string Id, string Title, string Content, LessonExercise[] Exercises);
public sealed record LessonContent(string Title, string Subject, string Description, string Prerequisites,
    string Level, int DurationMinutes, string Methodology, string Instructions,
    LessonObjective[] Objectives, LessonSection[] Sections);
public sealed record LessonPlanProposal(string Explanation, LessonContent Content);
public sealed record CreateLessonRequest(Guid RequestId, LessonContent Content);
public sealed record SaveLessonRequest(long Revision, LessonContent Content);
public sealed record LessonCommandRequest(Guid RequestId, long Revision, Guid? TargetId = null);
public sealed record LessonAiRequest(Guid RequestId, long Revision, string Action, string? Text = null, bool Retry = false);
public sealed record LessonItem(Guid Id, string Title, string State, long Revision, DateTimeOffset UpdatedAt);
public sealed record LessonValidation(string[] Errors, string[] Warnings);
public sealed record LessonProposalView(Guid Id, long DraftRevision, string State, string Explanation, LessonContent Content, bool Outdated);
public sealed record LessonVersionView(Guid Id, int Number, long DraftRevision, LessonContent Content,
    string ContentHash, Guid ApprovedBy, DateTimeOffset ApprovedAt, DateTimeOffset? PublishedAt, DateTimeOffset? ArchivedAt);
public sealed record LessonOperationView(Guid RequestId, Guid? SimulationId, string Action, string Text,
    long DraftRevision, string State, bool IsGenerating, string? ErrorCode, int Attempts,
    DateTimeOffset? RetryNotBefore, string? Reply, string? Outcome, string? ValidationType,
    string ProviderId, string ModelId);
public sealed record LessonSimulationItem(Guid Id, long DraftRevision, string State, long Revision);
public sealed record LessonView(Guid Id, string State, long Revision, long DraftRevision,
    LessonContent Content, LessonValidation Validation, LessonProposalView[] Proposals,
    LessonVersionView[] Versions, LessonOperationView[] Operations, LessonSimulationItem[] Simulations);
// Simulation clients deliberately receive the learner projection, without private keys/rubrics.
public sealed record SimulationExerciseView(string Id, string Kind, string Prompt, string[] Options);
public sealed record LessonSimulationView(Guid Id, Guid LessonId, long DraftRevision, string State,
    long Revision, string Title, string SectionTitle, string SectionContent,
    SimulationExerciseView? Exercise, int SectionIndex, int SectionCount, int Hints,
    bool Answered, LessonOperationView[] Operations);
public interface ILessonService
{
    Task<string> ExternalPromptAsync(LessonExternalOptions options, CancellationToken ct);
    Task<LessonImportPreview> PreviewImportAsync(LessonImportInput input, CancellationToken ct);
    Task<LessonView> ImportAsync(CreateImportedLessonRequest request, CancellationToken ct);
    Task<LessonItem[]> ListAsync(int page, CancellationToken ct);
    Task<LessonView> CreateAsync(CreateLessonRequest request, CancellationToken ct);
    Task<LessonView> GetAsync(Guid id, CancellationToken ct);
    Task<LessonView> SaveAsync(Guid id, SaveLessonRequest request, CancellationToken ct);
    Task<LessonView> CommandAsync(Guid id, string action, LessonCommandRequest request, CancellationToken ct);
    Task<LessonView> DesignAsync(Guid id, LessonAiRequest request, CancellationToken ct);
    Task<LessonView> CancelDesignAsync(Guid id, Guid requestId, CancellationToken ct);
    Task<LessonSimulationView> StartSimulationAsync(Guid id, LessonCommandRequest request, CancellationToken ct);
    Task<LessonSimulationView> GetSimulationAsync(Guid id, Guid simulationId, CancellationToken ct);
    Task<LessonSimulationView> SimulateAsync(Guid id, Guid simulationId, LessonAiRequest request, CancellationToken ct);
    Task<LessonSimulationView> SimulationCommandAsync(Guid id, Guid simulationId, string action, LessonCommandRequest request, CancellationToken ct);
}
