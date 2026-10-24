using System.Text.Json;
using AulaTutor.Application.Ai;

namespace AulaTutor.Infrastructure.Ai;

public sealed class PersonalRemoteAiProvider(AiHttpTransport transport, AiProviderOptions options, string provider,
    string model, string key, bool strict, bool freeOnly, string? workspace = null) : HttpAiProvider(transport, options)
{
    public override string ProviderId => provider;
    protected override string Model => model;
    public override AiCapabilities Capabilities => new(false, true, strict, true);
    protected override bool ConfigurationValid => provider is "OpenRouter" or "Anthropic" && PersonalModelCatalog.ValidId(model) &&
        key.Length is >= 8 and <= 4096 && !key.Any(c => c < 33 || c > 126) && Options.TimeoutSeconds is >= 1 and <= 120;
    protected override IEnumerable<string> Secrets => [key];
    protected override HttpRequestMessage Build(AiChatRequest request, JsonElement? schema)
    {
        if (provider == "OpenRouter")
        {
            var routing = new Dictionary<string, object> { ["require_parameters"] = true, ["allow_fallbacks"] = false };
            if (freeOnly) routing["max_price"] = new { prompt = 0, completion = 0, request = 0, image = 0 };
            var body = new Dictionary<string, object> { ["model"] = model, ["stream"] = false, ["max_tokens"] = request.MaxOutputTokens,
                ["provider"] = routing, ["messages"] = request.Messages.Select(m => new { role = m.Role, content = m.Text }).ToArray() };
            if (schema.HasValue) body["response_format"] = new { type = "json_schema", json_schema = new { name = "aulatutor_response", strict = request.ResponseContract.Strict, schema = schema.Value } };
            else if (request.ResponseContract.Format == "json") body["response_format"] = new { type = "json_object" };
            return JsonPost("https://openrouter.ai/api/v1/chat/completions", body, key);
        }
        var claude = new Dictionary<string, object> { ["model"] = model, ["max_tokens"] = request.MaxOutputTokens, ["stream"] = false,
            ["messages"] = request.Messages.Where(m => m.Role != "system").Select(m => new { role = m.Role, content = m.Text }).ToArray() };
        var system = string.Join("\n", request.Messages.Where(m => m.Role == "system").Select(m => m.Text));
        if (system.Length > 0) claude["system"] = system;
        if (schema.HasValue) claude["output_config"] = new { format = new { type = "json_schema", schema = schema.Value } };
        var message = JsonPost("https://api.anthropic.com/v1/messages", claude);
        message.Headers.Add("x-api-key", key); message.Headers.Add("anthropic-version", "2026-10-21");
        if (workspace is not null) message.Headers.Add("anthropic-workspace-id", workspace);
        return message;
    }
    protected override AiChatResult Parse(JsonElement response)
    {
        if (provider == "OpenRouter")
        {
            var choice = response.GetProperty("choices")[0];
            return Result(choice.GetProperty("message").GetProperty("content").GetString()!, Usage(response, "usage", "prompt_tokens", "completion_tokens"), choice.GetProperty("finish_reason").GetString());
        }
        var blocks = response.GetProperty("content").EnumerateArray().ToArray();
        if (blocks.Any(b => b.GetProperty("type").GetString() != "text")) throw new JsonException();
        var reason = response.GetProperty("stop_reason").GetString();
        return Result(string.Concat(blocks.Select(b => b.GetProperty("text").GetString())), Usage(response, "usage", "input_tokens", "output_tokens"), reason == "end_turn" ? "stop" : reason);
    }
}
