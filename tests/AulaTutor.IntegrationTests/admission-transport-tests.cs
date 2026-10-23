using System.Net;
using System.Text;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AulaTutor.IntegrationTests;

public sealed class AdmissionTransportTests
{
    private sealed class Handler(Func<int, CancellationToken, Task<HttpResponseMessage>> run) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => run(Interlocked.Increment(ref Calls), ct);
    }
    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan }; }
    private sealed class Admission : IAiCallAdmission
    {
        public int Reserved; public bool Reject; public List<(string State, AiTokenUsage? Usage, TimeSpan? Retry)> Results = [];
        public Task<Guid> ReserveAsync(string p, string m, int estimate, CancellationToken ct)
        { if (Reject) throw new AiProviderException(AiErrorCode.RateLimited, TimeSpan.FromMinutes(1)); Reserved++; return Task.FromResult(Guid.NewGuid()); }
        public Task FinishAsync(Guid id, string state, AiTokenUsage? usage, long ms, TimeSpan? retryAfter = null)
        { Results.Add((state, usage, retryAfter)); return Task.CompletedTask; }
    }
    private static HttpResponseMessage Success(string body = "{\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}") => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static AiHttpTransport Transport(Handler handler, Admission admission, int retries = 0, int timeout = 2) =>
        new(new Clients(handler), Options.Create(new AiProviderOptions { TimeoutSeconds = timeout, MaxRetries = retries }), NullLogger<AiHttpTransport>.Instance, admission);
    private static HttpRequestMessage Request() => new(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");

    [Fact] public async Task AdmissionRejectsBeforeAnyPhysicalHttpAttempt()
    {
        var a = new Admission { Reject = true }; using var h = new Handler((_, _) => Task.FromResult(Success())); using var t = Transport(h, a);
        Assert.Equal(AiErrorCode.RateLimited, (await Assert.ThrowsAsync<AiProviderException>(() => t.SendAsync(Request, "Groq", "fake", CancellationToken.None))).Code);
        Assert.Equal(0, h.Calls); Assert.Empty(a.Results);
    }
    [Fact] public async Task RetryRequiresAnotherReservationAndDoesNotHide429()
    {
        var a = new Admission(); using var h = new Handler((call, _) => {
            if (call > 1) return Task.FromResult(Success());
            var result = new HttpResponseMessage(HttpStatusCode.TooManyRequests); result.Headers.RetryAfter = new(TimeSpan.Zero); return Task.FromResult(result);
        }); using var t = Transport(h, a, retries: 1);
        using var response = await t.SendAsync(Request, "Groq", "fake", CancellationToken.None);
        Assert.Equal(2, h.Calls); Assert.Equal(2, a.Reserved); Assert.Equal("Failed", a.Results[0].State); Assert.Equal(TimeSpan.Zero, a.Results[0].Retry);
        Assert.Equal("Succeeded", a.Results[1].State); Assert.Equal(new AiTokenUsage(7, 3, false), a.Results[1].Usage);
    }
    [Theory][InlineData(401)][InlineData(403)][InlineData(500)][InlineData(429)]
    public async Task RejectionsRemainChargedAndDoNotAutomaticallyRetry(int status)
    {
        var a = new Admission(); using var h = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))); using var t = Transport(h, a);
        await Assert.ThrowsAsync<AiProviderException>(() => t.SendAsync(Request, "Groq", "fake", CancellationToken.None));
        Assert.Equal(1, h.Calls); var finished = Assert.Single(a.Results); Assert.Equal("Failed", finished.State); Assert.Null(finished.Usage);
        if (status == 429) Assert.Equal(TimeSpan.FromSeconds(60), finished.Retry);
    }
    [Theory][InlineData("invalid")][InlineData("disconnect")][InlineData("timeout")][InlineData("cancel")]
    public async Task UncertainResultsKeepTheirReservationWithoutInventingUsage(string mode)
    {
        var a = new Admission(); using var cancel = new CancellationTokenSource();
        using var h = new Handler(async (_, ct) => {
            if (mode == "invalid") return Success("incomplete-json");
            if (mode == "disconnect") throw new HttpRequestException("fictitious connection loss");
            if (mode == "cancel") cancel.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Success();
        }); using var t = Transport(h, a, timeout: 1);
        if (mode == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t.SendAsync(Request, "Groq", "fake", cancel.Token));
        else await Assert.ThrowsAsync<AiProviderException>(() => t.SendAsync(Request, "Groq", "fake", cancel.Token));
        Assert.Equal(1, h.Calls); var finished = Assert.Single(a.Results); Assert.Equal("Uncertain", finished.State); Assert.Null(finished.Usage);
    }
}
