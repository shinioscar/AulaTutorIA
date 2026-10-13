# Arquitectura del piloto (Iteraciones 0–3)

Monolito modular. Dependencias permitidas:

```text
Web (Angular) → HTTP /api → Api → Application → Domain
                             ↓        ↑
                        Infrastructure
                             ↓
                   PostgreSQL / adaptadores IA
```

- Domain: organización, grupo, membresía, roles, aprobación docente y entidades
  de tutoría privada, sin frameworks.
- Application: IAiProvider/DTO/capacidades/errores/diagnóstico; identidad/grupos y reglas
  de autorización puras, DTO de tutoría y orquestador pedagógico inicial,
  independientes de EF/Identity.
- Infrastructure: Identity/EF Core/Npgsql, servicios por recurso, bootstrap,
  Mock/Groq/Gemini/Ollama y transporte HTTP limitado sin SDK.
- Api: composición, Minimal APIs, filtro CSRF/DTO, logs JSON y OpenAPI con cookie/CSRF.
- Web: Angular standalone con router y formularios; sesión en cookies, navegación
  por permisos y proxy HTTPS de mismo origen. Sin SSR ni SDK IA.

PostgreSQL 18 tiene InitialIdentity y PrivateTutoring: identidad/grupos y sesiones,
mensajes/turnos privados. Se siembran roles/organización, sin usuarios predeterminados. Mantenimiento
explícito (--migrate / --bootstrap-admin), sin migrar en el inicio normal.
No se instala Redis, SignalR, colas, bibliotecas UI ni SDK de proveedores IA.

`GET /api/health` devuelve status, application, iteration, aiProvider y aiModel.
Confirma vida de la API/configuración IA sin red; conserva iteration=1 por
compatibilidad y no comprueba PostgreSQL. `GET /openapi/v1.json` solo Development.
Endpoints de identidad/grupos y /api/admin/ai/ con prueba explícita Admin+CSRF,
prompt fijo y máximo 128 tokens. Tutorías Student con autorización de propietario,
CSRF, historial durable y resumen local; contratos en TUTORING_HTTP.md.
No hay clases ni evaluaciones oficiales.

HTTPS local para cookies Secure, con certificados excluidos en .cache. HTTP 5080
conserva salud y OpenAPI de desarrollo. Fuera de desarrollo se activa HTTPS/HSTS;
la configuración TLS/proxy de producción queda pendiente. Compose es de desarrollo.

Adaptadores en `Infrastructure/Ai/`; configuración validada, catálogo de capacidades
y errores seguros según ADR-014. Mock predeterminado, sin fallback ni llamadas
durante arranque/salud. Orquestación inicial en Application y persistencia/leases en
Infrastructure (ADR-016/017). Streaming, auditoría integral y cuotas distribuidas
permanecen pendientes. Contraseñas y búsqueda de miembros: ADR-015.
