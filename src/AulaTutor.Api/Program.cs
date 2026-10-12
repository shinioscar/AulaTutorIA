using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Liveness and Mock availability only; no database readiness assertion.
app.MapGet("/api/health", async (IAiProvider provider, CancellationToken ct) =>
{
    var health = await provider.CheckHealthAsync(ct);
    return TypedResults.Ok(new ApiHealthResponse(
        "ok", "AulaTutor IA", 0, health.ProviderId, health.ModelId));
})
.WithName("GetHealth")
.WithSummary("Estado básico de la API y del proveedor simulado; no comprueba PostgreSQL.");

app.Run();

public sealed record ApiHealthResponse(
    string Status, string Application, int Iteration, string AiProvider, string AiModel);

public partial class Program;
