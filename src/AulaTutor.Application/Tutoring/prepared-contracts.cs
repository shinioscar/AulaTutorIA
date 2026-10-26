using AulaTutor.Application.Lessons;

namespace AulaTutor.Application.Tutoring;

public sealed record PreparedObjective(string Id, string Title);
public sealed record PreparedActivity(string Id, string ObjectiveId, string Explanation, string Example,
    ExerciseProposal Exercise, string ReferenceSolution, string[] Criteria, PreloadedExercise Aids);
public sealed record PreparedContent(string Format, string Introduction, PreparedObjective[] Objectives, PreparedActivity[] Activities);
public sealed record PreparedFinding(string ObjectiveId, string Text, Guid[] EvidenceIds);
public sealed record PreparedEvaluation(ReportContent Report, PreparedFinding[] Findings);
public sealed record PreparedDiagnosticEvaluation(ReportContent Report, PreparedFinding[] Findings, AssessmentContent Assessment, LearningPlanPayload Plan);
public sealed record PreparedLengthOption(string Id, int Activities, int GenerationOutputTokens, bool Available);
public sealed record PreparedOptionsView(string ProviderId, string ModelId, PreparedLengthOption[] Tutoring,
    PreparedLengthOption[] Diagnostic, string? Reason);
public sealed record PreparedLearningView(string GenerationState, string Length, string ContractVersion, string? ContentHash,
    int Position, int Total, int Delivered, Guid? CurrentExerciseId, string? Objective, string? Explanation,
    string? Example, string? Introduction, bool CanComplete, int RemainingHints, int RemainingAlternatives);
public sealed record StartPreparedPlanRequest(Guid RequestId, Guid PlanId, int ObjectiveOrder, string Modality, string Length = "Short");
