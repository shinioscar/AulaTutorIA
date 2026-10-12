# Contratos HTTP — Iteración 1

Base: /api, mismo origen. HTTPS requerido para autenticación y escrituras.
OpenAPI generado en /openapi/v1.json (solo Development) incluye esquemas DTO,
cookie de autenticación, cabecera CSRF y respuestas. No Swagger UI ni bearer tokens.

| Método / ruta | Acceso | Resultado normal |
|---|---|---|
| GET /health | Público | 200; Mock, liveness, sin validar PostgreSQL |
| GET /auth/csrf | Público, HTTPS | 200; requestToken y cookies antiforgery/XSRF |
| POST /auth/register | Público + CSRF + límite IP | 202 uniforme; email/password/displayName/requestedRole Student o Teacher |
| POST /auth/login | Público + CSRF + límite IP | 204 + cookie; email/password; fallo genérico 401 |
| POST /auth/logout | Autenticado + CSRF | 204; revoca sesiones de la cuenta |
| GET /me | Autenticado | 200; UserView propio |
| GET /users/{id} | Propietario o Admin del piloto | 200; ajeno 404 |
| GET /admin/users?page=1 | Admin | 200; items/page/pageSize=50 |
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
429 aplica al límite de login/registro. Errores son ProblemDetails; no exponen
contraseñas, hash, connection strings, diagnóstico de cuentas ni excepciones SQL.

No hay endpoints de tutoría/clases/evaluación. Las futuras tutorías personales
requerirán propietario y compartición explícita, sin heredar acceso por grupo.
