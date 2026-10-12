# ADR-012 — Identidad, cookies y autorización por recurso

Fecha: 2026-10-12. Estado: aprobada por el encargo de Iteración 1 e implementada.
Complementa ADR-011 y resuelve la elección de autenticación del maestro §8.1.

ASP.NET Core Identity con EF Core 10.0.10 y Npgsql EF 10.0.3. Contraseñas, hashes,
bloqueo y security stamps los gestiona Identity; no hay JWT ni almacenamiento de
sesiones en localStorage. Cookie `__Host-AulaTutor.Session`: Secure siempre,
HttpOnly, SameSite Strict, path /, sin Domain y ticket de dos horas sin renovación
deslizante. Login no persistente. Cambios de rol/estado/aprobación y logout
invalidan las cookies previas mediante security stamp; logout afecta todas las
sesiones de esa cuenta. La validación en cada petición prioriza revocación inmediata
en el piloto pequeño frente al costo de consultas adicionales.

HTTPS también en localhost. Angular usa https://localhost:4200 y proxy relativo
/api hacia https://localhost:7080. Los certificados de desarrollo se exportan a
.cache; no se instala un navegador, ni se versionan certificados o claves.
HTTP 5080 se conserva para salud/OpenAPI de desarrollo, sin login seguro por HTTP.
Producción requiere TLS y mismo origen; el Compose suministrado es de desarrollo.

Todas las escrituras de identidad/grupos exigen HTTPS y token antiforgery validado
en backend, también login y registro anónimos. GET /api/auth/csrf emite cookie
antiforgery HttpOnly y XSRF-TOKEN legible por Angular; **esta última no es una
credencial de sesión**. Angular envía X-XSRF-TOKEN automáticamente en URLs
relativas. Se obtiene un token nuevo tras login/logout. SameSite y CSRF son
controles complementarios; no se abre CORS. Se evita cachear datos de sesión.

Login: 5 fallos bloquean la cuenta 15 minutos; login/registro tienen límite de
30 peticiones/minuto por IP por proceso. Credenciales incorrectas, cuenta ausente,
desactivación y bloqueo producen el mismo error público. Registro duplicado
válido devuelve el mismo 202 que una solicitud nueva, sin sesión automática.
Las contraseñas solo existen temporalmente en formulario/servidor; no se registran
request bodies ni secretos. Datos estrictamente ficticios durante el piloto.

Roles efectivos: Admin, Student, Teacher. Registro Student otorga Student;
solicitud Teacher crea cuenta Pending **sin roles**, permite login y perfil propio,
pero no funciones docentes/grupos. Solo Admin puede aprobar/rechazar; Teacher
requiere aprobación incluso en cambios administrativos de roles. Cada servicio
consulta usuario/roles actuales, organización y relación, además del control de
rol en el endpoint. La navegación Angular facilita la experiencia; no concede permisos.

| Operación | Admin | Teacher aprobado | Student |
|---|---|---|---|
| Perfil /api/users/{id} | Organización piloto | Solo propio | Solo propio |
| Usuarios, roles, estado y validación docente | Sí | No | No |
| Crear, renombrar, archivar grupos / gestionar asociaciones | Sí | No | No |
| Listar/ver grupos | Todos del piloto | Asignados y activos | Membresías propias y activas |
| Lista de miembros (ID, nombre, tipo, sin email) | Sí | Solo grupo asignado | No |
| Futuras conversaciones privadas | Sin acceso por rol | Sin acceso por grupo/organización | Solo propietario por defecto |

404 uniforme oculta perfiles/grupos ajenos; 401 indica sesión ausente/revocada y
403 insuficiencia general de permisos. Un grupo archivado conserva membresías y
queda fuera de acceso Student/Teacher. Las relaciones académicas no autorizan
conversaciones personales; no existe aún un endpoint de tutorías.

Se protege al último Admin activo y se impide retirar el propio acceso administrativo.
La gestión global de roles/membresías corresponde al Admin en esta versión;
delegar creación/gestión de grupos a docentes requerirá una decisión posterior.

Referencias oficiales utilizadas:

- https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- https://angular.dev/best-practices/security
- https://www.npgsql.org/efcore/release-notes/10.0.html
