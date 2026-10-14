using System.Text.Json;
using AulaTutor.Application.Ai;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class GroqAiProvider(AiHttpTransport transport, IOptions<AiProviderOptions> options) : HttpAiProvider(transport, options.Value)
{
    private readonly RemoteAiOptions settings = options.Value.Groq;
    public override string ProviderId => "Groq";
    protected override string Model => settings.Model;
    private bool Strict => Model is "openai/gpt-oss-20b" or "openai/gpt-oss-120b" or "qwen/qwen3.8-27b";
    public override AiCapabilities Capabilities => new(true, Strict || Model == "openai/gpt-oss-safeguard-20b", Strict, true);
    protected override HttpRequestMessage Build(AiChatRequest request, JsonElement? schema)
    {
        var body = new Dictionary<string, object> { ["model"] = Model, ["stream"] = false,
            ["max_completion_tokens"] = request.MaxOutputTokens,
            ["messages"] = request.Messages.Select(x => new { role = x.Role, content = x.Text }).ToArray() };
        if (Model is "openai/gpt-oss-20b" or "openai/gpt-oss-120b")
        {
            body["reasoning_effort"] = request.UseCase.StartsWith("tutoring_", StringComparison.Ordinal) ? "medium" : "low";
            body["include_reasoning"] = false;
            if (request.ResponseContract.Format != "text") body["temperature"] = 0.2;
        }
        if (request.ResponseContract.Format == "json") body["response_format"] = new { type = "json_object" };
        if (schema.HasValue) body["response_format"] = new { type = "json_schema", json_schema = new { name = "aulatutor_response", strict = request.ResponseContract.Strict, schema = schema.Value } };
        return JsonPost("https://api.groq.com/openai/v1/chat/completions", body, settings.ApiKey);
    }
    protected override AiChatResult Parse(JsonElement response)
    {
        var choice = response.GetProperty("choices")[0];
        return Result(choice.GetProperty("message").GetProperty("content").GetString()!,
            Usage(response, "usage", "prompt_tokens", "completion_tokens"), choice.GetProperty("finish_reason").GetString());
    }
}
