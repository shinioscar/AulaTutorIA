using AulaTutor.Api.Identity;
using AulaTutor.Application.Lessons;

namespace AulaTutor.Api.Lessons;

public static class LessonEndpoints
{
    public static void MapLessonEndpoints(this WebApplication app)
    {
        var lessons = app.MapGroup("/api/lessons").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        lessons.MapPost("/external-prompt", async (LessonExternalOptions r, ILessonService service, CancellationToken ct) => TypedResults.Ok(new { prompt = await service.ExternalPromptAsync(r, ct) }));
        lessons.MapPost("/import/preview", async (LessonImportInput r, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.PreviewImportAsync(r, ct)));
        lessons.MapPost("/import", async (CreateImportedLessonRequest r, ILessonService service, CancellationToken ct) => { var view = await service.ImportAsync(r, ct); return TypedResults.Created($"/api/lessons/{view.Id}", view); });
        lessons.MapGet("/", async (int? page, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(page ?? 1, ct)));
        lessons.MapPost("/", async (CreateLessonRequest r, ILessonService service, CancellationToken ct) => { var view = await service.CreateAsync(r, ct); return TypedResults.Created($"/api/lessons/{view.Id}", view); });
        lessons.MapGet("/{id:guid}", async (Guid id, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)));
        lessons.MapPut("/{id:guid}/draft", async (Guid id, SaveLessonRequest r, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.SaveAsync(id, r, ct)));
        foreach (var action in new[] { "apply-proposal", "discard-proposal", "review", "approve", "publish", "new-draft", "archive" })
            lessons.MapPost($"/{{id:guid}}/{action}", async (Guid id, LessonCommandRequest r, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.CommandAsync(id, action, r, ct)))
                .WithSummary($"{action}: decisión explícita del docente propietario con revisión y clave idempotente.");
        lessons.MapPost("/{id:guid}/design", async (Guid id, LessonAiRequest r, ILessonService service, CancellationToken ct) => { var view = await service.DesignAsync(id, r, ct); return Results.Json(view, statusCode: view.Operations.Any(o => o.RequestId == r.RequestId && o.IsGenerating) ? 202 : 200); }).Produces<LessonView>(200).Produces<LessonView>(202).RequireRateLimiting("lessons");
        lessons.MapPost("/{id:guid}/design/{requestId:guid}/cancel", async (Guid id, Guid requestId, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.CancelDesignAsync(id, requestId, ct)));
        lessons.MapPost("/{id:guid}/simulations", async (Guid id, LessonCommandRequest r, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.StartSimulationAsync(id, r, ct)));
        lessons.MapGet("/{id:guid}/simulations/{simulationId:guid}", async (Guid id, Guid simulationId, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.GetSimulationAsync(id, simulationId, ct)));
        lessons.MapPost("/{id:guid}/simulations/{simulationId:guid}/messages", async (Guid id, Guid simulationId, LessonAiRequest r, ILessonService service, CancellationToken ct) => { var view = await service.SimulateAsync(id, simulationId, r, ct); return Results.Json(view, statusCode: view.Operations.Any(o => o.RequestId == r.RequestId && o.IsGenerating) ? 202 : 200); }).Produces<LessonSimulationView>(200).Produces<LessonSimulationView>(202).RequireRateLimiting("lessons");
        foreach (var action in new[] { "stop", "resume", "cancel" })
            lessons.MapPost($"/{{id:guid}}/simulations/{{simulationId:guid}}/{action}", async (Guid id, Guid simulationId, LessonCommandRequest r, ILessonService service, CancellationToken ct) => TypedResults.Ok(await service.SimulationCommandAsync(id, simulationId, action, r, ct)));
    }
}
