using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace AulaTutor.IntegrationTests;

internal static class TestHost
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AulaTutorIA.slnx"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
    public static WebApplicationFactory<Program> Create(string? connection = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment)
            .UseSetting("ConnectionStrings:AulaTutor", connection ?? "Host=localhost;Database=not_used;Username=fictitious;Password=fictitious")
            .UseSetting("DataProtection:KeyPath", Path.Combine(Root, ".cache", "test-keys"))
            .UseSetting("Ai:Provider", "Mock")
            .UseSetting("Ai:Groq:ApiKey", "")
            .UseSetting("Ai:Gemini:ApiKey", "")
            .UseSetting("Logging:LogLevel:Default", "Warning")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            { ["Ai:Provider"]="Mock", ["Ai:Groq:ApiKey"]="", ["Ai:Gemini:ApiKey"]="" }))
            .ConfigureServices(services => { services.RemoveAll<IAiProvider>(); services.AddSingleton<IAiProvider,MockAiProvider>(); }));
    public static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
