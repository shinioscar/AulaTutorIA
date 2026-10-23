using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AulaTutor.IntegrationTests;

public sealed class OperationalTests
{
    [Fact] public async Task OversizedInputIsRejectedBeforeDeserializationOrIdentityAccess()
    {
        await using var factory = TestHost.Create(); using var client = TestHost.Client(factory);
        using var response = await client.PostAsync("/api/auth/login", new StringContent(new string('x', 1_048_577)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("correlationId", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task MissingDatabaseReturnsSafeRecoverableErrorBeforeIdentityQuery()
    {
        var configured = Environment.GetEnvironmentVariable("AULATUTOR_TEST_CONNECTION") ?? throw new InvalidOperationException("Real project PostgreSQL connection required.");
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(configured) { Database = "aulatutor_test_missing_" + Guid.NewGuid().ToString("N"), Timeout = 1 };
        await using var factory = TestHost.Create(connection.ConnectionString); using var client = TestHost.Client(factory);
        var csrf = await client.GetFromJsonAsync<AulaTutor.Api.Identity.CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf!.RequestToken);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.invalid", password = "Fictitious!12345" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(connection.Database, text); Assert.DoesNotContain("Password", text);
        Assert.Contains("correlationId", text);
    }
    [Fact]
    public async Task LivenessDoesNotNeedDatabaseAndCorrelationIsGeneratedByServer()
    {
        await using var factory = TestHost.Create();
        using var client = TestHost.Client(factory);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "client-controlled-identifier");
        using var first = await client.GetAsync("/api/health");
        using var second = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var id = first.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParseExact(id, "N", out _));
        Assert.NotEqual(id, second.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("no-referrer", first.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", first.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", first.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(first.Headers.CacheControl!.NoStore);
        var health = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, health.GetProperty("iteration").GetInt32());
        Assert.Equal("Mock", health.GetProperty("aiProvider").GetString());
    }

    [Fact]
    public async Task ProblemDetailsIncludesSafeCorrelationWithoutEchoingInput()
    {
        await using var factory = TestHost.Create();
        using var client = TestHost.Client(factory);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "private@example.invalid", password = "never-log-this" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("never-log-this", body.ToString());
        Assert.DoesNotContain("private@example.invalid", body.ToString());
    }
}
