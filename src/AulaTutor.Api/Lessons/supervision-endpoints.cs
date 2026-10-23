using AulaTutor.Api.Identity;
using AulaTutor.Application.Lessons;

namespace AulaTutor.Api.Lessons;

public static class SupervisionEndpoints
{
    public static void MapSupervisionEndpoints(this WebApplication app)
    {
        var teacher = app.MapGroup("/api/teacher/supervision").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        teacher.MapGet("/", async (Guid? groupId, ISupervisionService service, CancellationToken ct) => TypedResults.Ok(await service.DashboardAsync(groupId, ct)));
        teacher.MapGet("/assignments/{id:guid}", async (Guid id, ISupervisionService service, CancellationToken ct) => TypedResults.Ok(await service.ClassAsync(id, ct)));
        teacher.MapGet("/assignments/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, ISupervisionService service, CancellationToken ct) => TypedResults.Ok(await service.IndividualAsync(id, studentId, ct)));
        teacher.MapPost("/assignments/{id:guid}/students/{studentId:guid}/reviews", async (Guid id, Guid studentId, ReviewRequest request, ISupervisionService service, CancellationToken ct) => TypedResults.Ok(await service.ReviewAsync(id, studentId, request, ct)))
            .WithSummary("Añade decisión humana con historial, referencias, revisión e idempotencia; nunca cambia evidencia primaria.");
        app.MapGet("/api/students/me/classes/{id:guid}/feedback", async (Guid id, ISupervisionService service, CancellationToken ct) => TypedResults.Ok(await service.FeedbackAsync(id, ct)))
            .RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
    }
}
