using AulaTutor.Api.Identity;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Api.Tutoring;

public static class TutoringEndpoints
{
    public static void MapTutoringEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/api/tutoring-sessions").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        sessions.MapPost("/{id:guid}/cards", async (Guid id, TutoringTurnRequest request, ITutoringService service, CancellationToken ct) => TypedResults.Ok(await service.SendCardAsync(id, request, ct)))
            .WithSummary("Acciones locales exclusivamente para clases precargadas autorizadas; sin IA ni evaluación final en esta ruta.");
        sessions.MapPost("/", async (CreateTutoringRequest request, ITutoringService service, CancellationToken ct) =>
        { var view = await service.CreateAsync(request, ct); return TypedResults.Created($"/api/tutoring-sessions/{view.Id}", view); })
            .WithSummary("Crea tutoría privada Student con clave idempotente; no genera IA ni admite studentId del cliente.");
        app.MapGet("/api/students/me/tutoring-sessions", async (int? page, ITutoringService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(page ?? 1, ct)))
            .RequireAuthorization().WithSummary("Solo tutorías propias del estudiante, páginas de 50.");
        sessions.MapGet("/{id:guid}", async (Guid id, ITutoringService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithSummary("Historial propio; clases asignadas exigen además relación académica vigente. Otros estudiantes, Teacher y Admin reciben 404.");
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
        sessions.MapGet("/{id:guid}/share-recipients", async (Guid id, ILearningService service, CancellationToken ct) => TypedResults.Ok(await service.RecipientsAsync(id, ct)))
            .WithSummary("Docentes aprobados con relación de grupo vigente; solo propietario.");
        sessions.MapPost("/{id:guid}/shares", async (Guid id, ShareReportRequest request, ILearningService service, CancellationToken ct) => TypedResults.Ok(await service.ShareAsync(id, request, ct)))
            .WithSummary("Autoriza expresamente una versión de reporte; nunca comparte historial, respuestas ni soluciones.");
        sessions.MapDelete("/{id:guid}/shares/{shareId:guid}", async (Guid id, Guid shareId, ILearningService service, CancellationToken ct) => { await service.RevokeAsync(id, shareId, ct); return TypedResults.NoContent(); });
        sessions.MapPost("/{id:guid}/recommendations/{recommendationId:guid}/start", async (Guid id, Guid recommendationId, ReinforcementRequest request, ILearningService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ReinforceAsync(id, recommendationId, request, ct)))
            .WithSummary("Crea refuerzo relacionado sin mezclar historiales ni generar IA automáticamente.");
        var teacher = app.MapGroup("/api/teacher/reports").RequireAuthorization();
        teacher.MapGet("/", async (ILearningService service, CancellationToken ct) => TypedResults.Ok(await service.SharedReportsAsync(ct)));
        teacher.MapGet("/{id:guid}", async (Guid id, ILearningService service, CancellationToken ct) => TypedResults.Ok(await service.SharedReportAsync(id, ct)))
            .WithSummary("Solo reporte expresamente compartido y relación vigente; revocación impide nuevas consultas.");
    }
}
