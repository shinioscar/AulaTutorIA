using AulaTutor.Api.Identity;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Api;

public static class OperationalEndpoints
{
    public static void MapOperationalEndpoints(this WebApplication app)
    {
        var operations = app.MapGroup("/api/admin/operations").RequireAuthorization(p => p.RequireRole(RoleNames.Admin))
            .AddEndpointFilter<IdentityValidationFilter>();
        operations.MapGet("/audit", async (int? page, ICurrentActor current, AulaTutorDbContext db, CancellationToken ct) =>
        {
            if (!(await current.GetAsync()).IsAdmin) throw new AppException(403, "Se requiere un administrador activo.");
            var index = page ?? 1;
            if (index is < 1 or > 10000) throw new AppException(400, "Página inválida.");
            return TypedResults.Ok(await db.AuditEvents.AsNoTracking().OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .Skip((index - 1) * 50).Take(50).Select(e => new { e.Id, e.ActorId, e.CreatedAt, e.Type, e.ResourceId, e.Result, e.CorrelationId, e.Change }).ToArrayAsync(ct));
        });
        operations.MapGet("/usage", async (ICurrentActor current, AulaTutorDbContext db, CancellationToken ct) =>
        {
            if (!(await current.GetAsync()).IsAdmin) throw new AppException(403, "Se requiere un administrador activo.");
            var now = DateTimeOffset.UtcNow; var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var rows = await db.AiReservations.AsNoTracking().Where(r => r.CreatedAt >= day).ToArrayAsync(ct);
            return TypedResults.Ok(new { sinceUtc = day, providers = rows.GroupBy(r => new { r.Scope, r.Provider }).Select(g => new {
                scope = g.Key.Scope, provider = g.Key.Provider, reservations = g.Count(), succeeded = g.Count(r => r.State == "Succeeded"), failed = g.Count(r => r.State == "Failed"),
                uncertain = g.Count(r => r.State == "Uncertain" || r.State == "Reserved" && r.LeaseUntil <= now),
                active = g.Count(r => r.LeaseUntil > now && r.State is "Reserved" or "Uncertain"),
                unknownUsage = g.Count(r => !r.InputTokens.HasValue || !r.OutputTokens.HasValue),
                reportedInputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)), reportedOutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0)),
                estimatedAllowance = g.Sum(r => (long)r.EstimatedTokens) }).ToArray() });
        });
        app.MapGet("/api/admin/operations/readiness", async (ICurrentActor current,
            AulaTutorDbContext db, HttpContext context, CancellationToken ct) =>
        {
            var actor = await current.GetAsync();
            if (!actor.IsAdmin) throw new AppException(403, "Se requiere un administrador activo.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            bool ready;
            try { ready = await db.Database.CanConnectAsync(deadline.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { ready = false; }
            // Connectivity only. Never call an AI provider or report its availability.
            return Results.Json(new { status = ready ? "ready" : "unavailable", database = ready,
                aiAvailabilityChecked = false, correlationId = context.TraceIdentifier }, statusCode: ready ? 200 : 503);
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin))
            .AddEndpointFilter<IdentityValidationFilter>()
            .WithSummary("Conectividad PostgreSQL, solo Admin activo; no valida migraciones, cuota ni disponibilidad IA.");
    }
}
