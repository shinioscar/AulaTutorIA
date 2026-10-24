using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

// Explicit isolated QA only. This handler has no network fallback or inner handler.
public sealed class PersonalAiQaMode
{
    public bool Enabled { get; }
    public PersonalAiQaMode(IConfiguration config, IHostEnvironment environment, IOptions<AiProviderOptions> ai, IOptions<AiQuotaOptions> quota)
    {
        Enabled = config.GetValue<bool>("PersonalAi:QaSimulation");
        if (!Enabled) return;
        var database = new Npgsql.NpgsqlConnectionStringBuilder(config.GetConnectionString("AulaTutor")).Database ?? "";
        if (!environment.IsDevelopment() || ai.Value.Provider != "Mock" || quota.Value.AllowExternalCalls || !Regex.IsMatch(database, "^aulatutor_(http|test)_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Personal AI simulation requires Development, Mock, disabled external calls and an isolated owned QA database.");
    }
}

public sealed class PersonalAiQaTransport(PersonalAiQaMode mode) : HttpMessageHandler
{
    private const string Key = "qa_personal_fictitious_credential_never_real";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!mode.Enabled) throw new InvalidOperationException("QA transport cannot run outside isolated simulation.");
        var uri = request.RequestUri!;
        if (uri.Scheme != "https" || uri.Host is not ("openrouter.ai" or "api.groq.com" or "api.anthropic.com" or "generativelanguage.googleapis.com")) throw new AiProviderException(AiErrorCode.Forbidden);
        if (uri.Host == "openrouter.ai" && uri.AbsolutePath == "/api/v1/models")
            return Json(new { data = new object[] {
                new { id="qa/structured:free",name="[Simulación] Modelo gratuito",context_length=65536,architecture=new { input_modalities=new[]{"text"},output_modalities=new[]{"text"} },top_provider=new { max_completion_tokens=8192 },supported_parameters=new[]{"response_format","structured_outputs"},pricing=new {prompt="0",completion="0",request="0"} },
                new { id="qa/structured-paid",name="[Simulación] Modelo de pago",context_length=65536,architecture=new { input_modalities=new[]{"text"},output_modalities=new[]{"text"} },top_provider=new { max_completion_tokens=8192 },supported_parameters=new[]{"response_format","structured_outputs"},pricing=new {prompt="0.000001",completion="0.000001",request="0"} },
                new { id="qa/incompatible",name="[Simulación] Modelo incompatible",context_length=4096,architecture=new { input_modalities=new[]{"text"},output_modalities=new[]{"text"} },top_provider=new { max_completion_tokens=1024 },supported_parameters=Array.Empty<string>(),pricing=new {prompt="0",completion="0",request="0"} }
                ,new { id="qa/limited:free",name="[Simulación] Prueba de límite",context_length=65536,architecture=new { input_modalities=new[]{"text"},output_modalities=new[]{"text"} },top_provider=new { max_completion_tokens=8192 },supported_parameters=new[]{"response_format","structured_outputs"},pricing=new {prompt="0",completion="0",request="0"} }
            } });
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        if (uri.Host == "openrouter.ai" && uri.AbsolutePath == "/api/v1/auth/keys")
        {
            if (body.RootElement.GetProperty("code_challenge_method").GetString() != "S256") return new(HttpStatusCode.BadRequest);
            return Json(new { key = Key });
        }
        var scope = PersonalInferenceScope.Current;
        if (scope is not { Simulated: true }) throw new AiProviderException(AiErrorCode.Forbidden);
        string? key = request.Headers.Authorization?.Parameter;
        if (request.Headers.TryGetValues("x-api-key", out var anthropic)) key = anthropic.Single();
        if (request.Headers.TryGetValues("x-goog-api-key", out var google)) key = google.Single();
        if (key is null || !key.StartsWith("qa_", StringComparison.Ordinal)) return new(HttpStatusCode.Unauthorized);
        var root = body.RootElement; var messages = new List<AiMessage>();
        if(root.TryGetProperty("model",out var selectedModel) && selectedModel.GetString()=="qa/limited:free")
        { var limited=new HttpResponseMessage(HttpStatusCode.TooManyRequests);limited.Headers.RetryAfter=new(TimeSpan.FromSeconds(60));return limited; }
        if (uri.Host == "generativelanguage.googleapis.com")
        {
            if (root.TryGetProperty("systemInstruction", out var system)) messages.AddRange(system.GetProperty("parts").EnumerateArray().Select(p => new AiMessage("system", p.GetProperty("text").GetString()!)));
            messages.AddRange(root.GetProperty("contents").EnumerateArray().Select(m => new AiMessage(m.GetProperty("role").GetString() == "model" ? "assistant" : "user", string.Concat(m.GetProperty("parts").EnumerateArray().Select(p => p.GetProperty("text").GetString())))));
        }
        else
        {
            if (root.TryGetProperty("system", out var system)) messages.Add(new("system", system.GetString()!));
            messages.AddRange(root.GetProperty("messages").EnumerateArray().Select(m => new AiMessage(m.GetProperty("role").GetString()!, m.GetProperty("content").GetString()!)));
        }
        var mock = await new MockAiProvider().GenerateAsync(new(scope.Operation, messages, new("text"), Guid.NewGuid().ToString("N")), ct);
        var text = scope.Operation.StartsWith("tutoring_", StringComparison.Ordinal) ? JsonSerializer.Serialize(new { text = mock.Text, activity = scope.Operation.Contains("hint", StringComparison.Ordinal) ? "Hint" : scope.Operation.Contains("start", StringComparison.Ordinal) ? "Diagnosis" : "Feedback", focus = "[Simulación] Práctica académica" }) : mock.Text;
        if (uri.Host == "api.anthropic.com") return Json(new { content = new[] { new { type = "text", text } }, stop_reason = "end_turn" });
        if (uri.Host == "generativelanguage.googleapis.com") return Json(new { candidates = new[] { new { content = new { parts = new[] { new { text } } }, finishReason = "STOP" } } });
        return Json(new { choices = new[] { new { message = new { content = text }, finish_reason = "stop" } } });
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
