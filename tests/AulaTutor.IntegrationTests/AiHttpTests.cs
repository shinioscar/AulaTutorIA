using System.Net;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AulaTutor.IntegrationTests;

// Real HTTP sockets to a controlled loopback server; no provider quota or external DNS.
public sealed class AiHttpTests
{
    private sealed class LoopbackHandler(Uri origin) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect=false, UseProxy=false, UseCookies=false })
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Host is not ("api.groq.com" or "generativelanguage.googleapis.com" or "localhost"))
                throw new InvalidOperationException("Unexpected test destination.");
            request.RequestUri = new Uri(origin, request.RequestUri.PathAndQuery);
            return base.SendAsync(request,ct);
        }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler,false) {Timeout=Timeout.InfiniteTimeSpan}; }

    [Theory]
    [InlineData("Groq",false)][InlineData("Gemini",false)][InlineData("Ollama",false)]
    [InlineData("Groq",true)][InlineData("Gemini",true)][InlineData("Ollama",true)]
    public async Task ControlledHttpServerVerifiesWireContractAndQuotaErrors(string provider, bool quotaError)
    {
        var builder=WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server=>server.Listen(IPAddress.Loopback,0));
        await using var app=builder.Build(); string? path=null; string? body=null; var calls=0;
        app.MapPost("/{**path}",async (HttpContext context)=>
        {
            calls++; path=context.Request.Path;
            using var reader=new StreamReader(context.Request.Body); body=await reader.ReadToEndAsync();
            context.Response.ContentType="application/json";
            if (quotaError) {context.Response.StatusCode=429;context.Response.Headers.RetryAfter="60"; await context.Response.WriteAsync("{\"secret\":\"discard upstream body\"}");return;}
            await context.Response.WriteAsync(provider switch
            {
                "Groq"=>"""{"choices":[{"message":{"content":"HTTP test ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":7,"completion_tokens":3}}""",
                "Gemini"=>"""{"candidates":[{"content":{"parts":[{"text":"HTTP test ok"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":7,"candidatesTokenCount":3}}""",
                _=>"""{"message":{"content":"HTTP test ok"},"done":true,"done_reason":"stop","prompt_eval_count":7,"eval_count":3}"""
            });
        });
        await app.StartAsync();
        var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var handler=new LoopbackHandler(new Uri(address));
        var config=new AiProviderOptions {Provider=provider,Groq=new(){Model="openai/gpt-oss-20b",ApiKey=Guid.NewGuid().ToString("N")},Gemini=new(){Model="gemini-3.8-flash",ApiKey=Guid.NewGuid().ToString("N")},Ollama=new(){Model="gpt-oss"}};
        var options=Options.Create(config);
        using var transport=new AiHttpTransport(new Factory(handler),options,NullLogger<AiHttpTransport>.Instance);
        IAiProvider adapter=provider switch {"Groq"=>new GroqAiProvider(transport,options),"Gemini"=>new GeminiAiProvider(transport,options),_=>new OllamaAiProvider(transport,options)};
        Assert.True((await adapter.CheckHealthAsync(CancellationToken.None)).IsHealthy); Assert.Equal(0,calls);
        var request=new AiChatRequest("http_test",[new("system","Fictitious diagnostic"),new("user","Return test result")],new("text"),Guid.NewGuid().ToString("N"),128);
        if (quotaError)
        {
            var error=await Assert.ThrowsAsync<AiProviderException>(()=>adapter.GenerateAsync(request,CancellationToken.None));
            Assert.Equal(AiErrorCode.RateLimited,error.Code); Assert.Equal(TimeSpan.FromSeconds(60),error.RetryAfter); Assert.DoesNotContain("discard upstream",error.ToString());
        }
        else
        {
            var result=await adapter.GenerateAsync(request,CancellationToken.None);
            Assert.Equal("HTTP test ok",result.Text); Assert.Equal(new AiTokenUsage(7,3,false),result.Usage);
        }
        Assert.Equal(1,calls);
        Assert.Equal(provider switch {"Groq"=>"/openai/v1/chat/completions","Gemini"=>"/v1beta/models/gemini-3.8-flash:generateContent",_=>"/api/chat"},path);
        using var sent=JsonDocument.Parse(body!);
        Assert.True(sent.RootElement.TryGetProperty(provider=="Gemini"?"contents":"messages",out _));
        Assert.DoesNotContain(config.Groq.ApiKey,body!); Assert.DoesNotContain(config.Gemini.ApiKey,body!);
        await app.StopAsync();
    }
}
