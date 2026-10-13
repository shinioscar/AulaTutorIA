namespace AulaTutor.Application.Ai;

// Foundation contract only. No provider SDK or transport dependencies here.
public interface IAiProvider
{
    string ProviderId { get; }
    AiCapabilities Capabilities { get; }
    Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct);
    Task<AiHealthResult> CheckHealthAsync(CancellationToken ct);
}
