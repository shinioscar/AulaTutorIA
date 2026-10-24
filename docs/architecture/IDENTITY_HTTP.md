# Contratos HTTP — identidad y diagnóstico (Iteraciones 1–2)

Base: /api, mismo origen. HTTPS requerido para autenticación y escrituras.
OpenAPI generado en /openapi/v1.json (solo Development) incluye esquemas DTO,
cookie de autenticación, cabecera CSRF y respuestas. No Swagger UI ni bearer tokens.

| Método / ruta | Acceso | Resultado normal |
|---|---|---|
| GET /health | Público | 200; proveedor configurado, liveness, sin red IA ni validar PostgreSQL; iteration=1 heredado |
| GET /auth/csrf | Público, HTTPS | 200; requestToken y cookies antiforgery/XSRF |
| POST /auth/register | Público + CSRF + límite IP | 202 uniforme; email/password/displayName/requestedRole Student o Teacher |
| POST /auth/login | Público + CSRF + límite IP | 204 + cookie; email/password; fallo genérico 401 |
| POST /auth/logout | Autenticado + CSRF | 204; revoca sesiones de la cuenta |
| GET /me | Autenticado | 200; UserView propio |
| GET /users/{id} | Propietario o Admin del piloto | 200; ajeno 404 |
| GET /admin/users?page=1&email= | Admin | 200; items/page/pageSize=50; correo parcial normalizado antes de paginar, máximo 254 |
| POST /admin/users/{id}/teacher-decision | Admin + CSRF | 204; {approve:boolean} |
| PUT /admin/users/{id}/roles | Admin + CSRF | 204; {roles:string[]} |
| PUT /admin/users/{id}/status | Admin + CSRF | 204; {isActive:boolean} |
| GET /groups/ | Autenticado, rol habilitado y relaciones | 200; lista autorizada |
| GET /groups/{id} | Relación autorizada o Admin | 200; sin relación 404 |
| GET /groups/{id}/members | Admin o Teacher asignado | 200; Student/ajeno 404 |
| POST /groups/ | Admin + CSRF | 201; {name:string} |
| PUT /groups/{id} | Admin + CSRF | 204; {name:string} |
| DELETE /groups/{id} | Admin + CSRF | 204; archivado, no borrado físico |
| PUT /groups/{id}/members | Admin + CSRF | 204 idempotente; {userId:guid,kind:Student/Teacher} |
| DELETE /groups/{id}/members/{userId}/{kind} | Admin + CSRF | 204 idempotente |
| GET /admin/ai/ | Admin activo del piloto |200; proveedor/modelo/configuración/capacidades/límites, sin claves ni red |
| POST /admin/ai/test | Admin + CSRF + límite de cuenta |200; diagnóstico explícito con success/text/durationMs/usage/error seguro; prompt fijo 128 tokens |

UserView: id, displayName, email, organizationId, roles, teacherApproval, isActive.
GroupView: id, name, isArchived, students, teachers, canViewMembers (guía para UI,
calculada por recurso; no reemplaza autorización backend). MembershipView: userId,
displayName, kind; sin email. La identidad del solicitante viene de la cookie,
nunca de un actorId enviado por navegador. Solo Admin busca usuarios del piloto.

CSRF: antes del primer POST obtener /auth/csrf, conservar ambas cookies y enviar
requestToken como X-XSRF-TOKEN. Renovar tras login/logout o cambio de usuario.
Angular lo hace con HttpClient. Token faltante/inválido/de otra sesión: 400.
403 no concede función docente; 404 no distingue recurso ajeno de inexistente.
409 indica conflicto (rol no aprobado, grupo archivado, duplicado o último Admin).
429 aplica al límite de login/registro y 3 intentos/min de diagnóstico por cuenta.
POST /admin/ai/test devuelve 200 con success=false y errorCode/Error/RetryAfterSeconds
si falla el proveedor; su 429 externo es RateLimited dentro del diagnóstico.401/403/400
de sesión/permisos/CSRF y 429 de la API son ProblemDetails. Ninguno expone
contraseñas, hash, connection strings, diagnóstico de cuentas ni excepciones SQL.

Tutorías privadas implementadas en Iteración 3: contratos en TUTORING_HTTP.md.
Solo propietario Student, sin compartición ni acceso implícito por grupo/rol.
No hay endpoints de clases ni evaluación oficial.

Contraseñas nuevas 5–128, sin composición obligatoria; política ADR-015. Modelo EF
y hashes existentes conservados. Configuración IA/formatos/capacidades en ADR-014.

## Ampliación BYOK-1

[ADR-030](../decisions/ADR-030-conexiones-personales-ia.md) y [contrato personal](PERSONAL_AI.md) agregan gestión propia Teacher/Student. La sesión incorpora un identificador de autorización; la cookie principal sigue Strict. Login/logout cancelan el intento OAuth del navegador, y la finalización exige sesión/stamp originales y CSRF. Admin conserva exclusivamente sus rutas operativas salvo rol académico adicional.
