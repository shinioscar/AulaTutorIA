namespace AulaTutor.Application.Ai;

public sealed record AiMessage(string Role, string Text);

public static class AiMessageRoles
{
    public const string System = "system", User = "user", Assistant = "assistant";
}
public sealed record AiResponseContract(string Format, string? JsonSchema = null, bool Strict = false);
public sealed record AiTokenUsage(int InputTokens, int OutputTokens, bool IsEstimated);
public sealed record AiHealthResult(bool IsHealthy, string ProviderId, string ModelId);

public sealed record AiChatRequest(
    string UseCase,
    IReadOnlyList<AiMessage> Messages,
    AiResponseContract ResponseContract,
    string CorrelationId,
    int MaxOutputTokens = 256,
    string PromptVersion = "v1");

public sealed record AiChatResult(
    string Text,
    string ProviderId,
    string ModelId,
    AiTokenUsage? Usage,
    string? StructuredPayload,
    string? FinishReason);

public sealed record AiCapabilities(bool Json, bool JsonSchema, bool StrictJsonSchema,
    bool StreamingTransport, bool StreamingImplemented = false);
// Future streaming boundary; transport implementation is deferred.
public sealed record AiStreamChunk(string Text, bool IsFinal, AiTokenUsage? Usage = null);
public enum AiErrorCode { Configuration, InvalidRequest, Unsupported, Authentication, Forbidden,
    RateLimited, Unavailable, Connection, Timeout, InvalidResponse, Rejected }
public sealed class AiProviderException(AiErrorCode code, TimeSpan? retryAfter = null) : Exception(SafeMessage(code))
{
    public AiErrorCode Code { get; } = code;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    private static string SafeMessage(AiErrorCode code) => code switch
    {
        AiErrorCode.Authentication => "El proveedor rechazó la credencial configurada.",
        AiErrorCode.Forbidden => "El proveedor no permite esta operación.",
        AiErrorCode.RateLimited => "El proveedor limitó la cuota. Espera antes de reintentar.",
        AiErrorCode.Timeout => "Se agotó el tiempo de la solicitud.",
        AiErrorCode.Connection => "No se pudo conectar con el proveedor.",
        AiErrorCode.Unavailable => "El proveedor no está disponible temporalmente.",
        AiErrorCode.InvalidResponse => "El proveedor devolvió una respuesta inválida o incompleta.",
        AiErrorCode.Unsupported => "El modelo no admite el formato solicitado.",
        AiErrorCode.InvalidRequest => "El contrato o los límites de la solicitud no son válidos.",
        AiErrorCode.Rejected => "El proveedor rechazó la solicitud.",
        _ => "La configuración del proveedor no es válida."
    };
}
public sealed record AiDiagnosticConfiguration(string Provider, string Model, bool ConfigurationValid,
    AiCapabilities Capabilities, int TimeoutSeconds, int MaxConcurrency, int MaxRetries);
public sealed record AiDiagnosticResult(bool Success, string Provider, string Model, long DurationMs,
    string? Text, AiTokenUsage? Usage, string? ErrorCode, string? Error, double? RetryAfterSeconds);
public interface IAiDiagnostics
{
    Task<AiDiagnosticConfiguration> GetAsync(CancellationToken ct);
    Task<AiDiagnosticResult> TestAsync(CancellationToken ct);
}
