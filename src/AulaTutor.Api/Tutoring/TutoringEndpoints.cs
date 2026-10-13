using AulaTutor.Api.Identity;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Api.Tutoring;

public static class TutoringEndpoints
{
    public static void MapTutoringEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/api/tutoring-sessions").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        sessions.MapPost("/", async (CreateTutoringRequest request, ITutoringService service, CancellationToken ct) =>
        { var view = await service.CreateAsync(request, ct); return TypedResults.Created($"/api/tutoring-sessions/{view.Id}", view); })
            .WithSummary("Crea tutoría privada Student con clave idempotente; no genera IA ni admite studentId del cliente.");
        app.MapGet("/api/students/me/tutoring-sessions", async (int? page, ITutoringService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(page ?? 1, ct)))
            .RequireAuthorization().WithSummary("Solo tutorías propias del estudiante, páginas de 50.");
        sessions.MapGet("/{id:guid}", async (Guid id, ITutoringService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithSummary("Historial privado propio; otros estudiantes, Teacher y Admin sin acceso ordinario reciben 404.");
        sessions.MapPost("/{id:guid}/messages", async (Guid id, TutoringTurnRequest request, ITutoringService service, CancellationToken ct) =>
        {
            var view = await service.SendAsync(id, request, ct);
            return Results.Json(view, statusCode: view.Turns.Any(t => t.RequestId == request.RequestId && t.IsGenerating) ? 202 : 200);
        }).Produces<TutoringSessionView>(200).Produces<TutoringSessionView>(202).RequireRateLimiting("tutoring")
            .WithSummary("Turno idempotente Start/Message/Hint/Simplify: intento persistido antes de IA; Retry explícito con misma clave/contenido.");
        foreach (var action in new[] { "pause", "resume", "complete" })
        {
            sessions.MapPost($"/{{id:guid}}/{action}", async (Guid id, TutoringTransitionRequest request, ITutoringService service, CancellationToken ct) =>
                TypedResults.Ok(await service.TransitionAsync(id, action, request.Revision, ct)))
                .WithSummary($"{action}: transición propia con revisión; complete genera resumen descriptivo local sin cuota IA.");
        }
        sessions.MapPost("/{id:guid}/turns/{requestId:guid}/cancel", async (Guid id, Guid requestId, TutoringTransitionRequest request, ITutoringService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CancelAsync(id, requestId, request.Revision, ct)))
            .WithSummary("Cancela turno propio conservando intento y descarta respuesta tardía; no garantiza detener facturación externa.");
    }
}
