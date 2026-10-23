using System.ComponentModel.DataAnnotations;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Lessons;

namespace AulaTutor.Application.Tutoring;

public sealed record CreateTutoringRequest(Guid RequestId,
    [Required, StringLength(160, MinimumLength = 2)] string Topic,
    [Required, StringLength(500, MinimumLength = 2)] string Goal,
    [Required, RegularExpression("^(Beginner|Intermediate|Advanced)$")] string Level = "Beginner",
    bool DirectReview = false, Guid? SourceRecommendationId = null);
public sealed record TutoringTurnRequest(Guid RequestId, long Revision,
    [Required, RegularExpression("^(Start|Message|Hint|Simplify|DiagnosticQuestion|AnalyzeDiagnosis|GeneratePlan|ActivatePlan|SubmitAttempt|NextActivity|GenerateExercise|FinalReport)$")] string Action,
    [StringLength(2000)] string? Text = null, bool Retry = false, Guid? ExerciseId = null);
public sealed record TutoringTransitionRequest(long Revision);
public sealed record TutoringSessionItem(Guid Id, string Topic, string Goal, string State,
    DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt, DateTimeOffset? CompletedAt,
    string ProviderId, string ModelId);
public sealed record TutoringMessageView(Guid Id, Guid RequestId, int Sequence, string Role,
    string Text, DateTimeOffset CreatedAt, string? ProviderId, string? ModelId,
    string? PromptVersion, AiTokenUsage? Usage);
public sealed record TutoringTurnView(Guid RequestId, string Action, string State, int Attempts,
    bool IsGenerating, bool CanRetry, string? ErrorCode, string? Error,
    DateTimeOffset? RetryNotBefore, long? DurationMs, Guid? ExerciseId = null, string? RetryText = null);
public sealed record TutoringSessionView(Guid Id, string Topic, string Goal, string Level,
    bool DirectReview, string State, long Revision, DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt, DateTimeOffset? CompletedAt, string ProviderId,
    string ModelId, string PolicyVersion, string Activity, string CurrentFocus,
    TutoringMessageView[] Messages, TutoringTurnView[] Turns, Guid? PendingRequestId,
    string? Summary, LearningView? Learning = null, string Mode = "Private", AssignedSessionView? AssignedClass = null);
public sealed record TutoringReply(string Text, string Activity, string Focus, AiChatResult Result, string? LearningPayload = null);
public interface ITutoringService
{
    Task<TutoringSessionView> SendCardAsync(Guid id, TutoringTurnRequest request, CancellationToken ct);
    Task<TutoringSessionView> CreateAsync(CreateTutoringRequest request, CancellationToken ct);
    Task<TutoringSessionItem[]> ListAsync(int page, CancellationToken ct);
    Task<TutoringSessionView> GetAsync(Guid id, CancellationToken ct);
    Task<TutoringSessionView> SendAsync(Guid id, TutoringTurnRequest request, CancellationToken ct);
    Task<TutoringSessionView> TransitionAsync(Guid id, string action, long revision, CancellationToken ct);
    Task<TutoringSessionView> CancelAsync(Guid id, Guid requestId, long revision, CancellationToken ct);
}
