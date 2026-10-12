namespace AulaTutor.Application.Ai;

public sealed record AiMessage(string Role, string Text);

// Provisional shape: capability negotiation and schema validation belong to iteration 2.
public sealed record AiResponseContract(string Format, string? JsonSchema = null);
public sealed record AiTokenUsage(int InputTokens, int OutputTokens, bool IsEstimated);
public sealed record AiHealthResult(bool IsHealthy, string ProviderId, string ModelId);

public sealed record AiChatRequest(
    string UseCase,
    IReadOnlyList<AiMessage> Messages,
    AiResponseContract ResponseContract,
    string CorrelationId);

public sealed record AiChatResult(
    string Text,
    string ProviderId,
    string ModelId,
    AiTokenUsage? Usage,
    string? StructuredPayload,
    string? FinishReason);
