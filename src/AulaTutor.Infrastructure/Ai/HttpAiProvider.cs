using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AulaTutor.Application.Ai;

namespace AulaTutor.Infrastructure.Ai;

public abstract class HttpAiProvider(AiHttpTransport transport, AiProviderOptions options) : IAiProvider
{
    protected AiProviderOptions Options => options;
    public abstract string ProviderId { get; }
    protected abstract string Model { get; }
    public abstract AiCapabilities Capabilities { get; }
    protected abstract HttpRequestMessage Build(AiChatRequest request, JsonElement? schema);
    protected abstract AiChatResult Parse(JsonElement response);
    protected virtual bool ConfigurationValid => options.IsValid() && options.Provider == ProviderId;
    protected virtual IEnumerable<string> Secrets => new[] { options.Groq.ApiKey, options.Gemini.ApiKey };
    public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new AiHealthResult(ConfigurationValid, ProviderId, Model)); // Configuration only; no network.
    }
    public async Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!ConfigurationValid) throw new AiProviderException(AiErrorCode.Configuration);
        if (request.Messages.Count is < 1 or > 32 || request.Messages.Any(x => x.Role is not ("system" or "user" or "assistant") || string.IsNullOrWhiteSpace(x.Text)) ||
            request.Messages.Sum(x => (long)x.Text.Length) > 32000 || !request.Messages.Any(x => x.Role == "user") ||
            request.MaxOutputTokens < 1 || request.MaxOutputTokens > (request.UseCase == "prepared_generate" ? 12288 : request.UseCase is "prepared_evaluate" or "prepared_diagnostic_evaluate" or "lesson_plan" or "lesson_content" or "lesson_exercises" or "lesson_rubrics" or "lesson_preloadedplan" ? 4096 : 2048))
            throw new AiProviderException(AiErrorCode.InvalidRequest);
        if (request.ResponseContract.Format is not ("text" or "json" or "json-schema")) throw new AiProviderException(AiErrorCode.Unsupported);
        if (request.ResponseContract.Format == "json" && !Capabilities.Json || request.ResponseContract.Format == "json-schema" && !Capabilities.JsonSchema ||
            request.ResponseContract.Strict && !Capabilities.StrictJsonSchema) throw new AiProviderException(AiErrorCode.Unsupported);
        var schema = AiJsonContract.ValidateSchema(request.ResponseContract);
        // Byte-based planning allowance, not a tokenizer or confirmed upstream usage.
        var allowance = request.Messages.Sum(m => System.Text.Encoding.UTF8.GetByteCount(m.Text)) + request.MaxOutputTokens + 4096;
        using var response = await transport.SendAsync(() => Build(request, schema), ProviderId, Model, ct, allowance);
        AiChatResult result;
        try { result = Parse(response.RootElement); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        { throw new AiProviderException(AiErrorCode.InvalidResponse); }
        var keys = Secrets.Where(x => !string.IsNullOrEmpty(x));
        if (string.IsNullOrWhiteSpace(result.Text) || result.Text.Length > 64000 || keys.Any(x => result.Text.Contains(x, StringComparison.Ordinal)))
            throw new AiProviderException(AiErrorCode.InvalidResponse);
        if (result.FinishReason is not ("stop" or "STOP")) throw new AiProviderException(AiErrorCode.InvalidResponse);
        AiJsonContract.ValidateOutput(result.Text, request.ResponseContract.Format, schema);
        return result with { StructuredPayload = request.ResponseContract.Format == "text" ? null : result.Text };
    }
    protected AiChatResult Result(string text, AiTokenUsage? usage, string? finish) => new(text, ProviderId, Model, usage, null, finish);
    protected static AiTokenUsage? Usage(JsonElement root, string field, string input, string output)
    {
        var usage = root;
        if (field.Length > 0 && !root.TryGetProperty(field, out usage)) return null;
        if (!usage.TryGetProperty(input, out var i) || !usage.TryGetProperty(output, out var o)) return null;
        var inTokens = i.GetInt32(); var outTokens = o.GetInt32();
        if (inTokens < 0 || outTokens < 0) throw new JsonException();
        return new(inTokens, outTokens, false);
    }
    protected static HttpRequestMessage JsonPost(string url, object body, string? key = null, bool google = false)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (key is not null)
        {
            if (google) message.Headers.Add("x-goog-api-key", key);
            else message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        return message;
    }
}
