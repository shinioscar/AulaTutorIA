# AulaTutor IA

Iteración 1: identidad, roles, validación docente y grupos. Fuente de verdad:
`AulaTutor_IA_Documento_Maestro_v1.0.md`, intacto. La base de Iteración 0 conserva
Mock, salud/OpenAPI, arquitectura modular, Compose y CI local. No hay tutorías,
clases, evaluaciones ni proveedores IA reales implementados.

## Herramientas

.NET SDK 10.0.302, EF Core/Identity 10.0.10, Npgsql EF 10.0.3, PostgreSQL 18.6;
Angular 21.2.25 (CLI/build 21.2.26), Node 24.11.0 y npm 11.6.2 usados en validación.
PowerShell 7 y Docker Desktop Linux activo. Dependencias exactas y lockfiles
versionados; dotnet-ef local en dotnet-tools.json. Sin instalaciones globales.

## Preparación

En PowerShell desde `F:\AulaTutorIA`:

```powershell
. ./scripts/Initialize-Environment.ps1
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Elegir POSTGRES_PASSWORD privado en .env si aún es un placeholder.
./scripts/Prepare-Local.ps1
. ./scripts/Load-LocalConfiguration.ps1

docker compose --env-file .env -f deploy/compose.yaml up -d postgres
dotnet restore AulaTutorIA.slnx --locked-mode
dotnet tool restore
dotnet run --project src/AulaTutor.Api --no-launch-profile -- --migrate
```

PostgreSQL está en localhost:15432, con 5432 dentro de su contenedor. .env lo lee
Compose; Load-LocalConfiguration.ps1 carga explícitamente conexión, certificados
y claves de protección para comandos .NET. No imprime secretos. .cache y .env se
excluyen de Git. No copiar contraseñas en appsettings o Angular.

Prepare-Local exporta el certificado HTTPS de desarrollo a .cache/certs. Usa
localhost para autenticación; si el navegador no confía en el certificado,
configurar su confianza local con `dotnet dev-certs https --trust`. Este comando
solo se ejecuta conscientemente por el desarrollador. No omitir validación TLS
fuera de las verificaciones locales con certificado de desarrollo.

## Administrador inicial (explícito)

```powershell
./scripts/Bootstrap-Admin.ps1
```

Solicita email, nombre y contraseña de forma interactiva, sin contraseña en
argumentos ni logs. No hay usuario/contraseña predeterminados. Ejecutar después
de la migración. Repetir con el mismo administrador no cambia su contraseña;
una cuenta previamente registrada no puede promoverse por bootstrap. Después,
los administradores adicionales se gestionan mediante un Admin autenticado.
También se admite --bootstrap-admin con variables privadas BootstrapAdmin__Email,
BootstrapAdmin__DisplayName y BootstrapAdmin__Password para operación controlada.

## Ejecutar API y web

API local (terminal 1, raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
. ./scripts/Load-LocalConfiguration.ps1
dotnet run --project src/AulaTutor.Api --launch-profile https
```

Web (terminal 2, raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
Set-Location src/AulaTutor.Web
npm.cmd ci
npm.cmd start
```

Abrir https://localhost:4200. Proxy HTTPS /api al mismo origen del navegador.
API HTTPS https://localhost:7080; salud heredada http://127.0.0.1:5080/api/health.
OpenAPI solo Development en /openapi/v1.json. La salud muestra Mock e iteración 1;
no afirma conexión a PostgreSQL.

Alternativa API en Docker (no ejecutar a la vez la API local):

```powershell
docker compose --env-file .env -f deploy/compose.yaml --profile app up -d --build
docker compose --env-file .env -f deploy/compose.yaml --profile app run --rm api --migrate
```

Puertos de API 5080 HTTP y 7080 HTTPS. Montaje local de certificados, claves de
Data Protection persistidas en volumen privado /keys; API sin root. Compose es
de desarrollo. `docker compose --env-file .env -f deploy/compose.yaml down`
conserva volúmenes; no usar -v para una parada habitual. Para la web, Ctrl+C.

## Funcionalidades y permisos

- Registro Student; solicitud Teacher pendiente sin rol docente, con login y
  consulta de su estado. Admin aprueba/rechaza y administra roles/estado de cuentas.
- Login, perfil de sesión /me y logout. Cookies Secure/HttpOnly/Strict, expiración
  de 2 horas; sin tokens en localStorage. Logout y cambios de permisos revocan
  sesiones anteriores, por lo que el usuario debe volver a entrar tras aprobación.
- Admin crea/renombra/archiva grupos y asocia docentes aprobados/estudiantes.
  Teacher ve grupos asignados y listado mínimo de miembros; Student solo sus
  grupos, sin listado de compañeros ni perfiles ajenos. Archivo conserva datos.
- Organización piloto única, preparada en el modelo sin interfaces multitenant.
- Tutorías autónomas futuras privadas por defecto, sin acceso implícito por grupo,
  organización o rol administrativo. No existen todavía endpoints de tutoría.

CSRF validado en todas las escrituras, incluido registro/login. Angular obtiene
/api/auth/csrf y envía X-XSRF-TOKEN; renueva tras login/logout. 5 fallos bloquean
una cuenta 15 minutos; login/registro limitados a 30 peticiones/minuto por IP.
Errores genéricos para cuentas inexistentes, bloqueadas o desactivadas; registro
válido duplicado mantiene respuesta 202. DTO validados y permisos en backend.

Contratos completos: docs/architecture/IDENTITY_HTTP.md y OpenAPI con cookie,
cabecera CSRF y códigos de respuesta. Decisiones: ADR-012/ADR-013.

## Verificación reproducible

```powershell
# PostgreSQL del proyecto activo; API/web detenidas (puertos 5080, 7080, 4200 libres).
./scripts/Verify.ps1 -Smoke -Http
```

Restaura locks, compila, ejecuta 11 pruebas unitarias y 13 de integración, verifica
consistencia de migración, npm ci/TypeScript/Angular, Compose, Git y dos recorridos
reales: smoke de fundaciones y HTTP de identidad sobre HTTPS/PostgreSQL.
Cada prueba de identidad crea/elimina su propia BD temporal; no usa InMemory ni
omite pruebas sin PostgreSQL. Requiere credenciales locales con CREATE DATABASE.
HTTP usa cuentas @example.invalid y contraseña aleatoria, sin sembrar usuarios en
la BD principal. `.env` y certificados deben estar preparados para estos scripts.

La revisión visual funcional cubrió escritorio 1440×1000 y móvil 390×844/320 de
ancho. Capturas: docs/iterations/evidence-1/. Resultados: docs/iterations/ITERATION_1.md.
En Windows detener la API antes de compilar para evitar bloqueo de DLL.

## Continuar el proyecto

Consultar AGENTS.md, los ADR, docs/architecture/OPEN_QUESTIONS.md y el plan de
iteraciones. Solo se autoriza hasta Iteración 1. Próximo encargo: Iteración 2,
formalizar contrato IA/Mock, Groq y configuración/telemetría según el maestro.

Pendientes: confirmación de email, recuperación/MFA, invitaciones autoservicio y
políticas de retención/compartición. Antes de producción: TLS confiable, mismo
origen/proxy definido, cifrado de claves Linux en reposo y usuario PostgreSQL de
mínimo privilegio. El piloto sigue usando datos ficticios.
