using System.Text.Json;
using AulaTutor.Application.Ai;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class GeminiAiProvider(AiHttpTransport transport, IOptions<AiProviderOptions> options) : HttpAiProvider(transport, options.Value)
{
    private readonly RemoteAiOptions settings = options.Value.Gemini;
    public override string ProviderId => "Gemini";
    protected override string Model => settings.Model;
    private bool Structured => Model is "gemini-2.5-flash" or "gemini-2.5-flash-lite" or "gemini-2.5-pro" or "gemini-3.8-flash";
    public override AiCapabilities Capabilities => new(Structured, Structured, false, true);
    protected override HttpRequestMessage Build(AiChatRequest request, JsonElement? schema)
    {
        var config = new Dictionary<string, object> { ["maxOutputTokens"] = request.MaxOutputTokens };
        if (request.ResponseContract.Format != "text")
        {
            var format = new Dictionary<string, object> { ["mimeType"] = "APPLICATION_JSON" };
            if (schema.HasValue) format["schema"] = schema.Value;
            config["responseFormat"] = new { text = format };
        }
        var body = new Dictionary<string, object> { ["generationConfig"] = config,
            ["contents"] = request.Messages.Where(x => x.Role != "system").Select(x => new { role = x.Role == "assistant" ? "model" : "user", parts = new[] { new { text = x.Text } } }).ToArray() };
        var instructions = request.Messages.Where(x => x.Role == "system").ToArray();
        if (instructions.Length > 0) body["systemInstruction"] = new { parts = instructions.Select(x => new { text = x.Text }).ToArray() };
        return JsonPost($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent", body, settings.ApiKey, true);
    }
    protected override AiChatResult Parse(JsonElement response)
    {
        var candidate = response.GetProperty("candidates")[0];
        var text = string.Concat(candidate.GetProperty("content").GetProperty("parts").EnumerateArray()
            .Where(x => !x.TryGetProperty("thought", out var thought) || !thought.GetBoolean()).Select(x => x.GetProperty("text").GetString()));
        return Result(text, Usage(response, "usageMetadata", "promptTokenCount", "candidatesTokenCount"), candidate.GetProperty("finishReason").GetString());
    }
}
