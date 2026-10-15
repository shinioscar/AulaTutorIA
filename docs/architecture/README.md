# Arquitectura del piloto (Iteraciones 0–6)

Monolito modular. Dependencias permitidas:

```text
Web (Angular) → HTTP /api → Api → Application → Domain
                             ↓        ↑
                        Infrastructure
                             ↓
                   PostgreSQL / adaptadores IA
```

- Domain: organización, grupo, membresía, roles, aprobación docente y entidades
  de tutoría privada, ejercicios/intentos/diagnósticos/planes/reportes y permisos, sin frameworks.
- Application: IAiProvider/DTO/capacidades/errores/diagnóstico; identidad/grupos y reglas
  de autorización puras, DTO de tutoría y orquestador pedagógico inicial,
  independientes de EF/Identity; LearningOrchestrator valida propuestas/evidencias y
  ExerciseValidators determina coincidencias de ejercicios cerrados.
- Infrastructure: Identity/EF Core/Npgsql, servicios por recurso, bootstrap,
  Mock/Groq/Gemini/Ollama y transporte HTTP limitado sin SDK.
- Api: composición, Minimal APIs, filtro CSRF/DTO, logs JSON y OpenAPI con cookie/CSRF.
- Web: Angular standalone con router y formularios; sesión en cookies, navegación
  por permisos y proxy HTTPS de mismo origen. Sin SSR ni SDK IA.

PostgreSQL 18 tiene InitialIdentity, PrivateTutoring y LearningEvidence: identidad/grupos,
sesiones/mensajes/turnos privados y aprendizaje formativo versionado. Se siembran
roles/organización, sin usuarios predeterminados. Mantenimiento
explícito (--migrate / --bootstrap-admin), sin migrar en el inicio normal.
No se instala Redis, SignalR, colas, bibliotecas UI ni SDK de proveedores IA.

`GET /api/health` devuelve status, application, iteration, aiProvider y aiModel.
Confirma vida de la API/configuración IA sin red; conserva iteration=1 por
compatibilidad y no comprueba PostgreSQL. `GET /openapi/v1.json` solo Development.
Endpoints de identidad/grupos y /api/admin/ai/ con prueba explícita Admin+CSRF,
prompt fijo y máximo 128 tokens. Tutorías Student con autorización de propietario,
CSRF, historial durable y resumen local; contratos en TUTORING_HTTP.md.
Diagnóstico/plan/actividad/reporte usan los mismos turnos, leases y límites, nunca
transacciones abiertas durante IA. Contratos de evidencias/compartición/refuerzo en
LEARNING_HTTP.md; ADR-018/019. El profesor solo recibe un reporte autorizado con
relación vigente, sin permiso sobre historial/planes privados. Sin estadísticas del modelo.
Constructor docente en Lessons: Lesson/Draft/Revision/Version/Publication, propuestas
expresas y simulaciones aisladas. Valores Section/Objective/Exercise/Rubric en JSON
validado. LessonOrchestrator comparte TutoringOrchestrator y validadores de 4;
no inserta tutorías ni evidencias estudiantiles. Aprobación congela contenido y
publicación separada prepara referencia estable para 6. TeacherLessons es la cuarta
migración, aditiva; triggers de inmutabilidad y contratos en LESSONS_HTTP.md / ADR-021.
AssignedClasses es la quinta migración: asignación explícita de publicación/versión,
historial de cambios y vínculos de TutoringSession/LearningExercise. AssignedClassAccess
centraliza relación y disponibilidad; AssignedClassWorkflow integra núcleo aprobado y
progreso individual al motor de tutoría/evidencias, sin otro cliente IA. Triggers protegen
origen de asignación/sesión/ejercicio e historial. Cada alumno tiene sesión independiente;
refuerzo complementario no sustituye obligatorios. Cierre crea reporte local trazable,
sin cuota ni nota. Contratos en ASSIGNMENTS_HTTP.md y decisiones en ADR-023.
No supervisión docente avanzada ni acceso implícito a tutorías privadas.

HTTPS local para cookies Secure, con certificados excluidos en .cache. HTTP 5080
conserva salud y OpenAPI de desarrollo. Fuera de desarrollo se activa HTTPS/HSTS;
la configuración TLS/proxy de producción queda pendiente. Compose es de desarrollo.

Adaptadores en `Infrastructure/Ai/`; configuración validada, catálogo de capacidades
y errores seguros según ADR-014. Mock predeterminado, sin fallback ni llamadas
durante arranque/salud. Orquestación inicial en Application y persistencia/leases en
Infrastructure (ADR-016/017). Streaming, auditoría integral y cuotas distribuidas
permanecen pendientes. Contraseñas y búsqueda de miembros: ADR-015.

UI-0: temas globales en ThemeService/ThemeToggle y CSS custom properties,
sin acoplarse a identidad o HTTP. Light inicial, Dark explícito; solo preferencia
visual en localStorage y aplicación temprana (ADR-020). Guías oficiales y evidencia
en docs/design. Inicio demuestra la composición; los demás flujos conservan layout.
