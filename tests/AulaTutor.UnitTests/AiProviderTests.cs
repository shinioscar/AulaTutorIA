using System.Net;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure;
using AulaTutor.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AulaTutor.UnitTests;

public sealed class AiProviderTests
{
    private const string Schema = """{"type":"object","properties":{"status":{"type":"string","enum":["ok"]}},"required":["status"],"additionalProperties":false}""";
    private static AiChatRequest Request(string format = "text", string? schema = null, bool strict = false) => new("test",
        [new("system","Only test data"),new("user","Return ok"),new("assistant","ok"),new("user","Again")],new(format,schema,strict),"test-2");
    private static AiProviderOptions Config(string provider) => new() { Provider = provider,
        Groq = new() { Model="openai/gpt-oss-20b", ApiKey=Guid.NewGuid().ToString("N") },
        Gemini = new() { Model="gemini-3.8-flash", ApiKey=Guid.NewGuid().ToString("N") },
        Ollama = new() { Model="gpt-oss" } };
    private static string Body(string provider, string text = "ok", string finish = "stop") => provider switch
    {
        "Groq" => JsonSerializer.Serialize(new { choices=new[] {new {message=new {content=text},finish_reason=finish}},usage=new {prompt_tokens=4,completion_tokens=2}}),
        "Gemini" => JsonSerializer.Serialize(new {candidates=new[] {new {content=new {parts=new[] {new {text}}},finishReason=finish == "stop" ? "STOP" : finish}},usageMetadata=new {promptTokenCount=4,candidatesTokenCount=2}}),
        _ => JsonSerializer.Serialize(new {message=new {content=text},done=true,done_reason=finish,prompt_eval_count=4,eval_count=2})
    };
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        public int Calls {get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return action(request,ct); }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) {Timeout=Timeout.InfiniteTimeSpan}; }
    private static IAiProvider Provider(AiProviderOptions config, Handler handler, out AiHttpTransport transport, ILogger<AiHttpTransport>? log = null)
    {
        var options = Options.Create(config);
        transport = new(new Factory(handler),options,log ?? NullLogger<AiHttpTransport>.Instance);
        return config.Provider switch {"Groq"=>new GroqAiProvider(transport,options),"Gemini"=>new GeminiAiProvider(transport,options),_=>new OllamaAiProvider(transport,options)};
    }
    private static HttpResponseMessage Reply(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status) {Content=new StringContent(json)};

    [Theory]
    [InlineData("Mock",typeof(MockAiProvider))]
    [InlineData("Groq",typeof(GroqAiProvider))]
    [InlineData("Gemini",typeof(GeminiAiProvider))]
    [InlineData("Ollama",typeof(OllamaAiProvider))]
    public void SelectionIsExplicitAndDoesNotSendNetworkRequests(string provider, Type expected)
    {
        var settings = Config(provider);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Ai:Provider"]=provider,["Ai:Groq:Model"]=settings.Groq.Model,["Ai:Groq:ApiKey"]=settings.Groq.ApiKey,
            ["Ai:Gemini:Model"]=settings.Gemini.Model,["Ai:Gemini:ApiKey"]=settings.Gemini.ApiKey,["Ai:Ollama:Model"]=settings.Ollama.Model
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        Assert.IsType(expected,services.GetRequiredService<IAiProvider>());
        Assert.False(services.GetRequiredService<IAiProvider>().Capabilities.StreamingImplemented);
    }

    [Theory]
    [InlineData("Groq")][InlineData("Gemini")][InlineData("Ollama")]
    public async Task MessagesAndUsageAreAdaptedWithoutLeakingCredentials(string provider)
    {
        var config = Config(provider); string? sent = null;
        using var handler = new Handler(async (request,ct) =>
        {
            sent = await request.Content!.ReadAsStringAsync(ct);
            Assert.DoesNotContain(config.Groq.ApiKey,request.RequestUri!.ToString());
            Assert.DoesNotContain(config.Gemini.ApiKey,request.RequestUri.ToString());
            if (provider == "Groq") Assert.Equal(config.Groq.ApiKey,request.Headers.Authorization!.Parameter);
            if (provider == "Gemini") Assert.Equal(config.Gemini.ApiKey,request.Headers.GetValues("x-goog-api-key").Single());
            if (provider == "Ollama") Assert.Null(request.Headers.Authorization);
            return Reply(Body(provider));
        });
        var adapter = Provider(config,handler,out var transport); using var _ = transport;
        var result = await adapter.GenerateAsync(Request(),CancellationToken.None);
        Assert.Equal("ok",result.Text); Assert.Equal(provider,result.ProviderId);
        Assert.Equal(new AiTokenUsage(4,2,false),result.Usage);
        using var doc = JsonDocument.Parse(sent!); var root = doc.RootElement;
        if (provider == "Gemini")
        { Assert.Equal("Only test data",root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()); Assert.Equal("model",root.GetProperty("contents")[1].GetProperty("role").GetString()); }
        else { Assert.Equal("system",root.GetProperty("messages")[0].GetProperty("role").GetString()); Assert.False(root.GetProperty("stream").GetBoolean()); }
        Assert.DoesNotContain(config.Groq.ApiKey,sent!); Assert.DoesNotContain(config.Gemini.ApiKey,JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("Groq")][InlineData("Gemini")][InlineData("Ollama")]
    public async Task SchemaSerializationAndLocalValidation(string provider)
    {
        using var handler = new Handler(async (request,ct) =>
        {
            var sent = await request.Content!.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(sent);
            var schema = provider switch {
                "Groq"=>doc.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema"),
                "Gemini"=>doc.RootElement.GetProperty("generationConfig").GetProperty("responseFormat").GetProperty("text").GetProperty("schema"),
                _=>doc.RootElement.GetProperty("format")};
            Assert.Equal("object",schema.GetProperty("type").GetString());
            return Reply(Body(provider,"{\"status\":\"ok\"}"));
        });
        var adapter = Provider(Config(provider),handler,out var transport); using var _ = transport;
        var result = await adapter.GenerateAsync(Request("json-schema",Schema,provider=="Groq"),CancellationToken.None);
        Assert.Equal("{\"status\":\"ok\"}",result.StructuredPayload);
    }

    [Theory]
    [InlineData(401,AiErrorCode.Authentication)][InlineData(403,AiErrorCode.Forbidden)]
    [InlineData(429,AiErrorCode.RateLimited)][InlineData(500,AiErrorCode.Unavailable)]
    [InlineData(503,AiErrorCode.Unavailable)][InlineData(400,AiErrorCode.Rejected)]
    public async Task ErrorsAreNormalizedAndBodiesDiscarded(int status, AiErrorCode code)
    {
        var config = Config("Groq");
        using var handler = new Handler((_,_) => Task.FromResult(Reply(config.Groq.ApiKey,(HttpStatusCode)status)));
        var adapter = Provider(config,handler,out var transport); using var _ = transport;
        var error = await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),CancellationToken.None));
        Assert.Equal(code,error.Code); Assert.DoesNotContain(config.Groq.ApiKey,error.ToString()); Assert.Null(error.InnerException); Assert.Equal(1,handler.Calls);
    }

    [Theory]
    [InlineData("not-json")][InlineData("[]")][InlineData("{\"status\":\"wrong\"}")]
    [InlineData("{\"status\":\"ok\",\"extra\":true}")][InlineData("{}")]
    public async Task InvalidJsonAndSchemaMismatchFail(string text)
    {
        using var handler = new Handler((_,_)=>Task.FromResult(Reply(Body("Groq",text))));
        var adapter = Provider(Config("Groq"),handler,out var transport); using var _ = transport;
        Assert.Equal(AiErrorCode.InvalidResponse,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request("json-schema",Schema,true),CancellationToken.None))).Code);
    }

    [Fact]
    public async Task UnsupportedSchemaOrModelRejectsBeforeNetwork()
    {
        var config = Config("Gemini");
        using var handler = new Handler((_,_)=>throw new InvalidOperationException("Must not send"));
        var adapter = Provider(config,handler,out var transport); using var _ = transport;
        Assert.Equal(AiErrorCode.Unsupported,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request("json-schema",Schema,true),CancellationToken.None))).Code);
        Assert.Equal(AiErrorCode.InvalidRequest,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request("json-schema",Schema.Replace("\"type\":\"object\"","\"type\":\"object\",\"$ref\":\"https://invalid.example\"")),CancellationToken.None))).Code);
        config.Gemini.Model = "unknown-model";
        Assert.Equal(AiErrorCode.Unsupported,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request("json"),CancellationToken.None))).Code);
        Assert.Equal(0,handler.Calls);
    }

    [Fact]
    public async Task MissingConfigurationAndHealthNeverSend()
    {
        var config = Config("Groq"); config.Groq.ApiKey = "";
        using var handler = new Handler((_,_)=>throw new InvalidOperationException("Must not send"));
        var adapter = Provider(config,handler,out var transport); using var _ = transport;
        Assert.False((await adapter.CheckHealthAsync(CancellationToken.None)).IsHealthy);
        Assert.Equal(AiErrorCode.Configuration,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),CancellationToken.None))).Code);
        Assert.Equal(0,handler.Calls);
    }

    [Fact]
    public async Task CancellationTimeoutAndConnectionAreDistinct()
    {
        var config = Config("Groq"); config.TimeoutSeconds=1;
        using var handler = new Handler(async (_,ct)=>{await Task.Delay(Timeout.Infinite,ct); return Reply("{}");});
        var adapter = Provider(config,handler,out var transport); using var _ = transport;
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.GenerateAsync(Request(),canceled.Token)); Assert.Equal(0,handler.Calls);
        using var inFlight = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.GenerateAsync(Request(),inFlight.Token));
        Assert.Equal(AiErrorCode.Timeout,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),CancellationToken.None))).Code);
        using var offline = new Handler((_,_)=>throw new HttpRequestException("Sensitive upstream"));
        var other = Provider(config,offline,out var otherTransport); using var _o=otherTransport;
        Assert.Equal(AiErrorCode.Connection,(await Assert.ThrowsAsync<AiProviderException>(()=>other.GenerateAsync(Request(),CancellationToken.None))).Code);
    }

    [Fact]
    public async Task RetryAfterIsRespectedAndRetriesAreBounded()
    {
        var config=Config("Groq"); config.MaxRetries=1;
        var count=0;
        using var handler = new Handler((_,_)=>{
            count++; var reply=Reply(Body("Groq"),count==1?HttpStatusCode.TooManyRequests:HttpStatusCode.OK);
            reply.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero); return Task.FromResult(reply);
        });
        var adapter=Provider(config,handler,out var transport); using var _=transport;
        await adapter.GenerateAsync(Request(),CancellationToken.None); Assert.Equal(2,handler.Calls);
        using var limited=new Handler((_,_)=>{var reply=Reply("private",HttpStatusCode.TooManyRequests); reply.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return Task.FromResult(reply);});
        var other=Provider(config,limited,out var otherTransport); using var _o=otherTransport;
        var error=await Assert.ThrowsAsync<AiProviderException>(()=>other.GenerateAsync(Request(),CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(60),error.RetryAfter); Assert.Equal(1,limited.Calls);
    }

    [Fact]
    public async Task ConcurrencyIsBoundedAndReleasedAfterFailures()
    {
        var config=Config("Groq"); config.MaxConcurrency=1; var active=0; var maximum=0;
        using var handler=new Handler(async (_,ct)=>{var now=Interlocked.Increment(ref active); maximum=Math.Max(maximum,now); await Task.Delay(40,ct); Interlocked.Decrement(ref active); return Reply(Body("Groq"));});
        var adapter=Provider(config,handler,out var transport); using var _=transport;
        await Task.WhenAll(Enumerable.Range(0,5).Select(_=>adapter.GenerateAsync(Request(),CancellationToken.None)));
        Assert.Equal(1,maximum); Assert.Equal(0,active);
    }

    private sealed class CaptureLogger : ILogger<AiHttpTransport>
    {
        public List<string> Lines {get;}=[];
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull => null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter)=>Lines.Add(formatter(state,error));
    }
    [Fact]
    public async Task SecretsAndMessagesAreAbsentFromLogsAndEchoedKeyIsRejected()
    {
        var config=Config("Groq"); var logger=new CaptureLogger();
        using var handler=new Handler((_,_)=>Task.FromResult(Reply(Body("Groq",config.Groq.ApiKey))));
        var adapter=Provider(config,handler,out var transport,logger); using var _=transport;
        var error=await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),CancellationToken.None));
        Assert.Equal(AiErrorCode.InvalidResponse,error.Code);
        var logs=string.Join('\n',logger.Lines); Assert.DoesNotContain(config.Groq.ApiKey,logs); Assert.DoesNotContain("Return ok",logs); Assert.DoesNotContain("Only test data",logs);
    }

    [Theory]
    [InlineData("https://example.invalid")][InlineData("http://localhost:11434/path")]
    [InlineData("http://user:password@localhost:11434")][InlineData("file:///etc/passwd")]
    public void ArbitraryOllamaUrlsAreNotAllowed(string url)=>Assert.False(AiProviderOptions.ValidLocalUri(url));

    [Fact]
    public void KeysWithoutSelectionDoNotActivateAnExternalProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ai:Groq:ApiKey"]=Guid.NewGuid().ToString("N") }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        Assert.IsType<MockAiProvider>(services.GetRequiredService<IAiProvider>());
    }

    [Theory]
    [InlineData("{")][InlineData("{}")][InlineData("null")]
    public async Task InvalidWireResponsesAndIncompleteCompletionsAreSafe(string json)
    {
        using var handler = new Handler((_,_)=>Task.FromResult(Reply(json)));
        var adapter = Provider(Config("Groq"),handler,out var transport); using var _ = transport;
        Assert.Equal(AiErrorCode.InvalidResponse,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),CancellationToken.None))).Code);
    }
}
