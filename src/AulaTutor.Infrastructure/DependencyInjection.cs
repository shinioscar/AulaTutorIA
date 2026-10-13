using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Application.Tutoring;
using AulaTutor.Infrastructure.Tutoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName))
            .Validate(options => options.IsValid(), "Configuración IA inválida: revisa proveedor, modelo, credencial y límites privados.")
            .ValidateOnStart();
        services.AddHttpClient("Ai", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddSingleton<AiHttpTransport>();
        services.AddSingleton<IAiProvider>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AiProviderOptions>>();
            return options.Value.Provider switch
            {
                "Mock" => new MockAiProvider(),
                "Groq" => ActivatorUtilities.CreateInstance<GroqAiProvider>(provider),
                "Gemini" => ActivatorUtilities.CreateInstance<GeminiAiProvider>(provider),
                "Ollama" => ActivatorUtilities.CreateInstance<OllamaAiProvider>(provider),
                _ => throw new InvalidOperationException("Proveedor IA no habilitado.")
            };
        });
        services.AddScoped<IAiDiagnostics, AiDiagnostics>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TutoringOrchestrator>();
        services.AddOptions<LearningPolicy>().Bind(configuration.GetSection("Learning"))
            .Validate(p => p.IsValid(), "Límites de diagnóstico inválidos.").ValidateOnStart();
        services.AddScoped<LearningOrchestrator>();
        services.AddScoped<LearningWorkflow>();
        services.AddScoped<ILearningService, LearningService>();
        services.AddScoped<ITutoringService, TutoringService>();
        return services;
    }
}
