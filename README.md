# AulaTutor IA

Base técnica de **Iteración 0**, construida a partir de
`AulaTutor_IA_Documento_Maestro_v1.0.md` (fuente de verdad intacta).

Angular + ASP.NET Core .NET 10 en monolito modular, PostgreSQL preparado por
Docker Compose y proveedor IA simulado. Esta entrega incluye únicamente una
pantalla de estado y `/api/health`; las funcionalidades académicas y de identidad
permanecen pendientes.

## Requisitos

- .NET SDK 10.0.302 (o parche compatible de esa feature band).
- Node.js 24.11.0 usado en verificación; rangos compatibles en package.json.
- npm 11.6.2 usado en verificación; Git; PowerShell 7 recomendado.
- Docker Desktop con motor Linux activo y Docker Compose para PostgreSQL/API
  en contenedor. API y web locales con Mock arrancan sin PostgreSQL en Iteración 0.

## Preparar y verificar

Desde `F:\AulaTutorIA` en PowerShell:

```powershell
. ./scripts/Initialize-Environment.ps1
./scripts/Verify.ps1 -Smoke
```

El script restaura dependencias bloqueadas, compila solución y Angular, ejecuta
pruebas .NET, verifica TypeScript y Compose, y arranca/para API y web con Mock.
Los puertos 5080 y 4200 deben estar libres para smoke. Salidas en `artifacts/`
ignoradas por Git; cachés en `.cache/`. Se necesita red en la primera restauración.

## Ejecutar en local

Terminal 1 (raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
dotnet run --project src/AulaTutor.Api --launch-profile http
```

Terminal 2 (raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
Set-Location src/AulaTutor.Web
npm.cmd ci
npm.cmd start
```

Abrir http://127.0.0.1:4200. La pantalla debe indicar «API disponible» y «Mock».
API: http://127.0.0.1:5080/api/health. OpenAPI de desarrollo:
http://127.0.0.1:5080/openapi/v1.json. Si la API está detenida, la web muestra el
error y permite comprobar nuevamente. El proxy evita necesitar CORS abierto.

## PostgreSQL y API con Compose

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Editar .env: elegir POSTGRES_PASSWORD local; nunca subir este archivo.
docker compose --env-file .env -f deploy/compose.yaml config --quiet
docker compose --env-file .env -f deploy/compose.yaml up -d postgres
# Opcional: API en contenedor en lugar de dotnet run, mismo puerto 5080.
docker compose --env-file .env -f deploy/compose.yaml --profile app up -d --build
docker compose --env-file .env -f deploy/compose.yaml ps
docker compose --env-file .env -f deploy/compose.yaml down
```

`down` conserva el volumen de PostgreSQL. La configuración es de desarrollo,
con puertos publicados solo en loopback; no es un despliegue de producción.
PostgreSQL se publica en `127.0.0.1:15432` (configurable con POSTGRES_PORT);
su puerto interno sigue siendo 5432. Se evita el puerto 5432 ocupado en el host.
En Iteración 0 la API aún no abre conexiones ni aplica migraciones; su health
**no comprueba PostgreSQL**. La variable ConnectionStrings__AulaTutor en Compose
prepara la futura persistencia. El uso académico de SQL Server no cambia la BD
de la aplicación ni autoriza ejecutar SQL no confiable en ella.

## Configuración

`Ai:Provider=Mock` en appsettings; `Ai__Provider` permite sobrescribirlo en .NET.
Compose usa `AI_PROVIDER` desde `.env`. Un valor distinto de Mock falla al iniciar.
No se aceptan claves IA ni se realizan llamadas externas. `.env` lo lee Compose;
no lo cargan automáticamente .NET o Angular. Secretos futuros: variables de
entorno/gestor de secretos, nunca repositorio ni navegador.

## Organización

```text
src/AulaTutor.Domain          Reglas/modelos futuros, sin frameworks
src/AulaTutor.Application     Contratos IA mínimos y casos de uso futuros
src/AulaTutor.Infrastructure  Mock, configuración y adaptadores futuros
src/AulaTutor.Api             Host, salud y OpenAPI
src/AulaTutor.Web             Angular, pantalla inicial y proxy local
tests/                       Unitarias, integración y reserva E2E
deploy/                      Compose y Dockerfile API
scripts/                     Entorno, CI local y smoke
docs/                        Arquitectura, ambigüedades, ADR y plan
```

Consultar `AGENTS.md` para continuar en otra conversación y
`docs/iterations/ITERATION_0.md` para los resultados, problemas y evidencias.
Tras esta entrega, API/PostgreSQL y la web quedan activos para revisión local.
Detenerlos antes de ejecutar el smoke, que requiere puertos libres; para detener
la web, finalizar su comando `npm start` en la terminal, y para contenedores usar
`docker compose --env-file .env -f deploy/compose.yaml down`.
Próximo encargo: Iteración 1 (identidad, roles/grupos y autorización por recurso),
resolviendo antes bootstrap de admin/validación docente y cookies/CSRF.
