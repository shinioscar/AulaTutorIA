using System.Text.Json;
using AulaTutor.Application.Ai;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class OllamaAiProvider(AiHttpTransport transport, IOptions<AiProviderOptions> options) : HttpAiProvider(transport, options.Value)
{
    private readonly OllamaOptions settings = options.Value.Ollama;
    public override string ProviderId => "Ollama";
    protected override string Model => settings.Model;
    // Verified documented example model; unknown local models remain text-only until reviewed.
    private bool Structured => Model is "gpt-oss" or "gpt-oss:20b" or "gpt-oss:120b";
    public override AiCapabilities Capabilities => new(Structured, Structured, false, true);
    protected override HttpRequestMessage Build(AiChatRequest request, JsonElement? schema)
    {
        var body = new Dictionary<string, object> { ["model"] = Model, ["stream"] = false,
            ["messages"] = request.Messages.Select(x => new { role = x.Role, content = x.Text }).ToArray(),
            ["options"] = new { num_predict = request.MaxOutputTokens } };
        if (request.ResponseContract.Format == "json") body["format"] = "json";
        if (schema.HasValue) body["format"] = schema.Value;
        return JsonPost(new Uri(new Uri(settings.BaseUrl), "/api/chat").AbsoluteUri, body);
    }
    protected override AiChatResult Parse(JsonElement response)
    {
        if (!response.GetProperty("done").GetBoolean()) throw new JsonException();
        return Result(response.GetProperty("message").GetProperty("content").GetString()!,
            Usage(response, "", "prompt_eval_count", "eval_count"), response.GetProperty("done_reason").GetString());
    }
}
