# AulaTutor IA — contexto para agentes

## Fuente y alcance

- Trabajar exclusivamente dentro de `F:\AulaTutorIA`. No modificar otros proyectos.
- Leer completo `AulaTutor_IA_Documento_Maestro_v1.0.md` antes de cambiar alcance.
  `docs/MASTER_PLAN.md` es un índice; no duplicar ni reescribir la fuente original.
- Último encargo autorizado: **exclusivamente Iteración 0**. No avanzar a 1 sin un
  nuevo encargo. Estado y evidencias: `docs/iterations/ITERATION_0.md`.
- En esta base solo existen Mock, salud y pantalla de estado. No hay auth,
  usuarios, grupos, sesiones, clases, evaluaciones, reportes ni proveedores reales.
- Mantener este AGENTS.md, README, ADR y registro de iteración actualizados con
  cada cambio relevante, incluyendo verificaciones y limitaciones reales.

## Arquitectura y versiones

- Monolito modular .NET 10: Domain ← Application ← Infrastructure; Api compone
  Application/Infrastructure. Domain no depende de frameworks o proveedores.
- Angular standalone responsive; TypeScript estricto, HttpClient y signals.
  Versiones exactas en package.json/locks, ADR-011 y global.json.
- PostgreSQL 18 preparado por Compose. EF Core/Npgsql/Identity y migración inicial
  se añadirán al ejecutar Iteración 1, sin migraciones vacías en 0.
- IAiProvider/DTO están en Application/Ai; adaptadores en Infrastructure/Ai.
  Mock singleton sin estado. Solo texto, sin consumo inventado, sin llamadas HTTP.
  `Ai:Provider != Mock` debe fallar al iniciar; jamás fallback silencioso.
- API: Minimal APIs, respuestas tipadas, ProblemDetails, logs JSON sin contenidos
  de mensajes. OpenAPI solo Development. No instalar Swagger UI por defecto.
- `/api/health` confirma vida/Mock; no afirmar disponibilidad de PostgreSQL.
- Proxy Angular `/api/**` → `127.0.0.1:5080`. Cookies HTTP-only + CSRF es propuesta
  para 1, no decisión de autenticación cerrada ni implementación existente.

## Convenciones

- C# PascalCase para tipos/métodos, namespaces AulaTutor.*; lógica de negocio en
  Domain/Application, hosts/endpoints finos. DTO separados de persistencia.
- TypeScript: strict, nombres de clases PascalCase, archivos kebab-case cuando
  tengan varias palabras. Texto visible/documentación en español; código en inglés.
- UTF-8 y LF (PowerShell CRLF según .gitattributes), `.editorconfig` en raíz.
- Dependencias directas exactas y lockfiles versionados; solo instalar las
  necesarias para la iteración. No microservicios, colas ni SDK IA prematuros.
- Versionado de planes/políticas, evidencias antes de IA y privacidad por defecto
  son reglas del maestro. Ambigüedades en `docs/architecture/OPEN_QUESTIONS.md`;
  documentarlas y resolverlas mediante ADR, sin inventar requisitos.

## Operación y validación

- PowerShell: `. ./scripts/Initialize-Environment.ps1` antes de comandos de build;
  caches .NET/NuGet/npm quedan en `.cache/` del proyecto, telemetría desactivada.
- CI local: `./scripts/Verify.ps1 -Smoke` (restore locked, build, tests, npm ci,
  check, build, Compose config, diff y smoke). Smoke requiere puertos 5080/4200 libres.
- La entrega 0 deja API/PostgreSQL y Angular activos para revisión. Comprobar
  procesos/puertos antes de nuevos arranques y detener solo servicios del proyecto.
- Desarrollo: `dotnet run --project src/AulaTutor.Api --launch-profile http`;
  en src/AulaTutor.Web: `npm.cmd start`. README incluye Compose.
- Cambiar dependencias solo intencionalmente; actualizar lockfiles con restore o
  npm install y volver a verificar. No ocultar alertas de seguridad.
- Añadir pruebas significativas para límites arquitectónicos y cambios reales.
  No probar flujos de negocio inexistentes. E2E reservado sin instalar runner.
- Git inicializado en `main`, sin remoto. No push/publicación sin autorización.
  No inventar identidad Git; crear commit solo con identidad disponible/autorizada.

## Secretos y datos

- `.env.example` contiene únicamente placeholders locales; `.env` ignorado.
  Compose lee `.env`; dotnet/Angular no lo cargan automáticamente.
- Usar variables de entorno para credenciales. Nunca claves en Angular,
  appsettings versionados, logs, capturas o historial Git.
- HTTP solo localhost para desarrollo; HTTPS fuera de localhost.
- Solo datos ficticios hasta políticas y autorización institucional.
- No automatizar el portal universitario ni conectar cuentas externas.
