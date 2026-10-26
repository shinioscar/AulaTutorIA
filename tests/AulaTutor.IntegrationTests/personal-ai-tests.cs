using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AulaTutor.Api.Identity;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace AulaTutor.IntegrationTests;

public sealed partial class PersonalAiTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> factory = null!;
    private string database = "", maintenance = "";
    private string? custodyDirectory;
    private readonly string password = "Aa1!" + Guid.NewGuid().ToString("N");
    private readonly Gateway gateway = new();
    private const string Key = "gsk_fictitious_personal_test_credential_only";
    private sealed class ClientFactory(Gateway handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan }; }
    private sealed class Gateway : HttpMessageHandler
    {
        public int Exchanges, Generations, Catalogs, ExchangeBytesRead;
        public bool OversizedExchange;
        private sealed class ExchangeStream(Gateway owner) : MemoryStream(new byte[200_000], false)
        {
            public override Task CopyToAsync(Stream destination,int bufferSize,CancellationToken ct)
            { var bytes=ToArray();owner.ExchangeBytesRead+=bytes.Length;return destination.WriteAsync(bytes,ct).AsTask(); }
        }
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool Hold;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Verifier, Body, Host, Authorization;
        public string ModelCatalog=Catalog;
        public string Text = """{"text":"[Simulación] Orientación breve.","activity":"Diagnosis","focus":"Condiciones SQL"}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Host = request.RequestUri!.Host;
            if (Host is not ("openrouter.ai" or "api.groq.com" or "api.anthropic.com" or "generativelanguage.googleapis.com")) throw new InvalidOperationException("Unexpected external destination in simulated QA.");
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            if (request.RequestUri.AbsolutePath == "/api/v1/models") { Catalogs++; return Json(ModelCatalog); }
            if (request.RequestUri.AbsolutePath == "/api/v1/auth/keys")
            {
                Exchanges++; using var json = JsonDocument.Parse(Body!); Verifier = json.RootElement.GetProperty("code_verifier").GetString();
                Assert.Equal("S256", json.RootElement.GetProperty("code_challenge_method").GetString());
                if(OversizedExchange){var content=new StreamContent(new ExchangeStream(this));content.Headers.ContentLength=200_000;return new(HttpStatusCode.OK){Content=content};}
                return new(Status) { Content = JsonContent.Create(new { key = Key }) };
            }
            Generations++; Authorization = request.Headers.Authorization?.Parameter;
            Started.TrySetResult(); if (Hold) await Release.Task.WaitAsync(ct);
            if (Status != HttpStatusCode.OK) { var error = Json("{\"message\":\"discard external details\"}"); error.StatusCode = Status; if ((int)Status == 429) error.Headers.TryAddWithoutValidation("Retry-After", "60"); return error; }
            return Host == "api.anthropic.com" ? new(HttpStatusCode.OK) { Content = JsonContent.Create(new { content = new[] { new { type = "text", text = Text } }, stop_reason = "end_turn", usage = new { input_tokens = 7, output_tokens = 3 } }) } :
                Host == "generativelanguage.googleapis.com" ? new(HttpStatusCode.OK) { Content = JsonContent.Create(new { candidates = new[] { new { content = new { parts = new[] { new { text = Text } } }, finishReason = "STOP" } }, usageMetadata = new { promptTokenCount = 7, candidatesTokenCount = 3 } }) } :
                new(HttpStatusCode.OK) { Content = JsonContent.Create(new { choices = new[] { new { message = new { content = Text }, finish_reason = "stop" } }, usage = new { prompt_tokens = 7, completion_tokens = 3 } }) };
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }
    private const string Catalog = """
        {"data":[
        {"id":"qwen/qwen3.8-27b:free","name":"Modelo gratuito simulado","context_length":65536,"architecture":{"input_modalities":["text"],"output_modalities":["text"]},"top_provider":{"max_completion_tokens":8192},"supported_parameters":["response_format","structured_outputs"],"pricing":{"prompt":"0","completion":"0","request":"0"}},
        {"id":"anthropic/claude-opus-5.5","name":"Modelo de pago simulado","context_length":65536,"architecture":{"input_modalities":["text"],"output_modalities":["text"]},"top_provider":{"max_completion_tokens":8192},"supported_parameters":["response_format","structured_outputs"],"pricing":{"prompt":"0.000005","completion":"0.000025"}},
        {"id":"unsupported/test","name":"Sin formato requerido","context_length":65536,"architecture":{"input_modalities":["text"],"output_modalities":["text"]},"top_provider":{"max_completion_tokens":8192},"supported_parameters":[],"pricing":{"prompt":"0","completion":"0"}}]}
        """;
    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AULATUTOR_TEST_CONNECTION") ?? throw new InvalidOperationException("Real PostgreSQL is required; no skipped BYOK tests.");
        database = "aulatutor_test_" + Guid.NewGuid().ToString("N");
        var config = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", IncludeErrorDetail = false }; maintenance = config.ConnectionString;
        await using(var connection = new NpgsqlConnection(maintenance)) { await connection.OpenAsync(); await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync(); }
        config.Database = database;
        string? certificatePath=null,certificateKeyPath=null;
        if(!OperatingSystem.IsWindows())
        {
            custodyDirectory=Path.Combine(TestHost.Root,".cache","byok-test-"+database);Directory.CreateDirectory(custodyDirectory);
            File.SetUnixFileMode(custodyDirectory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=AulaTutor fictitious custody QA",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
            certificatePath=Path.Combine(custodyDirectory,"certificate.pem");certificateKeyPath=Path.Combine(custodyDirectory,"private.key");
            await File.WriteAllTextAsync(certificatePath,certificate.ExportCertificatePem());await File.WriteAllTextAsync(certificateKeyPath,rsa.ExportRSAPrivateKeyPem());File.SetUnixFileMode(certificateKeyPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        }
        factory = TestHost.Create(config.ConnectionString).WithWebHostBuilder(b => {
            if(custodyDirectory is not null){b.UseSetting("PersonalAi:KeyPath",custodyDirectory);b.UseSetting("PersonalAi:CertificatePath",certificatePath);b.UseSetting("PersonalAi:CertificateKeyPath",certificateKeyPath);}
            b.ConfigureServices(s => {
            s.RemoveAll<IAiProvider>(); s.AddScoped<IAiProvider, PersonalAiProvider>();
            s.RemoveAll<IHttpClientFactory>(); s.AddSingleton<IHttpClientFactory>(new ClientFactory(gateway));
        });});
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        gateway.Release.TrySetResult(); if (factory is not null) await factory.DisposeAsync();
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^aulatutor_test_[a-f0-9]{32}$")) return;
        await using var c = new NpgsqlConnection(maintenance); await c.OpenAsync(); await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", c).ExecuteNonQueryAsync();
        if(custodyDirectory is not null && Path.GetFullPath(custodyDirectory)==Path.Combine(TestHost.Root,".cache","byok-test-"+database) && Directory.Exists(custodyDirectory))Directory.Delete(custodyDirectory,true);
    }
    private static async Task Csrf(HttpClient client)
    { var token = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf"); client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token!.RequestToken); }
    private async Task<HttpClient> User(string role = "Student")
    {
        var email = Guid.NewGuid().ToString("N") + "@example.invalid";
        var client = TestHost.Client(factory); await Csrf(client);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, "Ficticio", role))).StatusCode);
        if (role == "Teacher") { await using var scope = factory.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>(); var u = (await users.FindByEmailAsync(email))!; u.TeacherApproval = TeacherApproval.Approved; await users.UpdateAsync(u); await users.AddToRoleAsync(u, RoleNames.Teacher); }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password))).StatusCode); await Csrf(client); return client;
    }
    private static async Task<PersonalAiView> View(HttpClient client) => (await client.GetFromJsonAsync<PersonalAiView>("/api/ai-connections/"))!;
    private static async Task<OAuthStartView> Start(HttpClient client) { var r = await client.PostAsJsonAsync("/api/ai-connections/openrouter/start", new { }); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<OAuthStartView>())!; }
    private static async Task<HttpResponseMessage> Return(HttpClient client, OAuthStartView start, string? state = null)
    { var query = QueryHelpers.ParseQuery(new Uri(start.AuthorizationUrl).Query); return await client.GetAsync("/api/ai-connections/openrouter/callback?state=" + (state ?? query["state"].ToString()) + "&code=fictitious_single_use_code"); }
    private static async Task<PersonalAiConnection> ConnectManual(HttpClient client, string provider = "Groq")
    { Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/ai-connections/manual", new ManualAiRequest(provider, Key))).StatusCode); var row = (await View(client)).Connections.Single(c => c.Provider == provider); return new() { Id = row.Id, Provider = provider }; }
    private static async Task Activate(HttpClient client, Guid id, string provider = "Groq", string model = "openai/gpt-oss-20b", bool paid = true)
    { Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/ai-connections/models/{provider}")).StatusCode); Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/ai-connections/selection", new SelectAiRequest(id, model, true, paid))).StatusCode); }
    private static async Task<TutoringSessionView> Create(HttpClient client)
    { var r = await client.PostAsJsonAsync("/api/tutoring-sessions/", new CreateTutoringRequest(Guid.NewGuid(), "Condiciones SQL", "Practicar condiciones")); Assert.Equal(HttpStatusCode.Created, r.StatusCode); return (await r.Content.ReadFromJsonAsync<TutoringSessionView>())!; }

    [Theory][InlineData("Teacher")][InlineData("Student")]
    public async Task PkceConnectsOwnerWithEncryptedCredentialAndNoSecretReadback(string role)
    {
        using var client = await User(role); var start = await Start(client); var query = QueryHelpers.ParseQuery(new Uri(start.AuthorizationUrl).Query);
        Assert.Equal("https://openrouter.ai/auth", start.AuthorizationUrl.Split('?')[0]); Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("https://localhost:4200/api/ai-connections/openrouter/callback", query["callback_url"]);
        using var callback = await Return(client, start); Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode); Assert.Equal("https://localhost:4200/ai-provider/return", callback.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode);
        Assert.Equal(query["code_challenge"], WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(gateway.Verifier!))));
        Assert.Equal(1, gateway.Exchanges);
        var response = await client.GetStringAsync("/api/ai-connections/"); Assert.DoesNotContain(Key, response); Assert.DoesNotContain("protectedCredential", response, StringComparison.OrdinalIgnoreCase);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(); var row = await db.PersonalAiConnections.SingleAsync();
        Assert.NotEqual(Key, row.ProtectedCredential); Assert.Equal("OAuth", row.Method);
        var vault = scope.ServiceProvider.GetRequiredService<PersonalAiVault>(); Assert.Equal(Key, vault.Unprotect(PersonalAiVault.CredentialPurpose(row.OwnerId, row.Id, row.CredentialRevision), row.ProtectedCredential));
        Assert.Throws<AppException>(() => vault.Unprotect(PersonalAiVault.CredentialPurpose(Guid.NewGuid(), row.Id, row.CredentialRevision), row.ProtectedCredential));
        Assert.All(await db.AuditEvents.ToArrayAsync(), a => { Assert.DoesNotContain(Key, JsonSerializer.Serialize(a)); Assert.DoesNotContain(gateway.Verifier!, JsonSerializer.Serialize(a)); });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode); Assert.Equal(1, gateway.Exchanges);
    }
    [Theory][InlineData("invalid")][InlineData("absent")][InlineData("expired")][InlineData("replay")][InlineData("duplicate-code")]
    public async Task RejectsInvalidMissingExpiredOrReusedState(string scenario)
    {
        using var client = await User(); var start = await Start(client);
        if (scenario == "expired") { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(); var row = await db.PersonalAiOAuths.SingleAsync(); row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); }
        if (scenario == "replay") { await Return(client, start); using var replay = await Return(client, start); Assert.Contains("rejected", replay.Headers.Location!.ToString()); return; }
        if (scenario == "duplicate-code") { var state=QueryHelpers.ParseQuery(new Uri(start.AuthorizationUrl).Query)["state"].ToString();using var duplicate=await client.GetAsync("/api/ai-connections/openrouter/callback?state="+state+"&code=fictitious_code&code=another_code");Assert.Contains("rejected",duplicate.Headers.Location!.ToString());Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete",new{})).StatusCode);Assert.Equal(0,gateway.Exchanges);return; }
        using var returned = scenario == "absent" ? await client.GetAsync("/api/ai-connections/openrouter/callback?code=fictitious_code") : await Return(client, start, scenario == "invalid" ? PersonalAiVault.Random() : null);
        Assert.Contains("rejected", returned.Headers.Location!.ToString()); Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode); Assert.Equal(0, gateway.Exchanges);
    }
    [Fact]
    public async Task LogoutAndConcurrentTabsCancelOldAuthorization()
    {
        using var client = await User(); var first = await Start(client); var second = await Start(client);
        using var old = await Return(client, first); Assert.Contains("rejected", old.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        using var late = await Return(client, second); Assert.Contains("rejected", late.Headers.Location!.ToString()); Assert.Equal(0, gateway.Exchanges);
    }
    [Theory][InlineData("Gemini")][InlineData("Groq")][InlineData("Anthropic")]
    public async Task ManualConnectionIsOwnedAndDisconnectDoesNotSelectInstitutional(string provider)
    {
        using var a = await User(); using var b = await User(); var c = await ConnectManual(a, provider);
        Assert.Empty((await View(b)).Connections); Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/ai-connections/{c.Id}")).StatusCode);
        var model = provider == "Gemini" ? "gemini-3.8-flash" : provider == "Anthropic" ? "claude-opus-5-5" : "openai/gpt-oss-20b";
        await Activate(a, c.Id, provider, model); Assert.Equal("Personal", (await View(a)).Selection);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/ai-connections/{c.Id}")).StatusCode); Assert.Equal("None", (await View(a)).Selection);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PutAsJsonAsync("/api/ai-connections/selection", new SelectAiRequest(null, null, false, false))).StatusCode); Assert.Equal("Institutional", (await View(a)).Selection);
    }
    [Fact]
    public async Task SelectionChangesDoNotRetargetExistingSessionAndReservationsArePersonal()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client);
        Assert.Equal("Groq", session.ProviderId);
        await client.PutAsJsonAsync("/api/ai-connections/selection", new SelectAiRequest(null, null, false, false));
        var r = await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start")); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var result = (await r.Content.ReadFromJsonAsync<TutoringSessionView>())!; Assert.Equal("Succeeded", result.Turns.Single().State); Assert.Equal(1, gateway.Generations);
        Assert.Equal("Mock", (await Create(client)).ProviderId);
        await using var scope = factory.Services.CreateAsyncScope(); var rows = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.ToArrayAsync();
        var row = Assert.Single(rows); Assert.Equal("Personal", row.Scope); Assert.Equal(c.Id, row.ConnectionId); Assert.StartsWith("tutoring_start:", row.Operation); Assert.Equal(7, row.InputTokens); Assert.Equal(3, row.OutputTokens);
    }
    [Theory][InlineData(401, "Authentication")][InlineData(402, "CreditsRequired")][InlineData(429, "RateLimited")][InlineData(503, "Unavailable")]
    public async Task UpstreamErrorHasNoFallbackAndKeepsChargedReservation(int status, string error)
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client); gateway.Status = (HttpStatusCode)status;
        var r = await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); var view = (await r.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal(error, view.Turns.Single().ErrorCode); Assert.DoesNotContain("discard external", await r.Content.ReadAsStringAsync()); Assert.Equal(1, gateway.Generations); Assert.DoesNotContain(view.Messages, m => m.Role == "Tutor");
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Equal("Failed", (await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.SingleAsync()).State);
    }
    [Fact]
    public async Task DisconnectWhileWaitingDiscardsLateAcademicResult()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client); gateway.Hold = true;
        var pending = client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start"));
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/ai-connections/{c.Id}")).StatusCode); gateway.Release.TrySetResult();
        using var result = await pending; Assert.Equal(HttpStatusCode.OK, result.StatusCode); var view = (await result.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal("Authentication", view.Turns.Single().ErrorCode); Assert.DoesNotContain(view.Messages, m => m.Role == "Tutor");
    }
    [Fact]
    public async Task OpenRouterCatalogWithUnknownOutputLimitLoadsAndRefreshesOnlyVerifiedFreeModelsWithoutInference()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(Catalog)!;
        var data = json["data"]!.AsArray();
        var unknown = data[0]!.DeepClone();
        unknown["id"] = "vendor/unknown-output:free";
        unknown["top_provider"]!["max_completion_tokens"] = null;
        data.Insert(0, unknown);
        var unknownPrice = data[1]!.DeepClone();
        unknownPrice["id"] = "vendor/unknown-price:free";
        unknownPrice["pricing"]!["completion"] = null;
        data.Add(unknownPrice);
        gateway.ModelCatalog = json.ToJsonString();
        using var client = await User();
        var models = (await client.GetFromJsonAsync<PersonalModelView[]>("/api/ai-connections/models/OpenRouter"))!;
        Assert.Equal(3, models.Length);
        Assert.All(models, m => { Assert.True(m.Free); Assert.True(m.PriceKnown); });
        Assert.False(models.Single(m => m.Id == "vendor/unknown-output:free").Compatible);
        Assert.True(models.Single(m => m.Id == "qwen/qwen3.8-27b:free").Compatible);
        Assert.True(models.Single(m => m.Id == "qwen/qwen3.8-27b:free").Free);
        Assert.DoesNotContain(models, m => m.Id == "anthropic/claude-opus-5.5" || m.Id == "vendor/unknown-price:free");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
            Assert.Equal(5, await db.PersonalAiModels.CountAsync());
            foreach (var model in await db.PersonalAiModels.ToArrayAsync()) model.CheckedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
            await db.SaveChangesAsync();
        }
        using var refresh = await client.PostAsJsonAsync("/api/ai-connections/models/OpenRouter/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await refresh.Content.ReadFromJsonAsync<PersonalModelView[]>())!;
        Assert.Equal(3, refreshed.Length);
        Assert.All(refreshed, m => { Assert.True(m.Free); Assert.True(m.PriceKnown); });
        Assert.Equal(2, gateway.Catalogs); Assert.Equal(0, gateway.Exchanges); Assert.Equal(0, gateway.Generations);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.ToArrayAsync());
    }
    [Theory][InlineData("Groq")][InlineData("Gemini")][InlineData("Anthropic")]
    public async Task PersonalCatalogDoesNotExposeModelsWithUnconfirmedPrices(string provider)
    {
        using var client = await User();
        Assert.Empty((await client.GetFromJsonAsync<PersonalModelView[]>($"/api/ai-connections/models/{provider}"))!);
        using var refresh = await client.PostAsJsonAsync($"/api/ai-connections/models/{provider}/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Empty((await refresh.Content.ReadFromJsonAsync<PersonalModelView[]>())!);
        Assert.Equal(0, gateway.Catalogs); Assert.Equal(0, gateway.Generations);
    }
    [Fact]
    public async Task OpenRouterCatalogRequiresCompatibleModelAndExplicitPaidPermission()
    {
        using var client = await User(); var start = await Start(client); await Return(client, start); await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { }); var connection = (await View(client)).Connections.Single();
        var models = (await client.GetFromJsonAsync<PersonalModelView[]>("/api/ai-connections/models/OpenRouter"))!;
        Assert.DoesNotContain(models, m => !m.Free || !m.PriceKnown);
        Assert.True(models.Single(m => m.Id.EndsWith(":free", StringComparison.Ordinal)).Free);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/ai-connections/selection", new SelectAiRequest(connection.Id, "unsupported/test", true, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai-connections/selection", new SelectAiRequest(connection.Id, "anthropic/claude-opus-5.5", true, false))).StatusCode);
        await Activate(client, connection.Id, "OpenRouter", "qwen/qwen3.8-27b:free", false); var session = await Create(client);
        using var response = await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); using var body = JsonDocument.Parse(gateway.Body!); var routing = body.RootElement.GetProperty("provider");
        Assert.False(routing.GetProperty("allow_fallbacks").GetBoolean()); Assert.True(routing.GetProperty("require_parameters").GetBoolean()); Assert.Equal(0, routing.GetProperty("max_price").GetProperty("prompt").GetInt32());
        Assert.DoesNotContain(Key, gateway.Body!); Assert.Equal(1, gateway.Catalogs);
    }
    [Theory][InlineData("Ollama")][InlineData("https://evil.invalid")][InlineData("OpenAI")]
    public async Task ManualAndCatalogRejectArbitraryProviders(string provider)
    {
        using var client = await User(); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai-connections/manual", new ManualAiRequest(provider, Key))).StatusCode);
        Assert.Equal(0, gateway.Generations); Assert.Empty((await View(client)).Connections);
    }
    [Fact]
    public async Task AnonymousCsrfAndOwnerControlsRemainEnforced()
    {
        using var anonymous = TestHost.Client(factory); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/ai-connections/")).StatusCode);
        using var client = await User(); client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai-connections/manual", new ManualAiRequest("Groq", Key))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/start", new { })).StatusCode); Assert.Equal(0, gateway.Exchanges);
    }
}
