using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AulaTutor.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task AnonymousAuthenticationTrafficIsRateLimited()
    {
        await using var factory = TestHost.Create();
        using var client = TestHost.Client(factory);
        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new {email="rate@example.invalid",password="Fictitious!12345"})).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new {email="rate@example.invalid",password="Fictitious!12345"})).StatusCode);
    }
    [Fact]
    public async Task HealthResolvesMockAndOpenApiRetainsFoundation()
    {
        await using var factory = TestHost.Create();
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<ApiHealthResponse>("/api/health");
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("Mock", health.AiProvider);
        Assert.Equal(1, health.Iteration);

        using var schema = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = schema.RootElement.GetProperty("paths").EnumerateObject().ToArray();
        Assert.Contains(paths, path => path.Name == "/api/health");
        Assert.Contains(paths, path => path.Name == "/api/auth/login");
        Assert.Equal("apiKey", schema.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("sessionCookie").GetProperty("type").GetString());
        var meOperation = schema.RootElement.GetProperty("paths").GetProperty("/api/me").GetProperty("get");
        Assert.Single(meOperation.GetProperty("security").EnumerateArray());
        var loginOperation = schema.RootElement.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");
        Assert.Contains(loginOperation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "X-XSRF-TOKEN");
        Assert.True(loginOperation.GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(loginOperation.GetProperty("responses").TryGetProperty("401", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/lessons")).StatusCode);
    }

    [Fact]
    public async Task OpenApiIsUnavailableOutsideDevelopment()
    {
        await using var factory = TestHost.Create(environment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
