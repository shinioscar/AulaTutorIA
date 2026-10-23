using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AulaTutor.Api.Identity;

public static class IdentityOpenApi
{
    public static void DescribeIdentity(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["sessionCookie"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie,
                Name = "__Host-AulaTutor.Session", Description = "Cookie Secure/HttpOnly emitida por login HTTPS; no bearer tokens."
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, context, ct) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security = [new OpenApiSecurityRequirement
                { [new OpenApiSecuritySchemeReference("sessionCookie", context.Document)] = [] }];
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Sin sesión válida." });
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Permisos insuficientes." });
                operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Recurso no disponible o relación no autorizada." });
            }
            if (context.Description.HttpMethod is "POST" or "PUT" or "DELETE" or "PATCH")
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-XSRF-TOKEN", In = ParameterLocation.Header, Required = true,
                    Description = "Token de /api/auth/csrf ligado al usuario y cookie antiforgery; renovar después de login/logout.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("400", new OpenApiResponse { Description = "CSRF inválido, HTTPS requerido o datos no válidos." });
                operation.Responses.TryAdd("409", new OpenApiResponse { Description = "Conflicto con estado, rol o restricción de persistencia." });
                if (context.Description.RelativePath is "api/auth/login" or "api/auth/register")
                    operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Límite de intentos alcanzado." });
                if (context.Description.RelativePath is "api/auth/login")
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Credenciales no aceptadas, sin enumerar cuentas." });
                if (context.Description.RelativePath is "api/auth/register")
                { operation.Responses.Remove("200"); operation.Responses.TryAdd("202", new OpenApiResponse { Description = "Solicitud recibida, respuesta uniforme." }); }
                else if (context.Description.RelativePath?.StartsWith("api/tutoring-sessions", StringComparison.Ordinal) == true || context.Description.RelativePath?.StartsWith("api/lessons", StringComparison.Ordinal) == true)
                { operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Límite de generación o espera requerida por el proveedor." }); }
                else if (context.Description.RelativePath == "api/admin/ai/test")
                { operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Máximo 3 pruebas por administrador por minuto." }); }
                else if (context.Description.RelativePath?.StartsWith("api/teacher/assignments", StringComparison.Ordinal) == true || context.Description.RelativePath?.StartsWith("api/teacher/supervision", StringComparison.Ordinal) == true || context.Description.RelativePath?.StartsWith("api/students/me/classes", StringComparison.Ordinal) == true)
                { /* Assignment mutations return typed state (200/201), never a fabricated 204. */ }
                else if (!(context.Description.HttpMethod == "POST" && context.Description.RelativePath?.TrimEnd('/') == "api/groups"))
                { operation.Responses.Remove("200"); operation.Responses.TryAdd("204", new OpenApiResponse { Description = "Operación completada." }); }
            }
            return Task.CompletedTask;
        });
    }
}
