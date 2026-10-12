# Arquitectura inicial

Monolito modular. Dependencias permitidas:

```text
Web (Angular) → HTTP /api → Api → Application → Domain
                             ↓        ↑
                        Infrastructure
                             ↓
                   PostgreSQL / adaptadores IA (futuros)
```

- Domain: organización, grupo, membresía, roles y aprobación docente, sin frameworks.
- Application: IAiProvider conservado; DTO, interfaces de identidad/grupos y reglas
  de autorización puras, independientes de EF/Identity.
- Infrastructure: Identity/EF Core/Npgsql, servicios por recurso, bootstrap y Mock.
- Api: composición, Minimal APIs, filtro CSRF/DTO, logs JSON y OpenAPI con cookie/CSRF.
- Web: Angular standalone con router y formularios; sesión en cookies, navegación
  por permisos y proxy HTTPS de mismo origen. Sin SSR ni SDK IA.

PostgreSQL 18 tiene migración InitialIdentity: Identity, organización, grupos y
membresías. Se siembran roles/organización, sin usuarios predeterminados. Mantenimiento
explícito (--migrate / --bootstrap-admin), sin migrar en el inicio normal.
No se instala Redis, SignalR, colas, bibliotecas UI ni clientes externos de IA.

`GET /api/health` devuelve status, application, iteration, aiProvider y aiModel.
Solo confirma vida de la API y disponibilidad del Mock; **no** confirma conexión
a PostgreSQL ni representa readiness del MVP. `GET /openapi/v1.json` existe
solo en Development. Los endpoints de negocio actuales se limitan a identidad
y grupos; no hay generación IA, tutorías, clases ni evaluaciones.

HTTPS local para cookies Secure, con certificados excluidos en .cache. HTTP 5080
conserva salud y OpenAPI de desarrollo. Fuera de desarrollo se activa HTTPS/HSTS;
la configuración TLS/proxy de producción queda pendiente. Compose es de desarrollo.

Adaptadores futuros vivirán en `Infrastructure/Ai/`; selección, capacidades,
enrutamiento, cuotas y telemetría quedan para Iteración 2. No hay fallback.
