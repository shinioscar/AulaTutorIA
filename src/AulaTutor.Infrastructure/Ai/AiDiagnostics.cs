using System.Diagnostics;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class AiDiagnostics(IAiProvider provider, IOptions<AiProviderOptions> options,
    ICurrentActor current, ILogger<AiDiagnostics> logger) : IAiDiagnostics
{
    private async Task RequireAdminAsync()
    {
        if (!(await current.GetAsync()).IsAdmin) throw new AppException(403, "Se requiere administración activa.");
    }
    public async Task<AiDiagnosticConfiguration> GetAsync(CancellationToken ct)
    {
        await RequireAdminAsync();
        var health = await provider.CheckHealthAsync(ct);
        return new(health.ProviderId, health.ModelId, health.IsHealthy, provider.Capabilities,
            options.Value.TimeoutSeconds, options.Value.MaxConcurrency, options.Value.MaxRetries);
    }
    public async Task<AiDiagnosticResult> TestAsync(CancellationToken ct)
    {
        var config = await GetAsync(ct);
        var timer = Stopwatch.StartNew(); var correlation = Guid.NewGuid().ToString("N");
        try
        {
            var result = await provider.GenerateAsync(new AiChatRequest("admin_diagnostic",
                [new("system", "Responde de forma breve. No solicites datos personales."), new("user", "Responde únicamente: Conexión de prueba correcta.")],
                new("text"), correlation, 128, "diagnostic-v1"), ct);
            logger.LogInformation("AI diagnostic {Provider} {Model} {CorrelationId} diagnostic-v1 {DurationMs}ms {InputTokens} {OutputTokens} Success",
                result.ProviderId, result.ModelId, correlation, timer.ElapsedMilliseconds, result.Usage?.InputTokens, result.Usage?.OutputTokens);
            return new(true, result.ProviderId, result.ModelId, timer.ElapsedMilliseconds, result.Text, result.Usage, null, null, null);
        }
        catch (AiProviderException error)
        {
            logger.LogInformation("AI diagnostic {Provider} {Model} {CorrelationId} diagnostic-v1 {DurationMs}ms {ErrorCode}",
                config.Provider, config.Model, correlation, timer.ElapsedMilliseconds, error.Code);
            return new(false, config.Provider, config.Model, timer.ElapsedMilliseconds, null, null, error.Code.ToString(), error.Message, error.RetryAfter?.TotalSeconds);
        }
    }
}
