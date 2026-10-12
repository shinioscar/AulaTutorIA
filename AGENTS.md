# AulaTutor IA — contexto para agentes

## Fuente y alcance autorizado

- Trabajar solo en F:\AulaTutorIA. Documento maestro v1.0 en raíz: leer completo,
  nunca modificarlo. SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
- Último encargo: **exclusivamente Iteración 1**, identidad/roles/grupos/autorización.
  Iteraciones 0 y 1 completadas; no avanzar a 2 sin nueva instrucción del usuario.
- Registros: docs/iterations/ITERATION_0.md, ITERATION_1.md; plan y ADR en docs/.
  Mantener README, AGENTS, ADR, ambigüedades y verificaciones actualizados.
- No implementar tutorías, clases, motor pedagógico, evaluación ni IA real en 1.
  Mock, salud y estructura de proveedores permanecen funcionales.

## Arquitectura y convenciones

- Monolito modular: Domain sin frameworks; Application referencia Domain y expone
  DTO/interfaces/reglas de acceso; Infrastructure implementa Identity, EF/Npgsql,
  bootstrap, servicios y Mock; Api compone/autoriza/valida; Angular usa HTTP relativo.
- SDK .NET 10.0.302 / net10.0; EF Core e Identity 10.0.10, Npgsql EF 10.0.3.
  dotnet-ef 10.0.10 local en dotnet-tools.json. NuGet locks y package-lock versionados.
- Angular standalone 21.2.25 con router/forms, TS estricto, signals y HttpClient.
  Texto/documentación español; tipos/código inglés, nombres de archivos kebab-case.
- C# PascalCase, namespaces AulaTutor.*; endpoints finos, lógica en servicios.
  DTO distintos de entidades de persistencia. UTF-8/LF, PowerShell CRLF.
- No nuevos paquetes sin necesidad de la iteración; sin SDK IA, Swagger UI, Redis,
  microservicios, colas o runner E2E prematuros.
- IAiProvider/DTO en Application/Ai y Mock en Infrastructure/Ai, sin red/estado.
  Solo texto; proveedor distinto de Mock falla al iniciar, sin fallback silencioso.

## Identidad y autorización (ADR-012/013)

- ASP.NET Core Identity: roles Admin/Teacher/Student, contraseñas solo Identity.
  Student público; Teacher público Pending sin roles hasta aprobación Admin.
- Cookies __Host-AulaTutor.Session: Secure siempre, HttpOnly, SameSite Strict,
  path /, 2 horas sin sliding. Sin JWT ni sesiones en localStorage.
- HTTPS local: Angular https://localhost:4200, API https://localhost:7080;
  proxy /api/** de mismo origen. HTTP 5080 conserva salud/OpenAPI de desarrollo.
- CSRF backend para POST/PUT/DELETE, también login/registro. GET /api/auth/csrf
  emite cookie antiforgery HttpOnly y XSRF-TOKEN legible (no autentica). Angular
  X-XSRF-TOKEN relativo y renovación tras login/logout. No CORS abierto.
- 5 fallos: bloqueo 15 min; 30 login/registro por IP/min/proceso. Registro duplicado
  válido recibe mismo 202; credenciales desconocidas/incorrectas/bloqueadas: mismo 401.
- Roles/estado/aprobación/logout actualizan security stamp; revocación inmediata,
  logout de todas las sesiones de la cuenta. Servicios verifican actor actual en BD,
  organización y relación; no confiar en userId de navegador para identificar actor.
- Admin gestiona cuentas, aprobación y ciclo/miembros de grupos. Teacher aprobado:
  grupos asignados y roster mínimo. Student: solo grupos propios, sin roster.
  Perfil /api/users/{id}: propio o Admin, sin permiso implícito docente. 404 oculta
  recursos ajenos, 401 sin sesión, 403 sin rol, 409 conflicto, 400 CSRF/datos.
- Las futuras conversaciones privadas solo pertenecen al estudiante por defecto.
  Ser Admin/Teacher o compartir organización/grupo NO concede acceso a tutorías.
- Una organización piloto fija; FKs compuestas de membresías evitan cruces entre
  organizaciones. No crear multitenancy completo. Archivar grupos no elimina datos.
- Bootstrap explícito (--bootstrap-admin o script interactivo), sin credenciales
  predeterminadas ni seed de usuarios. No promover registro existente ni resetear
  contraseña al repetir; primer admin serializado por advisory lock/transacción.
  Se protege último Admin y retirada del propio acceso. No habilitar bootstrap HTTP.
- Migración InitialIdentity con organización/roles, sin usuarios. --migrate explícito;
  no migrar/crear cuentas al arranque web. Data Protection persistente, DPAPI Windows,
  volumen privado UID1654/permisos700 en Docker. Producción Linux requiere cifrado definido.

## Operación y pruebas

- Inicializar herramientas: . ./scripts/Initialize-Environment.ps1.
  Exportar certificado: ./scripts/Prepare-Local.ps1 (solo .cache/certs).
  Configurar entorno privado: . ./scripts/Load-LocalConfiguration.ps1.
- .env lo lee Compose; .NET lo carga únicamente mediante el script local explícito.
  No imprimir connection strings, cookies, contraseñas, .env ni archivos de QA privados.
- PostgreSQL localhost:15432 (interno5432) en proyecto Compose aulatutor-ia.
  API/web pueden seguir activos al entregar: inspeccionar puertos/procesos y detener
  SOLO los del proyecto antes de build/smoke (Windows bloquea DLL en uso).
- CI local completo: ./scripts/Verify.ps1 -Smoke -Http. Restore locked, build, 24
  pruebas (11 unitarias/13 integración), consistencia EF, npm ci/check/build,
  Compose config, diff, smoke Mock y HTTP de identidad.
- Integración exige AULATUTOR_TEST_CONNECTION (el loader lo asigna si falta) y
  PostgreSQL real con CREATE DATABASE. Bases aulatutor_test_<GUID> / aulatutor_http_<GUID>
  aisladas y eliminadas con validación de nombre. Nunca usar EnsureCreated/InMemory
  para reemplazar comprobaciones de migraciones o saltarlas silenciosamente.
- HTTP QA -KeepRunning solo para revisión: .cache/http-qa.json es privado, contiene
  contraseña ficticia aleatoria/PID/base temporal. Limpiar proceso/base/archivo al
  terminar; no mantener el administrador de QA en la BD principal.
- Pruebas con @example.invalid, claves aleatorias ficticias. Capturas versionadas
  en docs/iterations/evidence-1, sin secretos. Logs/artifacts y caches ignorados.
- /api/health liveness/Mock, no readiness PostgreSQL. OpenAPI solo Development con
  seguridad cookie/CSRF y rutas de 1. Los endpoints nuevos deben exigir autorización.
- Git main, sin remoto. Commit solo al pasar verificaciones; no push sin autorización.

## Pendientes y continuidad

- Iteración 2: formalizar contrato/proveedores, Groq y telemetría; conservar identidad
  y privacidad. No continuar automáticamente.
- Pendientes documentados: confirmación email, recuperación/MFA, invitaciones,
  auditoría integral (8), retención/compartición (4), frontera Approved/Published (5).
- Antes de producción/datos reales: aprobación/políticas institucionales, TLS/proxy
  de mismo origen, mínimo privilegio PostgreSQL, claves Linux cifradas y respaldos.
- No automatizar portal universitario ni conectar cuentas/proveedores externos.
