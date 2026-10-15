using AulaTutor.Api.Identity;
using AulaTutor.Application.Lessons;

namespace AulaTutor.Api.Lessons;

public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this WebApplication app)
    {
        var teacher = app.MapGroup("/api/teacher/assignments").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        teacher.MapGet("/published-versions", async (IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.PublishedAsync(ct)));
        teacher.MapGet("/authorized-groups", async (IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.GroupsAsync(ct)));
        teacher.MapGet("/", async (IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)));
        teacher.MapGet("/{id:guid}", async (Guid id, IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)));
        teacher.MapPost("/", async (CreateAssignmentRequest r, IAssignmentService service, CancellationToken ct) =>
        { var view = await service.CreateAsync(r, ct); return TypedResults.Created($"/api/teacher/assignments/{view.Id}", view); })
            .WithSummary("Asigna versión publicada propia a grupo docente vigente; creación idempotente, sin IA.");
        foreach (var action in new[] { "schedule", "deactivate", "activate", "withdraw" })
            teacher.MapPost($"/{{id:guid}}/{action}", async (Guid id, AssignmentCommandRequest r, IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.CommandAsync(id, action, r, ct)));
        var student = app.MapGroup("/api/students/me/classes").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        student.MapGet("/", async (IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.StudentListAsync(ct)));
        student.MapGet("/{id:guid}", async (Guid id, IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.StudentGetAsync(id, ct)));
        student.MapPost("/{id:guid}/start", async (Guid id, StartAssignedClassRequest r, IAssignmentService service, CancellationToken ct) => TypedResults.Ok(await service.StartAsync(id, r, ct)))
            .WithSummary("Recupera o crea sesión única por asignación/alumno. Comenzar conversación es un turno explícito del motor existente, sin IA al recargar.");
    }
}
