using AulaTutor.Api.Identity;
using AulaTutor.Application.Ai;
using AulaTutor.Domain.Identity;

namespace AulaTutor.Api.Ai;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        var ai = app.MapGroup("/api/admin/ai").RequireAuthorization(p => p.RequireRole(RoleNames.Admin))
            .AddEndpointFilter<IdentityValidationFilter>();
        ai.MapGet("/", async (IAiDiagnostics service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(ct)))
            .WithSummary("Configuración IA y capacidades sin secretos ni llamadas externas; solo Admin activo.");
        ai.MapPost("/test", async (IAiDiagnostics service, CancellationToken ct) => TypedResults.Ok(await service.TestAsync(ct)))
            .RequireRateLimiting("ai-diagnostic")
            .WithSummary("Prueba explícita de texto fijo, máximo 128 tokens; exige Admin y CSRF. Puede consumir cuota del proveedor activo.");
    }
}
