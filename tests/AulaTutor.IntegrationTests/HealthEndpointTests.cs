using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AulaTutor.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task HealthResolvesMockAndOpenApiDocumentsOnlyHealth()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<ApiHealthResponse>("/api/health");
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("Mock", health.AiProvider);
        Assert.Equal(0, health.Iteration);

        using var schema = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = schema.RootElement.GetProperty("paths").EnumerateObject().ToArray();
        Assert.Single(paths);
        Assert.Equal("/api/health", paths[0].Name);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/tutoring-sessions")).StatusCode);
    }

    [Fact]
    public async Task OpenApiIsUnavailableOutsideDevelopment()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
