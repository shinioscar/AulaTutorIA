using AulaTutor.Application.Ai;

namespace AulaTutor.Infrastructure.Ai;

// Deterministic foundation stub: no network, credentials, teaching or assessment.
public sealed class MockAiProvider : IAiProvider
{
    public string ProviderId => "Mock";
    public const string ModelId = "mock-foundation-v1";
    public AiCapabilities Capabilities => new(false, false, false, false);

    public Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.ResponseContract.Format != "text")
        {
            throw new NotSupportedException("The foundation Mock supports text only.");
        }

        return Task.FromResult(new AiChatResult(
            "Proveedor simulado de la Iteración 0. No genera tutorías ni evaluaciones.",
            ProviderId, ModelId, null, null, "stop"));
    }

    public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new AiHealthResult(true, ProviderId, ModelId));
    }
}
