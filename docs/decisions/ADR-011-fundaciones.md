# ADR-011 — Herramientas y límites de Iteración 0

Fecha: 2026-10-12. Estado: adoptada para la base técnica; no modifica requisitos.

- SDK .NET 10.0.302 (`global.json`, latestPatch), target net10.0 y paquetes ASP.NET
  10.0.10. Formato de solución `.slnx`, admitido por el SDK instalado.
- Angular core 21.2.25; CLI/build 21.2.26; TypeScript 5.9.3; RxJS 7.8.2.
  Node de verificación: 24.11.0, npm 11.6.2. Angular 21 admite este Node;
  Angular 22 exige Node 24.15 o posterior en esta rama. No actualizar runtimes globales.
- Dependencias directas exactas, package-lock.json y packages.lock.json versionados.
  NuGet se restaura en modo bloqueado; npm mediante `npm ci`.
- Microsoft.OpenApi se fija explícitamente a 2.7.5 porque la plantilla 10.0.10
  resuelve transitivamente 2.0.0, vulnerable (GHSA-v5pm-xwqc-g5wc). No desactivar auditoría.
- PostgreSQL `18-bookworm` fijado por digest en Compose, con volumen en
  `/var/lib/postgresql` (layout oficial desde 18). Puerto local 15432: el 5432
  estaba ocupado. Mantener 5432 en la red interna del contenedor. Actualizar
  digest intencionalmente al aplicar parches, después de verificar la imagen.
- Minimal API, OpenAPI first-party sin Swagger UI; singleton Mock sin estado.
- Pantalla Angular de estado con HttpClient y signals; sin funcionalidades futuras.
- Compose arranca PostgreSQL; perfil `app` añade API. Web se ejecuta con Angular CLI
  en host. No empaquetar un despliegue de producción en esta iteración.
- CI local: `scripts/Verify.ps1 -Smoke`; sin remoto, pipeline hospedado ni push.
- Propuesta para Iteración 1: Identity con cookies HTTP-only y protección CSRF,
  manteniendo mismo origen. No hay mecanismo de autenticación implementado aún.

Fuentes de compatibilidad y corrección consultadas:

- https://angular.dev/reference/versions
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0
- https://hub.docker.com/_/postgres
- https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
