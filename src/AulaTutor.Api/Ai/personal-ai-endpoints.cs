using AulaTutor.Api.Identity;
using AulaTutor.Application.Ai;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Ai;

namespace AulaTutor.Api.Ai;

public static class PersonalAiEndpoints
{
    public static void MapPersonalAiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai-connections").RequireAuthorization(p => p.RequireRole(RoleNames.Student, RoleNames.Teacher))
            .AddEndpointFilter<IdentityValidationFilter>().RequireRateLimiting("ai-connections");
        group.MapGet("/", async (IPersonalAiConnections service, CancellationToken ct) => Results.Ok(await service.GetAsync(ct)));
        group.MapPost("/manual", async (ManualAiRequest request, IPersonalAiConnections service, CancellationToken ct) => { await service.SaveAsync(request, ct); return Results.NoContent(); });
        group.MapPut("/selection", async (SelectAiRequest request, IPersonalAiConnections service, CancellationToken ct) => { await service.SelectAsync(request, ct); return Results.NoContent(); });
        group.MapDelete("/{id:guid}", async (Guid id, IPersonalAiConnections service, CancellationToken ct) => { await service.DisconnectAsync(id, ct); return Results.NoContent(); });
        group.MapGet("/models/{provider}", async (string provider, IPersonalAiConnections service, CancellationToken ct) => Results.Ok(await service.ModelsAsync(provider, false, ct)));
        group.MapPost("/models/{provider}/refresh", async (string provider, IPersonalAiConnections service, CancellationToken ct) => Results.Ok(await service.ModelsAsync(provider, true, ct)));
        group.MapPost("/openrouter/start", async (PersonalAiOAuthService service, CancellationToken ct) => Results.Ok(await service.StartAsync(ct))).RequireRateLimiting("ai-oauth-start");
        group.MapPost("/openrouter/complete", async (PersonalAiOAuthService service, CancellationToken ct) => { await service.CompleteAsync(ct); return Results.NoContent(); });
        group.MapPost("/openrouter/cancel", async (PersonalAiOAuthService service, CancellationToken ct) => { await service.CancelBrowserAsync(ct); return Results.NoContent(); });
        // Cross-site callback uses only the short-lived transaction cookie; no session authorization is relaxed.
        app.MapGet("/api/ai-connections/openrouter/callback", async (HttpContext http, PersonalAiOAuthService service, CancellationToken ct) =>
            Results.Redirect(await service.CallbackAsync(
                http.Request.Query["state"].Count == 1 ? http.Request.Query["state"].ToString() : null,
                http.Request.Query["code"].Count == 1 ? http.Request.Query["code"].ToString() : null, ct)))
            .AllowAnonymous().RequireRateLimiting("auth");
    }
}
