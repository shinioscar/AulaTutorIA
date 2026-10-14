using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AulaTutor.Application.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class AiHttpTransport(IHttpClientFactory clients, IOptions<AiProviderOptions> options,
    ILogger<AiHttpTransport> logger) : IDisposable
{
    private readonly AiProviderOptions settings = options.Value;
    private readonly SemaphoreSlim slots = new(options.Value.MaxConcurrency);
    public async Task<JsonDocument> SendAsync(Func<HttpRequestMessage> requestFactory, string provider, string model, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var admitted = false; var timer = Stopwatch.StartNew(); var outcome = "Success";
        try
        {
            await slots.WaitAsync(deadline.Token); admitted = true;
            using var client = clients.CreateClient("Ai");
            for (var attempt = 0; ; attempt++)
            {
                using var request = requestFactory();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("AI upstream rejection {Provider} {Model} HTTP {StatusCode}", provider, model, (int)response.StatusCode);
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    if (response.Headers.RetryAfter?.Date is { } date) retryAfter = date - DateTimeOffset.UtcNow;
                    if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
                    if (attempt < settings.MaxRetries && response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable &&
                        (!retryAfter.HasValue || retryAfter <= TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds)))
                    {
                        await Task.Delay(retryAfter ?? TimeSpan.FromSeconds(1), deadline.Token);
                        continue;
                    }
                    // Only an exact allowlisted Groq code is classified. Never retain or expose
                    // error.message, failed_generation or arbitrary upstream strings.
                    if (provider == "Groq" && response.StatusCode == HttpStatusCode.BadRequest &&
                        await IsGroqGenerationFailureAsync(response.Content, deadline.Token))
                        throw new AiProviderException(AiErrorCode.InvalidResponse);
                    throw new AiProviderException(response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized => AiErrorCode.Authentication,
                        HttpStatusCode.Forbidden => AiErrorCode.Forbidden,
                        HttpStatusCode.TooManyRequests => AiErrorCode.RateLimited,
                        _ when (int)response.StatusCode >= 500 => AiErrorCode.Unavailable,
                        _ => AiErrorCode.Rejected
                    }, retryAfter);
                }
                if (response.Content.Headers.ContentLength > 2_000_000) throw new AiProviderException(AiErrorCode.InvalidResponse);
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var buffer = new MemoryStream(); var chunk = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(chunk, deadline.Token)) != 0)
                {
                    if (buffer.Length + read > 2_000_000) throw new AiProviderException(AiErrorCode.InvalidResponse);
                    buffer.Write(chunk, 0, read);
                }
                return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 48 });
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { outcome = "Timeout"; throw new AiProviderException(AiErrorCode.Timeout); }
        catch (OperationCanceledException) { outcome = "Canceled"; throw; }
        catch (HttpRequestException) { outcome = "Connection"; throw new AiProviderException(AiErrorCode.Connection); }
        catch (IOException) { outcome = "Connection"; throw new AiProviderException(AiErrorCode.Connection); }
        catch (JsonException) { outcome = "InvalidResponse"; throw new AiProviderException(AiErrorCode.InvalidResponse); }
        catch (AiProviderException e) { outcome = e.Code.ToString(); throw; }
        finally
        {
            if (admitted) slots.Release();
            logger.LogInformation("AI transport {Provider} {Model} {DurationMs}ms {Outcome}", provider, model, timer.ElapsedMilliseconds, outcome);
        }
    }
    private static async Task<bool> IsGroqGenerationFailureAsync(HttpContent content, CancellationToken ct)
    {
        const int limit = 16_384;
        if (content.Headers.ContentLength > limit) return false;
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[2048];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + read > limit) return false;
            buffer.Write(chunk, 0, read);
        }
        try
        {
            using var error = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            return error.RootElement.ValueKind == JsonValueKind.Object &&
                error.RootElement.TryGetProperty("error", out var detail) && detail.ValueKind == JsonValueKind.Object &&
                detail.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String &&
                code.GetString() is "json_validate_failed" or "tool_use_failed";
        }
        catch (JsonException) { return false; }
    }
    public void Dispose() => slots.Dispose();
}
