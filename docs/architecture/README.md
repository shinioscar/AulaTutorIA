# Arquitectura inicial

Monolito modular. Dependencias permitidas:

```text
Web (Angular) → HTTP /api → Api → Application → Domain
                             ↓        ↑
                        Infrastructure
                             ↓
                   PostgreSQL / adaptadores IA (futuros)
```

- Domain: sin paquetes ni modelos de negocio todavía.
- Application: referencia Domain; contrato conceptual `IAiProvider` y DTO mínimos.
- Infrastructure: referencia Application; Mock y registro/validación de configuración.
- Api: composition root, Minimal API de salud, ProblemDetails, JSON logs y OpenAPI.
- Web: Angular standalone, sin SSR, router, formularios ni SDK IA; una pantalla de
  estado y proxy `/api/**` para mantener el mismo origen del navegador en local.

PostgreSQL 18 queda preparado por Compose. La API aún no utiliza la cadena de
conexión; no hay DbContext, entidades, migraciones, Identity ni datos precargados.
EF Core/Npgsql se incorporarán al implementar persistencia en Iteración 1.
No se instala Redis, SignalR, colas, bibliotecas UI ni clientes externos de IA.

`GET /api/health` devuelve status, application, iteration, aiProvider y aiModel.
Solo confirma vida de la API y disponibilidad del Mock; **no** confirma conexión
a PostgreSQL ni representa readiness del MVP. `GET /openapi/v1.json` existe
solo en Development. No hay endpoints de generación IA o de negocio.

HTTP se limita a localhost en los comandos de desarrollo y puertos Compose.
Fuera de desarrollo se activa redirección HTTPS/HSTS; la configuración TLS/proxy
de despliegue real queda pendiente. Compose es exclusivamente de desarrollo.

Adaptadores futuros vivirán en `Infrastructure/Ai/`; selección, capacidades,
enrutamiento, cuotas y telemetría quedan para Iteración 2. No hay fallback.
