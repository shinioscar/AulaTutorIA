using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Application.Tutoring;
using AulaTutor.Infrastructure.Tutoring;
using AulaTutor.Application.Lessons;
using AulaTutor.Infrastructure.Lessons;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName))
            .Validate(options => options.IsValid(), "Configuración IA inválida: revisa proveedor, modelo, credencial y límites privados.")
            .ValidateOnStart();
        services.AddHttpClient("Ai", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddSingleton<AiHttpTransport>();
        services.AddOptions<AiQuotaOptions>().Bind(configuration.GetSection("AiQuota"))
            .Validate(p => p.Valid(), "Límites persistentes IA inválidos.").ValidateOnStart();
        services.AddSingleton<IAiCallAdmission, PersistentAiAdmission>();
        services.AddOptions<PersonalAiOptions>().Bind(configuration.GetSection("PersonalAi"))
            .Validate(p => p.Valid(), "Origen de autorización IA no válido.").ValidateOnStart();
        services.AddSingleton<PersonalAiVault>();
        services.AddSingleton<PersonalAiQaMode>();
        services.AddTransient<PersonalAiQaTransport>();
        services.AddHttpClient("AiMetadata", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }).RemoveAllLoggers();
        services.AddScoped<PersonalModelCatalog>();
        services.AddScoped<PersonalAiConnections>();
        services.AddScoped<PersonalAiOAuthService>();
        if (configuration.GetValue<bool>("PersonalAi:QaSimulation"))
        {
            services.AddHttpClient("Ai").ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<PersonalAiQaTransport>());
            services.AddHttpClient("AiMetadata").ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<PersonalAiQaTransport>());
        }
        services.AddScoped<IPersonalAiConnections>(p => p.GetRequiredService<PersonalAiConnections>());
        services.AddScoped<IAiProvider, PersonalAiProvider>();
        services.AddSingleton<InstitutionalAiProvider>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AiProviderOptions>>();
            IAiProvider selected = options.Value.Provider switch
            {
                "Mock" => new MockAiProvider(),
                "Groq" => ActivatorUtilities.CreateInstance<GroqAiProvider>(provider),
                "Gemini" => ActivatorUtilities.CreateInstance<GeminiAiProvider>(provider),
                "Ollama" => ActivatorUtilities.CreateInstance<OllamaAiProvider>(provider),
                _ => throw new InvalidOperationException("Proveedor IA no habilitado.")
            };
            return new InstitutionalAiProvider(selected);
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
        services.AddScoped<LessonOrchestrator>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<AssignedClassAccess>();
        services.AddScoped<AssignedClassWorkflow>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ISupervisionService, SupervisionService>();
        return services;
    }
}
