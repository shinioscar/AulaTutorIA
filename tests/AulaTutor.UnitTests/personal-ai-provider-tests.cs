using System.Net;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AulaTutor.UnitTests;

public sealed class PersonalAiProviderTests
{
    private const string Key="qa_unit_fictitious_credential_only";
    private const string Schema="""{"type":"object","properties":{"status":{"type":"string","enum":["ok"]}},"required":["status"],"additionalProperties":false}""";
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;return action(request,ct);} }
    private sealed class Clients(HttpMessageHandler handler):IHttpClientFactory
    { public HttpClient CreateClient(string name)=>new(handler,false){Timeout=Timeout.InfiniteTimeSpan}; }
    private sealed class Admission:IAiCallAdmission
    {
        public int Calls; public readonly List<(string State,AiTokenUsage? Usage)> Results=[];
        public Task<Guid> ReserveAsync(string provider,string model,int estimate,CancellationToken ct){Calls++;return Task.FromResult(Guid.NewGuid());}
        public Task FinishAsync(Guid id,string state,AiTokenUsage? usage,long duration,TimeSpan? retryAfter=null){Results.Add((state,usage));return Task.CompletedTask;}
    }
    private static HttpResponseMessage Response(string provider,string text,string finish="stop")=>new(HttpStatusCode.OK){Content=provider=="Anthropic"?
        JsonContent(new{content=new[]{new{type="text",text}},stop_reason=finish=="stop"?"end_turn":finish,usage=new{input_tokens=7,output_tokens=3}}):
        JsonContent(new{choices=new[]{new{message=new{content=text},finish_reason=finish}},usage=new{prompt_tokens=7,completion_tokens=3}})};
    private static HttpContent JsonContent(object value)=>System.Net.Http.Json.JsonContent.Create(value);
    private static AiChatRequest Request()=>new("test",[new("system","Fictitious contract"),new("user","Return status")],new("json-schema",Schema,true),Guid.NewGuid().ToString("N"));

    [Theory][InlineData("OpenRouter")][InlineData("Anthropic")]
    public async Task FixedOfficialHostAuthenticationSchemaAndReportedUsage(string provider)
    {
        using var handler=new Handler(async(request,ct)=>{
            Assert.Equal(provider=="OpenRouter"?"https://openrouter.ai/api/v1/chat/completions":"https://api.anthropic.com/v1/messages",request.RequestUri!.ToString());
            var text=await request.Content!.ReadAsStringAsync(ct);Assert.DoesNotContain(Key,text);using var body=JsonDocument.Parse(text);
            if(provider=="OpenRouter"){Assert.Equal(Key,request.Headers.Authorization!.Parameter);Assert.False(body.RootElement.GetProperty("provider").GetProperty("allow_fallbacks").GetBoolean());Assert.Equal(0,body.RootElement.GetProperty("provider").GetProperty("max_price").GetProperty("request").GetInt32());Assert.Equal("json_schema",body.RootElement.GetProperty("response_format").GetProperty("type").GetString());}
            else{Assert.Equal(Key,request.Headers.GetValues("x-api-key").Single());Assert.Equal("2026-10-21",request.Headers.GetValues("anthropic-version").Single());Assert.Equal("wrkspc_fictitious",request.Headers.GetValues("anthropic-workspace-id").Single());Assert.Equal("json_schema",body.RootElement.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());}
            return Response(provider,"{\"status\":\"ok\"}");
        });
        using var transport=new AiHttpTransport(new Clients(handler),Options.Create(new AiProviderOptions()),NullLogger<AiHttpTransport>.Instance);
        var adapter=new PersonalRemoteAiProvider(transport,new(),provider,"fictitious-structured-model",Key,true,true,"wrkspc_fictitious");
        Assert.True((await adapter.CheckHealthAsync(default)).IsHealthy);Assert.Equal(0,handler.Calls);
        var result=await adapter.GenerateAsync(Request(),default);Assert.Equal(new AiTokenUsage(7,3,false),result.Usage);Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData("OpenRouter","truncated")][InlineData("Anthropic","truncated")][InlineData("OpenRouter","schema")][InlineData("Anthropic","schema")][InlineData("OpenRouter","echo")][InlineData("Anthropic","echo")]
    public async Task TruncationSchemaErrorsAndSecretEchoAreRejected(string provider,string scenario)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Response(provider,scenario=="echo"?JsonSerializer.Serialize(new{status=Key}):scenario=="schema"?"{\"status\":\"wrong\"}":"{\"status\":\"ok\"}",scenario=="truncated"?"max_tokens":"stop")));
        using var transport=new AiHttpTransport(new Clients(handler),Options.Create(new AiProviderOptions()),NullLogger<AiHttpTransport>.Instance);
        var adapter=new PersonalRemoteAiProvider(transport,new(),provider,"fictitious-model",Key,true,false);
        Assert.Equal(AiErrorCode.InvalidResponse,(await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(Request(),default))).Code);Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData("OpenRouter")][InlineData("Anthropic")]
    public async Task EachPhysicalRetryReservesAgainWithSameProviderAndModel(string provider)
    {
        var admission=new Admission();var calls=0;
        using var handler=new Handler((_,_)=>{
            if(++calls==1){var r=new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);r.Headers.RetryAfter=new(TimeSpan.Zero);return Task.FromResult(r);}
            return Task.FromResult(Response(provider,"{\"status\":\"ok\"}"));
        });
        var settings=new AiProviderOptions{MaxRetries=1};using var transport=new AiHttpTransport(new Clients(handler),Options.Create(settings),NullLogger<AiHttpTransport>.Instance,admission);
        await new PersonalRemoteAiProvider(transport,settings,provider,"fictitious-model",Key,true,false).GenerateAsync(Request(),default);
        Assert.Equal(2,handler.Calls);Assert.Equal(2,admission.Calls);Assert.Equal("Failed",admission.Results[0].State);Assert.Equal("Succeeded",admission.Results[1].State);
    }
    [Theory][InlineData("OpenRouter")][InlineData("Anthropic")]
    public async Task TimeoutKeepsUncertainChargeWithoutInventingUsage(string provider)
    {
        var admission=new Admission();using var handler=new Handler(async(_,ct)=>{await Task.Delay(Timeout.InfiniteTimeSpan,ct);return Response(provider,"");});
        var settings=new AiProviderOptions{TimeoutSeconds=1};using var transport=new AiHttpTransport(new Clients(handler),Options.Create(settings),NullLogger<AiHttpTransport>.Instance,admission);
        var error=await Assert.ThrowsAsync<AiProviderException>(()=>new PersonalRemoteAiProvider(transport,settings,provider,"fictitious-model",Key,true,false).GenerateAsync(Request(),default));
        Assert.Equal(AiErrorCode.Timeout,error.Code);Assert.Equal("Uncertain",Assert.Single(admission.Results).State);Assert.Null(admission.Results[0].Usage);Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData("0",true)][InlineData("0.01",false)][InlineData("unknown",false)][InlineData("-1",false)]
    public void FreeLabelRequiresVerifiedOfficialPriceRatherThanModelSuffix(string completion,bool free)
    {
        var json=JsonSerializer.Serialize(new{data=new[]{new{id="vendor/example:free",name="Fixture",context_length=65536,architecture=new{input_modalities=new[]{"text"},output_modalities=new[]{"text"}},top_provider=new{max_completion_tokens=8192},supported_parameters=new[]{"response_format","structured_outputs"},pricing=new{prompt="0",completion}}}});
        using var doc=JsonDocument.Parse(json);var model=Assert.Single(PersonalModelCatalog.ParseOpenRouter(doc.RootElement,DateTimeOffset.UtcNow));Assert.Equal(free,model.Free);Assert.True(model.Compatible);
    }
    [Theory]
    [InlineData("top_provider", "{\"max_completion_tokens\":null}")]
    [InlineData("top_provider", "{}")]
    [InlineData("top_provider", "null")]
    [InlineData("top_provider", "{\"max_completion_tokens\":\"8192\"}")]
    [InlineData("top_provider", "{\"max_completion_tokens\":2147483648}")]
    [InlineData("context_length", "null")]
    [InlineData("context_length", "\"65536\"")]
    public void UnknownTokenLimitsDoNotRejectOtherCatalogModels(string field, string value)
    {
        var valid = new Dictionary<string, object?> {
            ["id"] = "vendor/compatible", ["name"] = "Modelo ficticio", ["context_length"] = 65536,
            ["architecture"] = new { input_modalities = new[] { "text" }, output_modalities = new[] { "text" } },
            ["top_provider"] = new { max_completion_tokens = 8192 },
            ["supported_parameters"] = new[] { "response_format", "structured_outputs" },
            ["pricing"] = new { prompt = "0", completion = "0" }
        };
        using var unknown = JsonDocument.Parse(value);
        var incomplete = new Dictionary<string, object?>(valid) { ["id"] = "vendor/unknown-limit", [field] = unknown.RootElement };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { data = new[] { incomplete, valid } }));
        var models = PersonalModelCatalog.ParseOpenRouter(doc.RootElement, DateTimeOffset.UtcNow);
        Assert.Equal(2, models.Length);
        var model = models.Single(m => m.Id == "vendor/unknown-limit");
        Assert.False(model.Compatible); Assert.False(model.StrictSchema);
        Assert.True(models.Single(m => m.Id == "vendor/compatible").Compatible);
    }
}
