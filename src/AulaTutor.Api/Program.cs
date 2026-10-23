using System.Threading.RateLimiting;
using AulaTutor.Api;
using AulaTutor.Api.Identity;
using AulaTutor.Api.Ai;
using AulaTutor.Api.Tutoring;
using AulaTutor.Api.Lessons;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Infrastructure;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(options => options.DescribeIdentity());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAcademicIdentity(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.Name = "__Host-AulaTutor.Antiforgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(keyPath))
    throw new InvalidOperationException("DataProtection:KeyPath es obligatorio y debe apuntar a almacenamiento privado persistente.");
var protection = builder.Services.AddDataProtection().SetApplicationName("AulaTutorIA").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("lessons", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("tutoring", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("ai-diagnostic", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
// Explicit maintenance commands, never an automatic account seed at web startup.
if (args.Contains("--migrate") || args.Contains("--bootstrap-admin"))
{
    await using var scope = app.Services.CreateAsyncScope();
    if (args.Contains("--migrate")) await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().Database.MigrateAsync();
    if (args.Contains("--bootstrap-admin"))
    {
        var created = await scope.ServiceProvider.GetRequiredService<AdminBootstrap>().FromConfigurationAsync(builder.Configuration);
        Console.WriteLine(created ? "Administrador inicial creado." : "Administrador inicial ya existente; sin cambios.");
    }
    return;
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    // Server generated: never trust a client header as a log/correlation identifier.
    context.TraceIdentifier = Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    context.Response.Headers.CacheControl = "no-cache, no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.ContentLength > 1_048_576)
    {
        await Results.Problem(statusCode: 413, title: "La solicitud supera el tamaño máximo permitido.").ExecuteAsync(context);
        return;
    }
    try { await next(context); }
    catch (AppException error) { await Results.Problem(statusCode: error.Status, title: error.Message).ExecuteAsync(context); }
    catch (DbUpdateException) { await Results.Problem(statusCode: 409, title: "No se pudo guardar el cambio. Revisa los datos o actualiza la página.").ExecuteAsync(context); }
    catch (Npgsql.NpgsqlException) { await Results.Problem(statusCode: 503, title: "El almacenamiento no está disponible temporalmente. Conserva tu solicitud y consulta su estado antes de reintentar.").ExecuteAsync(context); }
    finally
    {
        if (context.Response.StatusCode is 400 or 401 or 403 or 429 or 503)
            app.Logger.LogWarning("Operational rejection {Status} {CorrelationId}", context.Response.StatusCode, context.TraceIdentifier);
    }
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Preserve the foundation liveness contract; no database readiness assertion.
app.MapGet("/api/health", async (IAiProvider provider, CancellationToken ct) =>
{
    var health = await provider.CheckHealthAsync(ct);
    return TypedResults.Ok(new ApiHealthResponse("ok", "AulaTutor IA", 1, health.ProviderId, health.ModelId));
}).WithName("GetHealth").WithSummary("Estado básico de la API y configuración IA; sin llamadas al proveedor ni comprobación PostgreSQL.").AllowAnonymous();
app.MapIdentityEndpoints();
app.MapAiEndpoints();
app.MapTutoringEndpoints();
app.MapLessonEndpoints();
app.MapAssignmentEndpoints();
app.MapSupervisionEndpoints();
app.MapOperationalEndpoints();
app.Run();

public sealed record ApiHealthResponse(string Status, string Application, int Iteration, string AiProvider, string AiModel);
public partial class Program;
