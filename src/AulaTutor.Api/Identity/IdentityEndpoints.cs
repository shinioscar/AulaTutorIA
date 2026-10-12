using System.ComponentModel.DataAnnotations;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using Microsoft.AspNetCore.Antiforgery;

namespace AulaTutor.Api.Identity;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().AddEndpointFilter<IdentityValidationFilter>();
        api.MapGet("/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!context.Request.IsHttps) return Results.Problem(statusCode: 400, title: "Se requiere HTTPS.");
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true
            });
            return Results.Ok(new CsrfResponse(tokens.RequestToken!));
        }).AllowAnonymous().WithSummary("Emite token CSRF para el usuario actual. Renovar después de login/logout.");

        api.MapPost("/auth/register", async (RegisterRequest request, IIdentityService identity) =>
        {
            await identity.RegisterAsync(request);
            return Results.Accepted(value: new { message = "Solicitud recibida. Puedes intentar iniciar sesión; las solicitudes docentes requieren aprobación." });
        }).AllowAnonymous().RequireRateLimiting("auth").WithSummary("Registro Student o solicitud Teacher pendiente; respuesta uniforme para cuentas existentes.");
        api.MapPost("/auth/login", async (LoginRequest request, IIdentityService identity) =>
            await identity.LoginAsync(request) ? Results.NoContent() : Results.Problem(statusCode: 401, title: "No se pudo iniciar sesión con las credenciales proporcionadas."))
            .AllowAnonymous().RequireRateLimiting("auth").WithSummary("Inicio mediante cookie Secure/HttpOnly; bloqueo tras 5 fallos y límite por IP.");
        api.MapPost("/auth/logout", async (IIdentityService identity) =>
        {
            await identity.LogoutAsync(); return Results.NoContent();
        }).RequireAuthorization().WithSummary("Cierra sesión e invalida las cookies anteriores de la cuenta.");
        api.MapGet("/me", async (IIdentityService identity) => TypedResults.Ok(await identity.MeAsync()))
            .RequireAuthorization().WithSummary("Perfil, roles y estado de aprobación de la sesión activa.");
        api.MapGet("/users/{id:guid}", async (Guid id, IIdentityService identity) => TypedResults.Ok(await identity.GetUserAsync(id)))
            .RequireAuthorization().WithSummary("Perfil propio o administración; oculta recursos ajenos con 404.");

        var admin = api.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("/users", async (int? page, IIdentityService identity) => TypedResults.Ok(await identity.ListUsersAsync(page ?? 1)))
            .WithSummary("Lista paginada de usuarios de la organización piloto, solo Admin.");
        admin.MapPost("/users/{id:guid}/teacher-decision", async (Guid id, TeacherDecisionRequest request, IIdentityService identity) =>
        { await identity.DecideTeacherAsync(id, request.Approve); return Results.NoContent(); })
            .WithSummary("Aprueba o rechaza una solicitud docente; revoca sesiones previas.");
        admin.MapPut("/users/{id:guid}/roles", async (Guid id, RolesRequest request, IIdentityService identity) =>
        { await identity.ChangeRolesAsync(id, request.Roles); return Results.NoContent(); })
            .WithSummary("Gestiona roles válidos; Teacher exige aprobación y se protege al último Admin.");
        admin.MapPut("/users/{id:guid}/status", async (Guid id, AccountStatusRequest request, IIdentityService identity) =>
        { await identity.ChangeStatusAsync(id, request.IsActive); return Results.NoContent(); })
            .WithSummary("Activa/desactiva cuenta con revocación de sesiones y protección del administrador.");

        var groups = api.MapGroup("/groups").RequireAuthorization();
        groups.MapGet("/", async (IGroupService service) => TypedResults.Ok(await service.ListAsync()))
            .WithSummary("Grupos propios por membresía/asignación; Admin ve los grupos del piloto.");
        groups.MapGet("/{id:guid}", async (Guid id, IGroupService service) => TypedResults.Ok(await service.GetAsync(id)))
            .WithSummary("Consulta de grupo autorizada por pertenencia, además del rol.");
        groups.MapGet("/{id:guid}/members", async (Guid id, IGroupService service) => TypedResults.Ok(await service.MembersAsync(id)))
            .WithSummary("Membresías mínimas, solo Admin o Teacher aprobado asignado; no concede acceso a chats.");
        groups.MapPost("/", async (GroupRequest request, IGroupService service) =>
        { var group = await service.CreateAsync(request.Name); return TypedResults.Created($"/api/groups/{group.Id}", group); })
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin)).WithSummary("Crea grupo del piloto, solo Admin.");
        groups.MapPut("/{id:guid}", async (Guid id, GroupRequest request, IGroupService service) =>
        { await service.RenameAsync(id, request.Name); return Results.NoContent(); })
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin)).WithSummary("Renombra grupo, solo Admin.");
        groups.MapDelete("/{id:guid}", async (Guid id, IGroupService service) =>
        { await service.ArchiveAsync(id); return Results.NoContent(); })
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin)).WithSummary("Archiva sin borrar membresías ni futura evidencia, solo Admin.");
        groups.MapPut("/{id:guid}/members", async (Guid id, MembershipRequest request, IGroupService service) =>
        { await service.AddMemberAsync(id, request); return Results.NoContent(); })
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin)).WithSummary("Asocia Student o Teacher aprobado de la misma organización; idempotente.");
        groups.MapDelete("/{id:guid}/members/{userId:guid}/{kind}", async (Guid id, Guid userId, string kind, IGroupService service) =>
        { await service.RemoveMemberAsync(id, userId, kind); return Results.NoContent(); })
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin)).WithSummary("Retira membresía, solo Admin; acceso docente desaparece inmediatamente.");
    }
}

public sealed record CsrfResponse(string RequestToken);

public sealed class IdentityValidationFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            if (!http.Request.IsHttps) return Results.Problem(statusCode: 400, title: "Se requiere HTTPS.");
            try { await antiforgery.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException) { return Results.Problem(statusCode: 400, title: "CSRF no válido. Actualiza la página e inténtalo nuevamente."); }
        }
        foreach (var argument in context.Arguments)
        {
            if (argument is not (RegisterRequest or LoginRequest or RolesRequest or GroupRequest or MembershipRequest)) continue;
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(argument, new ValidationContext(argument), errors, true))
                return Results.Problem(statusCode: 400, title: "Revisa el formato y los límites de los campos.");
        }
        return await next(context);
    }
}
