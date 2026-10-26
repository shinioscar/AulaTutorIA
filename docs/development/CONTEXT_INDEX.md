# Índice de contexto — lectura por subsistema

Leer siempre [AGENTS.md](../../AGENTS.md). Elegir filas según el cambio; si cruza
subsistemas, leer todas las afectadas. Abrir solo secciones pertinentes de README,
ADR y contratos, ampliando revisión para seguridad, integridad o contradicciones.
Las cifras operativas y detalles de implementación se consultan en esos documentos,
no se sustituyen por el resumen permanente. Las ampliaciones posteriores prevalecen
sobre los límites históricos de un ADR; no autorizan encargos nuevos.

| Tarea | Reglas/decisiones y contratos que consultar |
|---|---|
| Arquitectura/backend/API | [Mapa](../architecture/README.md), [ADR-001–010](../decisions/ADR-001-010.md), [fundaciones](../decisions/ADR-011-fundaciones.md); contrato de la fila funcional |
| Frontend/UX/temas | [Diseño](../design/DESIGN_SYSTEM.md), [tokens](../design/THEME_TOKENS.md), [componentes](../design/COMPONENT_GUIDELINES.md), [ADR-020](../decisions/ADR-020-temas-y-sistema-visual.md), [UX docente ADR-026](../decisions/ADR-026-creacion-docente-progresiva.md), [navegación/gráficos UX-2 ADR-029](../decisions/ADR-029-navegacion-y-supervision-visual.md) |
| Identity/permisos/grupos | [ADR-012](../decisions/ADR-012-identidad-y-autorizacion.md), [bootstrap/BD ADR-013](../decisions/ADR-013-persistencia-y-bootstrap.md), [contraseña/búsqueda ADR-015](../decisions/ADR-015-contrasenas-y-busqueda.md), [HTTP](../architecture/IDENTITY_HTTP.md) |
| PostgreSQL/migraciones | [ADR-013](../decisions/ADR-013-persistencia-y-bootstrap.md), [integridad/leases ADR-017](../decisions/ADR-017-integridad-de-turnos.md), ADR del módulo (021/023/024/025 abajo), [operación/backup](../architecture/OPERATIONS.md); migraciones existentes en src/AulaTutor.Infrastructure/Persistence/Migrations |
| Tutoría privada/política/turnos | [ADR-016](../decisions/ADR-016-tutorias-privadas.md), [ADR-017](../decisions/ADR-017-integridad-de-turnos.md), [formatos/recuperación ADR-022](../decisions/ADR-022-formatos-groq-y-recuperacion.md), [HTTP](../architecture/TUTORING_HTTP.md) |
| Diagnóstico/planes/validadores/evidencias | [ADR-018](../decisions/ADR-018-planes-y-evidencias.md), [ADR-022](../decisions/ADR-022-formatos-groq-y-recuperacion.md), [HTTP](../architecture/LEARNING_HTTP.md); también reglas de turno anteriores |
| Reportes/compartición/refuerzo | [ADR-019](../decisions/ADR-019-reportes-y-comparticion.md), [HTTP aprendizaje](../architecture/LEARNING_HTTP.md); no confundir con supervisión AssignedClass |
| Constructor/versionado/simulación | [ADR-021](../decisions/ADR-021-clases-versiones-y-simulaciones.md), [ADR-022](../decisions/ADR-022-formatos-groq-y-recuperacion.md), [ADR-026](../decisions/ADR-026-creacion-docente-progresiva.md), [HTTP](../architecture/LESSONS_HTTP.md) |
| Asignaciones/sesiones vinculadas | [ADR-023](../decisions/ADR-023-asignaciones-y-sesiones-vinculadas.md), [HTTP](../architecture/ASSIGNMENTS_HTTP.md); también reglas de turno/evidencia |
| Supervisión/revisión/métricas | [ADR-024](../decisions/ADR-024-supervision-y-revision-humana.md), [HTTP](../architecture/SUPERVISION_HTTP.md); consultar ADR-023 para acceso/disponibilidad |
| Proveedores/JSON/transporte/cuotas | [ADR-014](../decisions/ADR-014-proveedores-ia.md), [ADR-022](../decisions/ADR-022-formatos-groq-y-recuperacion.md), [presupuestos/auditoría ADR-025](../decisions/ADR-025-cuotas-auditoria-y-resiliencia.md), [operación](../architecture/OPERATIONS.md) |
| Conexiones personales/OAuth/custodia | [ADR-030](../decisions/ADR-030-conexiones-personales-ia.md), [contrato y guía BYOK](../architecture/PERSONAL_AI.md), [auditoría/QA](../iterations/BYOK_1.md); también Identity, PostgreSQL, cuotas y contratos académicos de las filas anteriores |
| Seguridad/resiliencia/auditoría | [ADR-012](../decisions/ADR-012-identidad-y-autorizacion.md), [ADR-013](../decisions/ADR-013-persistencia-y-bootstrap.md), [ADR-017](../decisions/ADR-017-integridad-de-turnos.md), [ADR-025](../decisions/ADR-025-cuotas-auditoria-y-resiliencia.md), [operación](../architecture/OPERATIONS.md); además permisos del módulo afectado |
| Pruebas/despliegue/recuperación | [README: preparación y verificación](../../README.md), [OPERATIONS](../architecture/OPERATIONS.md), scripts existentes en scripts/; QA del recorrido afectado abajo |

## Alcance, pendientes e históricos — solo cuando aporten al encargo

Piloto interno ficticio Iteración 9: [escenario y guías](../pilot/README.md),
[resultados](../iterations/ITERATION_9.md), [matriz para reales](../pilot/READINESS.md)
y [plantilla pedagógica](../pilot/PEDAGOGICAL_VALIDATION.md). Completado como ensayo ficticio;
no autoriza datos/personas reales ni cuota externa. Consultar los contratos
académicos y operativos de las filas anteriores; no sustituye sus permisos.

Clases precargadas IA-2: [ADR-028](../decisions/ADR-028-clases-precargadas-y-evaluacion-agrupada.md),
[contrato/guía](../architecture/PRELOADED_CLASSES.md),
[estado/verificación](../iterations/IA_2.md). Consultar también clases, asignaciones,
turnos, evaluación, supervisión, proveedores/cuotas y frontend: el cambio es transversal.

Importación externa docente IA-1: [ADR-027](../decisions/ADR-027-importacion-externa-de-clases.md)
y [contrato/guía](../architecture/LESSON_IMPORT.md), junto con las filas de constructor,
frontend y permisos. No conecta una API ni sustituye el motor estudiantil.
Diagnóstico posterior de límites/errores: [QA de importación](../iterations/QA_IMPORT_DIAGNOSTICS.md).

- Maestro inmutable en raíz: alcance/arquitectura. [Plan](../iterations/PLAN.md) y
  [preguntas abiertas](../architecture/OPEN_QUESTIONS.md): pendientes y límites.
- Cierres actuales: [Iteración 8](../iterations/ITERATION_8.md), [UI-1](../iterations/UI_1.md).
- Importación externa cerrada: [IA-1](../iterations/IA_1.md).
- Clases precargadas y evaluación agrupada cerradas: [IA-2](../iterations/IA_2.md).
  Sus resultados son históricos, no pruebas ejecutadas por cada nueva tarea.
- Historial: docs/iterations/ITERATION_0.md a ITERATION_8.md; QA_3/4/5/6/7/8.md,
  QA_GROQ_FIX.md, evidence-* y docs/design/QA_UI_0.md. Elegir el informe del problema
  concreto, no cargar todos. El historial Git conserva el AGENTS anterior en 1966caf.
- [OPT-1](../iterations/OPT_1.md) explica la clasificación/preservación y la medición;
  consultarlo para auditar esta reorganización, no como contexto obligatorio diario.

- Rediseño UX-2 verificado: [UX-2](../iterations/UX_2.md), con auditoría,
  navegación y gráficos sobre los contratos actuales, con QA visual comparativa.
  [Afinado posterior](../iterations/UX_2_POLISH.md): redacción breve, Inicio por
  tareas y sección Proveedor de IA con los permisos existentes.
- Conexiones personales implementadas y verificadas con simulación: [BYOK-1](../iterations/BYOK_1.md).
  OAuth con cuenta real y requisitos de datos reales permanecen pendientes.
  Corrección posterior de límites nulos y filtrado de catálogos personales a
  modelos gratuitos confirmados: [QA del catálogo OpenRouter](../iterations/QA_OPENROUTER_CATALOG.md).
