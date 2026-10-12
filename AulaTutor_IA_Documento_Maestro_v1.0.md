# AULATUTOR IA
## Documento maestro del producto, arquitectura y plan de desarrollo

**Versión:** 1.0 (propuesta inicial)  
**Fecha:** 12 de octubre de 2026  
**Estado:** Base de trabajo para diseño y desarrollo; no representa una implantación institucional aprobada  
**Nombre:** «AulaTutor IA», provisional  
**Orientación:** MVP web responsive, desarrollo local-first, piloto universitario con datos ficticios y posterior estudio controlado.

> **Definición:** AulaTutor IA es una plataforma de acompañamiento académico con tutoría conversacional personalizada, ejercicios progresivos y reportes supervisables por docentes. Puede funcionar tanto en tutorías individuales solicitadas por el alumno como en clases guiadas y aprobadas previamente por el profesor.

---

## 1. Resumen ejecutivo

AulaTutor IA busca disminuir el tiempo entre la duda de un estudiante y la ayuda formativa que recibe, sin desplazar la responsabilidad pedagógica del docente. La aplicación ofrecerá un tutor de inteligencia artificial dentro de la propia interfaz, apoyado en un motor pedagógico y en registros verificables de la actividad.

**Dos modalidades iniciales:**

1. **Tutoría bajo demanda:** el alumno solicita ayuda sobre una asignatura o dificultad concreta. El sistema identifica el objetivo, realiza un diagnóstico breve, propone explicaciones y ejercicios, adapta la ayuda y permite cerrar la sesión con un resumen. No requiere que un profesor haya preparado de antemano cada conversación; sí requiere políticas educativas institucionales o del producto previamente configuradas.
2. **Clase guiada:** el profesor diseña o adapta un plan con apoyo de IA, lo prueba en una simulación de alumno, lo aprueba y publica una versión inmutable para un grupo. Cada estudiante sigue una sesión independiente con la misma meta curricular y actividades obligatorias comparables.

**Integración institucional futura:** la tutoría con IA podrá actuar como apoyo previo o posterior a una tutoría presencial existente. La integración con el portal universitario solo se realizará con autorización, acceso técnico documentado y reglas de protección de datos. El MVP no presupone acceso a dicho portal.

**Principio rector:** la IA ayuda a enseñar y propone retroalimentación; la plataforma determina permisos, estados y reglas, registra evidencias y conserva la decisión académica final para el docente.

## 2. Problema, oportunidad y objetivos

### 2.1 Problema

- Los grupos grandes limitan la atención individual y la retroalimentación inmediata.
- Algunas dudas pequeñas bloquean el aprendizaje cuando no existe una tutoría disponible.
- Las tutorías presenciales pueden dedicarse a repasar fundamentos que una herramienta guiada podría atender previamente.
- Los profesores carecen de evidencia homogénea sobre intentos, pistas utilizadas, errores comunes y avances durante el estudio autónomo.
- Un chat general de IA no garantiza planes aprobados, permisos académicos, registros comparables ni trazabilidad de evaluación.

### 2.2 Hipótesis a validar

«Una tutoría guiada con IA, ejercicios activos y revisión docente facilita la comprensión de temas específicos y mejora la oportunidad de la retroalimentación, sin disminuir la autonomía docente». Es una **hipótesis**, no un resultado demostrado por el proyecto.

### 2.3 Objetivos del MVP

**O1.** Permitir iniciar y terminar una tutoría académica totalmente dentro de AulaTutor IA.  
**O2.** Permitir que el profesor diseñe, simule, apruebe y asigne una clase reutilizable.  
**O3.** Registrar respuestas, intentos, pistas, resultados y el contexto mínimo necesario para auditoría.  
**O4.** Generar reportes claros, editables o comentables por el docente, sin calificar automáticamente de manera oficial.  
**O5.** Intercambiar proveedores de IA sin modificar clases ni historiales.  
**O6.** Operar un piloto pequeño con proveedor simulado y, cuando proceda, con cuotas gratuitas de API.

### 2.4 Fuera del alcance del primer MVP

Calificaciones oficiales; sustitución del profesor; integración automática con el sistema universitario de tutorías; pagos; videollamadas; seguimiento emocional o psicológico; aplicaciones móviles nativas; ejecución de código o SQL no confiable sobre infraestructura productiva; soporte multiinstitución completo; analítica predictiva de abandono; decisión automática de aprobar/reprobar materias.

## 3. Usuarios, roles y visibilidad

| Rol | Funciones principales | Restricción clave |
|---|---|---|
| Estudiante | Solicitar tutoría, cursar clases asignadas, consultar avances, revisar y compartir reportes | Accede solo a sus datos y clases autorizadas |
| Profesor/tutor | Preparar y aprobar lecciones, asignar a grupos, revisar evidencias y tutorías vinculadas | Accede únicamente a grupos/casos asignados o compartidos con fundamento y permiso |
| Administrador | Gestionar alta y validación de profesores, grupos, políticas y configuraciones institucionales | No accede por defecto al contenido completo de chats; accesos extraordinarios deben justificarse y auditarse |
| Operador técnico (posterior) | Salud del servicio, cuotas, incidentes | Priorizar métricas agregadas y datos seudonimizados |

**Control obligatorio:** registrarse como «profesor» no basta para obtener permisos docentes; se exige validación de rol. En el MVP se pueden precargar usuarios ficticios y aprobarlos manualmente.

## 4. Modalidades y reglas de producto

### 4.1 Modo A: tutoría individual bajo demanda (prioritario para piloto)

**Caso de uso:** «No entiendo `IF EXISTS` y tengo un examen».

1. El alumno elige materia, tema, objetivo y nivel percibido; puede describir su dificultad libremente.
2. El sistema presenta política de uso, privacidad y visibilidad del reporte; si corresponde, recoge consentimiento antes de compartirlo.
3. El motor inicia diagnóstico corto (1 a 3 preguntas), salvo si el alumno prefiere repaso directo.
4. La IA explica brevemente, pregunta una cosa por turno, analiza cada intento, ofrece pistas graduadas y ajusta la dificultad.
5. El alumno puede pedir explicación distinta, volver a fundamentos, pausar, reanudar o finalizar.
6. Al cierre, se registra un resumen con objetivos tratados, evidencias de ejercicios, ayudas, pendientes y recomendación de repaso.
7. Si el alumno no progresa o desea atención humana, puede solicitar una recomendación de tutoría presencial; el envío a un profesor requiere una relación válida y autorización correspondiente.

**Dos variantes de visibilidad:**
- **Práctica individual:** el informe permanece privado del estudiante de forma predeterminada; no se envía automáticamente a ningún docente.
- **Tutoría vinculada:** existe una solicitud/relación con un docente o programa de apoyo, y el estudiante conoce qué datos se compartirán; se registra el motivo y la autorización de acceso.

**Diferencia respecto a clase guiada:** la tutoría espontánea no necesita lección previamente aprobada, pero sí aplica `TutoringPolicyVersion` (reglas pedagógicas y límites), materiales permitidos y requisitos de seguridad aprobados por quien administra la plataforma.

### 4.2 Modo B: clase guiada y reproducible

1. El profesor crea título, objetivos, requisitos previos, duración orientativa, ejercicios y rúbricas.
2. La IA propone estructura y explicaciones; todo queda en estado `Draft`.
3. El profesor entra a `Simulation` y recorre la experiencia en calidad de alumno, pudiendo simular respuestas correctas e incorrectas.
4. El profesor modifica o acepta el plan y lo aprueba, creando una `LessonVersion` inmutable.
5. Se asigna a uno o más grupos como `Assignment`.
6. Cada alumno crea su propia `LearningSession` vinculada a la versión asignada.
7. El plan, objetivos y ejercicios obligatorios se mantienen estables; el tutor adapta explicaciones, pistas y ejercicios complementarios.
8. El profesor revisa reportes y evidencias; toda revisión o anotación conserva su autor y fecha.

**Regla:** editar una clase publicada produce una versión nueva; no modifica silenciosamente sesiones existentes.

### 4.3 Modo C: complemento de tutorías presenciales (fase posterior)

Flujo ideal: solicitud en el sistema institucional → oferta voluntaria de refuerzo con IA → sesión y autoevaluación → [resuelto] cierre o [persiste dificultad] derivación a tutor humano → informe previo resumido para el profesor autorizado. Se deben ofrecer rutas para acceder directamente a tutor humano; la IA no debe convertirse en un obstáculo para recibir atención presencial.

No se presupone que la universidad disponga de API pública, SSO o compatibilidad LTI. Inicialmente: `Referral` interno, exportación de informe y enlace informativo configurado por administrador; **sin automatizar la web ajena ni manipular sus cuentas**. Las integraciones se evaluarán con documentación y permisos institucionales.

## 5. Historias de usuario prioritarias

| ID | Historia | Prioridad |
|---|---|---|
| HU-01 | Como alumno, quiero iniciar una tutoría sobre un tema y recibir ejercicios progresivos | P0 |
| HU-02 | Como alumno, quiero pedir pistas y explicar mis dudas sin perder el punto de la sesión | P0 |
| HU-03 | Como alumno, quiero pausar y reanudar desde mi último ejercicio | P0 |
| HU-04 | Como profesor, quiero diseñar y simular una clase antes de aprobarla | P0 |
| HU-05 | Como profesor, quiero asignar una versión aprobada a un grupo | P0 |
| HU-06 | Como estudiante, quiero completar una clase con chat integrado y evaluación | P0 |
| HU-07 | Como docente, quiero revisar intentos, pistas, errores y reporte del alumno | P0 |
| HU-08 | Como administrador, quiero cambiar proveedor/modelo y limitar consumo | P0 |
| HU-09 | Como alumno, quiero controlar cuándo se comparte una tutoría individual | P0 |
| HU-10 | Como profesor, quiero identificar temas que dificultaron a varios alumnos | P1 |
| HU-11 | Como alumno, quiero solicitar atención humana cuando la IA no me ayude | P1 |
| HU-12 | Como institución, quiero vincular la solicitud de tutoría existente, con integración autorizada | P2 |

## 6. Requisitos funcionales

### 6.1 Identidad y organización

- **RF-01:** autenticación segura y gestión de sesiones.
- **RF-02:** roles `Admin`, `Teacher`, `Student` con autorización por recurso, grupo, institución y relación docente-alumno.
- **RF-03:** validación de la condición de docente antes de habilitar funciones privilegiadas.
- **RF-04:** grupos, membresías, invitaciones y asignaciones; no permitir enumerar alumnos ajenos.

### 6.2 Planificación

- **RF-05:** crear clase manualmente o con propuestas IA.
- **RF-06:** simulación docente aislada de analítica real.
- **RF-07:** aprobar y publicar `LessonVersion` inmutable, con objetivos y rúbricas.
- **RF-08:** asignar, retirar o reemplazar asignaciones respetando progreso existente.

### 6.3 Aprendizaje

- **RF-09:** iniciar tutoría bajo demanda con tema, objetivo y política configurable.
- **RF-10:** iniciar sesión de una clase asignada con el plan aprobado.
- **RF-11:** chat integrado de turnos, pistas escalonadas, reexplicación y ayuda contextual.
- **RF-12:** estado persistente por estudiante: tema, ejercicio, intentos, dificultades y metas dominadas.
- **RF-13:** mostrar una sola consigna principal por turno y evitar revelar la solución antes de lo permitido por la política.
- **RF-14:** pausar, reanudar y finalizar sesiones sin pérdida de datos confirmados.

### 6.4 Evidencia y evaluación

- **RF-15:** registrar cada intento antes de pedir a la IA que lo explique; identificador idempotente para evitar duplicados.
- **RF-16:** evaluar con validadores deterministas en ejercicios cerrados; para respuestas abiertas, aplicar rúbrica y revisión docente cuando corresponda.
- **RF-17:** diferenciar `correcto sin ayuda`, `correcto con ayuda`, `incorrecto`, `no evaluable` y `requiere revisión`.
- **RF-18:** generar reporte trazable a evidencias y con indicación de qué observaciones fueron generadas por IA.
- **RF-19:** gestionar visibilidad de la tutoría privada vs vinculada, registro de compartición y revocación conforme a política.
- **RF-20:** permitir revisión/comentario docente; no producir nota oficial automática.

### 6.5 Proveedores IA e infraestructura

- **RF-21:** conmutar por configuración entre `Mock`, `Groq`, `Gemini`, `Ollama`, `OpenAI` (según implementación/disponibilidad).
- **RF-22:** registrar modelo, versión de prompt, consumo estimado/real y errores por llamada.
- **RF-23:** políticas por caso de uso: tutoría, generación de lecciones, explicación y resumen.
- **RF-24:** `timeout`, cancelación, reintentos limitados, respuesta de error amigable y continuidad de la sesión.
- **RF-25:** límites por alumno, grupo, proveedor y día; no sobrepasar el presupuesto habilitado.

### 6.6 Solicitud de atención humana

- **RF-26 (P1):** derivación interna voluntaria a tutor humano con estado `Suggested`, `Requested`, `Acknowledged`, `Closed`.
- **RF-27 (P1):** resumen compartible con datos mínimos y consentimiento aplicable.
- **RF-28 (P2):** conectores institucionales autorizados por API, SSO o integración compatible, cuando existan.

## 7. Requisitos no funcionales y criterios del piloto

| Dimensión | Objetivo para piloto, sujeto a medición |
|---|---|
| Usabilidad | Experiencia responsiva desde PC y smartphone; alumno puede iniciar una tutoría en tres pasos o menos tras iniciar sesión |
| Accesibilidad | Navegación con teclado, foco visible, etiquetas y contraste adecuado; tomar WCAG 2.2 AA como referencia de diseño |
| Rendimiento | Medir p50/p95 de primera respuesta y respuesta completa por proveedor; **no prometer** una latencia fija dependiente de API externa |
| Disponibilidad | Estado y recuperación claros ante caída de proveedor; reanudación sin duplicar intentos |
| Seguridad | HTTPS fuera de localhost; secretos fuera de repositorio; autorización por recurso; auditoría; límites de peticiones |
| Portabilidad | API y base de datos levantables con Docker Compose, IA local opcional y proveedores remotos configurables |
| Privacidad | Minimización de datos, retención configurable, control de compartición, políticas institucionales antes de usar datos reales |
| Observabilidad | Logs estructurados sin contenido sensible innecesario, trazas con `correlationId`, consumo/costos por sesión |
| Mantenibilidad | Monolito modular, pruebas automatizadas y contratos de proveedores desacoplados |

**Criterio de éxito técnico del MVP:** un mismo conjunto de ejercicios aprobados puede ejecutarse con `Mock`, Groq y otro proveedor, guardando igual estructura de evidencias y sin cambios de esquema de clase. El nivel pedagógico equivalente entre modelos requiere pruebas y no está garantizado por la compatibilidad técnica.

## 8. Arquitectura propuesta

### 8.1 Stack de referencia

- **Frontend:** Angular + TypeScript, interfaz responsive. Versión exacta fijada al iniciar el repositorio conforme a compatibilidad y soporte.
- **Backend:** ASP.NET Core Web API sobre **.NET 10 (LTS)**, C#; EF Core; OpenAPI; validación explícita de DTO.
- **Datos:** PostgreSQL con migraciones EF Core.
- **Autenticación:** ASP.NET Core Identity; sesión segura / tokens según la arquitectura web definitiva. Para SPA, preferir cookies seguras HTTP-only y protección CSRF si frontend y backend comparten origen; si se adopta JWT, justificar almacenamiento y mitigaciones.
- **IA:** `IAiProvider`, adaptadores específicos y `MockAiProvider`; configuraciones por entorno.
- **Desarrollo:** Docker Compose para PostgreSQL y servicios auxiliares; `appsettings.*` sin secretos; variables de entorno / secret manager.
- **Entrega de respuesta:** primera versión mediante HTTP y estado de sesión; después streaming (SSE) con persistencia controlada. SignalR solo si se justifica para monitoreo en vivo.
- **Tareas diferidas:** servicio `BackgroundService` + outbox simple para reportes cuando resulte necesario; no introducir colas distribuidas prematuramente.

### 8.2 Estructura de solución sugerida

```text
AulaTutorIA/
  docs/
    MASTER_PLAN.md
    architecture/
    decisions/
    iterations/
  src/
    AulaTutor.Api/
    AulaTutor.Application/
    AulaTutor.Domain/
    AulaTutor.Infrastructure/
    AulaTutor.Web/                  # Angular
  tests/
    AulaTutor.UnitTests/
    AulaTutor.IntegrationTests/
    AulaTutor.E2E/
  deploy/
    compose.yaml
  scripts/
  AGENTS.md
  README.md
  .env.example
```

Capas: `Domain` (modelos y reglas), `Application` (casos de uso, contratos IA y DTO), `Infrastructure` (EF Core, adaptadores y servicios externos), `Api` (endpoints y autorización), `Web` (componentes y estados de interfaz).

### 8.3 Componentes lógicos

```text
Angular (estudiante/docente/admin)
          |
       HTTPS
          |
ASP.NET Core API  -> AuthZ por recurso
          |-- LessonsModule (draft, simulation, approval, assignment)
          |-- TutoringModule (on-demand, lesson session, state machine)
          |-- AssessmentModule (validators, attempts, rubric, reports)
          |-- ReferralsModule (future human tutoring handoff)
          |-- AiOrchestration (selection, prompt policy, budgets, retries)
          |        |-- MockProvider
          |        |-- GroqProvider
          |        |-- GeminiProvider
          |        |-- OllamaProvider
          |        `-- OpenAiProvider [future]
          |
       PostgreSQL
```

No crear microservicios para el MVP. Separar por módulos y contratos primero; extraer servicios solo con evidencia de necesidad.

## 9. Motor pedagógico y estados

### 9.1 Reglas comunes

- Iniciar con diagnóstico breve cuando aporte valor; omitirlo si el estudiante decide un repaso guiado.
- Explicar con mensajes cortos y solicitar participación activa.
- Una actividad principal a la vez; preservar contexto de dudas sin perder el hilo.
- Dos intentos antes de ofrecer la solución **como política por defecto configurable**, no regla universal.
- En errores: identificar causa, dar pista proporcional y permitir reintentar; nunca humillar ni inventar dominio.
- Aumentar dificultad al demostrar comprensión; simplificar ante fallos repetidos.
- Distinguir comprensión observada de inferencias del modelo.
- Si un alumno solicita una persona o muestra bloqueo persistente, ofrecer derivación sin obligarlo a continuar con IA.

### 9.2 Estados propuestos

**Plan de clase:** `Draft → Simulation → InReview → Approved → Published → Archived`. Se permite volver a `Draft` antes de aprobar; después, cualquier cambio crea otra versión.

**Sesión:** `Created → Diagnosing → Teaching → Practicing ↔ Reinforcing → Completed` con estados alternativos `Paused`, `Cancelled`, `Escalated`. La transición válida es decisión de servidor y queda auditada.

**Intento:** `Submitted → Validated / NeedsReview → FeedbackDelivered`. Ningún modelo puede declarar `Validated` sin pasar por los criterios del ejercicio o la revisión aplicable.

### 9.3 Plan reproducible y tutoría flexible

En clases guiadas, `LessonVersion` contiene objetivos fijos, ejercicios obligatorios, rúbricas y límites. En tutoría libre, `TutoringPolicyVersion` controla seguridad, metodología y límites, mientras `TutoringGoal` establece el objetivo emergente. Ambos recorren el mismo `LearningSession` y generan evidencias con un campo `sessionMode`.

## 10. Evaluación, calidad pedagógica y reportes

**Evidencia primaria:** ejercicio, consigna, versión, respuesta, marca temporal, ayudas, intentos, validación y resultado. **Evidencia secundaria:** interpretación del modelo, explicación y resumen. La evidencia primaria nunca debe reemplazarse por texto generado.

**Clasificación mínima de ejercicio:**
- Opción múltiple / verdadero-falso: validador determinista.
- Respuesta corta: comparador con alternativas permitidas + reglas; revisión si ambiguo.
- SQL y código: validadores semánticos o pruebas en contenedor aislado, con límites de recursos y permisos; no ejecutar sobre la base productiva.
- Respuesta extensa: rúbrica explícita, justificación, confianza limitada y marca `NeedsReview` donde se requiera criterio humano.

**Reporte del estudiante:** modo, objetivos, actividades realizadas, correctos sin ayuda/con ayuda, ayudas utilizadas, errores recurrentes con evidencias referenciadas, pendientes y recomendaciones; indicación visible de «generado por IA / sujeto a revisión».

**Reporte del docente:** progreso individual (solo de sus estudiantes autorizados), dificultades comunes a nivel de grupo, filtro por objetivo; posibilidad de abrir evidencias para contrastar resumen. En tutoría privada del alumno no compartir datos personales automáticamente con profesores.

**Importante:** cantidad de mensajes, tiempo en pantalla o uso de pistas no son sustitutos directos del aprendizaje. Las métricas deben interpretarse pedagógicamente.

## 11. Proveedores de IA: contrato, conmutación y costos

### 11.1 Interfaz conceptual

```csharp
public interface IAiProvider
{
    string ProviderId { get; }
    Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct);
    Task<AiHealthResult> CheckHealthAsync(CancellationToken ct);
}

public sealed record AiChatRequest(
    string UseCase,
    IReadOnlyList<AiMessage> Messages,
    AiResponseContract ResponseContract,
    string CorrelationId);

public sealed record AiChatResult(
    string Text,
    string ProviderId,
    string ModelId,
    AiTokenUsage? Usage,
    string? StructuredPayload,
    string? FinishReason);
```

No exponer `ApiKey` al navegador. Las implementaciones de cada proveedor traducen su formato al contrato común; no asumir que OpenAI, Gemini, Groq y Ollama tienen capacidades idénticas de herramientas, streaming o JSON estricto.

### 11.2 Política de selección

- `Mock` es predeterminado en pruebas automáticas y entorno de demostración sin IA externa.
- Para laboratorio, habilitar Groq o Gemini según cuotas y términos vigentes; Ollama local para pruebas sin costos de API.
- Registrar en cada llamada: proveedor, modelo, versión de configuración/prompt, uso y tipo de error.
- Configurar separación entre `lesson_generation`, `tutoring`, `assessment_assistance` y `report_summarization`.
- **Fallback** solo cuando el usuario/administrador autorice el proveedor de destino, el tratamiento de datos sea compatible y no cambie la política de privacidad. Nunca reenviar automáticamente datos sensibles a otro tercero solo para eludir un límite.
- Al recibir `429`: respetar `Retry-After`, usar espera limitada y mostrar un estado claro; evitar tormenta de reintentos.
- A nivel de servidor: cuotas por estudiante, proveedor y organización; límites diarios y botón administrativo para detener consumo.

**Advertencia de viabilidad:** nivel gratuito no equivale a capacidad garantizada para todo un salón; disponibilidad, límites, tratamiento de datos y modelos pueden cambiar. No incluir tarifas fijas como promesa contractual. Un piloto gratuito puede tener costos de electricidad, dominio, red y mantenimiento.

### 11.3 Calidad de respuesta

Preferir salidas con JSON Schema si el proveedor/modelo lo soporta; validar igualmente en backend tipos, rangos, IDs permitidos y transiciones. Separar respuesta pedagógica visible del objeto interno de evaluación; los mensajes del alumno, PDFs o recursos consultados son entrada no confiable y no pueden sobrescribir instrucciones de sistema ni conceder permisos.

## 12. Modelo de datos lógico inicial

| Entidad | Campos conceptuales clave | Observación |
|---|---|---|
| `Organization` | id, name, policyVersion | Una institución piloto; base preparada para ampliar |
| `User` | id, organizationId, displayName, email, status | Sin guardar secretos en texto plano |
| `RoleAssignment` | userId, role, scope | Validar roles y alcance |
| `Group` / `GroupMember` | groupId, userId, memberRole | Separar grupos de roles globales |
| `Lesson` | id, ownerTeacherId, title | Identidad general |
| `LessonVersion` | lessonId, version, contentJson, approvedBy, approvedAt, status | Inmutable tras publicar |
| `LessonAssignment` | id, lessonVersionId, groupId, schedule | Vincula clase y grupo |
| `TutoringPolicyVersion` | id, rulesJson, approvedBy, version | Tutorías bajo demanda |
| `TutoringGoal` | id, studentId, subject, topic, request | Objetivo emergente |
| `LearningSession` | id, studentId, mode, goalId?, lessonVersionId?, state, visibility, policyId | Solo uno de goal/lesson según modo |
| `SessionMessage` | id, sessionId, role, text, createdAt | Retención y acceso restringidos |
| `Exercise` | id, lessonVersionId?, objectiveId, type, rubricJson | Puede ser ad hoc en tutoría, identificado como tal |
| `ExerciseAttempt` | id, exerciseId, sessionId, answer, attemptNumber, hintCount, status | Clave idempotente para envío |
| `Evaluation` | id, attemptId, validatorType, outcome, evidenceRef, modelInfo? | Resultado reproducible cuando posible |
| `SessionReport` | id, sessionId, reportJson, generatedAt, reviewStatus | Vínculo a evidencia primaria |
| `ReportShare` | id, reportId, recipientId, legalBasis/consentRef, grantedAt, revokedAt | Acceso verificable |
| `HumanReferral` | id, sessionId, requestedBy, state, targetTeacherId? | Opt-in; fase posterior |
| `AiCallLog` | id, correlationId, provider, model, tokenCounts, latencyMs, status | Evitar guardar prompts sensibles innecesariamente |
| `AuditEvent` | id, actor, action, resource, timestamp, metadata | Accesos y cambios críticos |

**Restricciones:** claves foráneas e índices por institución/usuario/grupo; versiones únicas por lección; integridad de estados; políticas de borrado; evitar cascadas que eliminen evidencias obligatorias sin proceso autorizado.

## 13. Contratos HTTP iniciales (borrador)

```text
POST   /api/auth/login
GET    /api/me
GET    /api/students/me/tutoring-sessions
POST   /api/tutoring-sessions
GET    /api/tutoring-sessions/{id}
POST   /api/tutoring-sessions/{id}/messages
POST   /api/tutoring-sessions/{id}/attempts
POST   /api/tutoring-sessions/{id}/pause
POST   /api/tutoring-sessions/{id}/complete
GET    /api/tutoring-sessions/{id}/report
POST   /api/tutoring-sessions/{id}/shares
DELETE /api/tutoring-sessions/{id}/shares/{shareId}
POST   /api/lessons
POST   /api/lessons/{id}/versions
POST   /api/lesson-versions/{id}/simulate
POST   /api/lesson-versions/{id}/approve
POST   /api/lesson-versions/{id}/publish
POST   /api/lesson-versions/{id}/assignments
GET    /api/teacher/groups/{id}/progress
GET    /api/teacher/reports/{reportId}
GET    /api/admin/ai-providers
PATCH  /api/admin/ai-routing
GET    /api/admin/usage
```

Cada endpoint exige política de autorización por relación de negocio, no únicamente por rol. Crear versiones desde `POST /lessons/{id}/versions` puede requerir cambio de ruta final; las rutas son referencias y deben formalizarse mediante OpenAPI. Para mutaciones críticas, `Idempotency-Key`, `409 Conflict` y concurrencia optimista donde aplique.

## 14. Seguridad, privacidad y ética

**Controles mínimos:**

1. HTTPS y políticas CORS restrictivas, cabeceras seguras, validación estricta y anti-CSRF según mecanismo de autenticación.
2. Permisos por institución, grupo, relación de tutoría y recurso; no confiar en `studentId` enviado por navegador si puede derivarse de la sesión autenticada.
3. Secretos mediante configuración de entorno; fallar al iniciar en producción si falta un secreto crítico.
4. Cifrado en tránsito, controles de acceso a respaldos y eliminación conforme a política documentada.
5. Privacidad por defecto en tutoría individual; consentimiento/autorización cuando sea aplicable antes de compartir; transparencia sobre análisis con IA.
6. Redacción/minimización de información sensible antes de enviar datos a proveedores cuando sea viable.
7. Resistencia a prompt injection; sanitización y límites de adjuntos; recursos y mensajes tratados como contenido no confiable.
8. Restricciones contra uso del tutor para tomar decisiones disciplinarias, emitir diagnósticos de salud o sustituir atención humana requerida.
9. Solicitudes de acceso, corrección y eliminación, y períodos de retención definidos por la entidad que trate los datos y la normativa aplicable.
10. Revisión jurídica e institucional **antes** de incluir datos personales reales de estudiantes, especialmente si son menores de edad.

**Diseño propuesto de privacidad:** que el alumno vea una sección «Quién puede consultar esta sesión» y que el sistema diferencie conversación completa, evidencias de ejercicios y reporte resumido. El profesor solo recibirá los datos permitidos por la modalidad y la relación académica.

## 15. Experiencia de usuario y pantallas

### 15.1 Estudiante

Inicio con dos acciones principales: **Solicitar tutoría** y **Mis clases**. Dentro de sesión: objetivo visible, progreso, tarjeta de ejercicio, chat, respuestas rápidas («dame una pista», «explícalo diferente», «otro ejemplo»), botón «quiero atención humana», pausa y cierre. Mostrar aviso sobre límites de IA sin distraer.

### 15.2 Profesor

Inicio con **Crear clase**, **Simular**, **Mis grupos** y **Reportes**. El constructor muestra objetivos/rúbricas a la izquierda y conversación de diseño a la derecha. En revisión: vista resumida, evidencias originales, anotaciones y exportación posterior.

### 15.3 Administrador

Gestión de roles y políticas, matriz de proveedores habilitados, límites, estado de servicio y auditoría; sin una pantalla que permita recorrer indiscriminadamente chats privados.

## 16. Integración con el sistema existente de tutorías de la universidad

**Objetivo:** complementar la gestión presencial; disminuir solicitudes que solo requieren práctica guiada y dar contexto al profesor cuando una tutoría humana sea necesaria.

**Etapa I — Sin integración:** AulaTutor opera de forma independiente. El alumno puede completar una tutoría IA, descargar o compartir voluntariamente su resumen y reservar por los canales universitarios existentes.

**Etapa II — Enlace superficial autorizado:** el portal de tutorías muestra un enlace a AulaTutor, o viceversa, sin transferir datos personales ni automatizar credenciales.

**Etapa III — Integración formal:** después de aprobación universitaria, evaluar API, SSO/OIDC, webhooks o LTI 1.3 si el sistema anfitrión soporta el estándar. Definir intercambio de identidad y de solicitud, base jurídica/consentimiento, registro de eventos y límites de acceso.

**Métricas de esa integración:** tutorías IA utilizadas antes de cita presencial, satisfacción, porcentaje que solicita atención humana, errores persistentes por tema y tiempo de preparación del docente. **No considerar la reducción de tutorías presenciales como éxito por sí sola** si perjudica el aprendizaje o limita el acceso al profesor.

## 17. Plan de pruebas, investigación educativa y métricas

### 17.1 Pruebas técnicas

- Unitarias: reglas de avance, autorización por recurso, validadores, generación de reportes, presupuesto.
- Integración: EF Core/PostgreSQL, migraciones, concurrencia, transiciones, estados y auditoría.
- Contratos IA: `Mock`, esquemas de respuesta válidos/inválidos, proveedor lento, caída de red, `429`, timeouts, cancelación.
- E2E: profesor publica → asigna → alumno practica → recibe retroalimentación → docente revisa; y alumno pide tutoría libre → pausa → reanuda → decide compartir.
- Seguridad: alumno A no ve B, profesor X no ve grupo Y, admin no accede a chat privado sin procedimiento permitido, no filtración de claves.
- Carga: escenarios de 1, 5 y 30 alumnos simulados; métricas separadas de límites de proveedor.

### 17.2 Piloto académico (propuesta, no experimento aprobado)

Tema sugerido: **SQL Server: procedimientos almacenados, parámetros e `IF EXISTS`**.

Diseño preliminar: encuesta y prueba diagnóstica; uso voluntario de tutoría libre y/o clase guiada; prueba final independiente de IA; cuestionario de claridad y utilidad; profesor revisa una muestra de reportes vs evidencia. Para afirmar mejoras de aprendizaje habrá que diseñar comparación adecuada, controlar sesgos, recopilar consentimiento requerido y obtener autorización institucional cuando proceda.

**Indicadores:** finalización, respuestas correctas en prueba sin ayuda, mejora pre/post, tiempo hasta primera explicación útil, resolución tras pistas, satisfacción del alumno, tiempo de revisión docente, tasa de derivación humana, tasa de contradicciones detectadas y costos por sesión.

**No usar como indicador único:** «porcentaje de ejercicios que GPT calificó como correctos».

## 18. Plan de desarrollo por iteraciones para Codex

**Norma de ejecución:** una iteración a la vez; cada una debe dejar compilación estable, pruebas y registro de cambios. No avanzar solo porque el código compile; revisar criterios de aceptación y capturas del flujo real.

| Iteración | Entregables | Condición de aceptación |
|---|---|---|
| 0. Fundaciones | Repositorio, arquitectura, `AGENTS.md`, `.env.example`, Compose, decisiones, CI local | API y web arrancan con `Mock`; secretos excluidos |
| 1. Identidad | Registro/login piloto, roles, validación docente, grupos, autorización | Pruebas de acceso cruzado pasan |
| 2. Proveedores IA | Contrato, Mock, Groq, configuración/telemetría; interfaz para Gemini y Ollama | Sustitución de proveedor sin tocar casos de uso |
| 3. Tutoría libre | Inicio, diagnóstico, mensajes, ejercicios básicos, pausa/reanudación | Alumno finaliza sesión completa sin pestañas externas |
| 4. Evidencias | Intentos, pistas, validadores cerrados, reportes, control de compartición | Reporte corresponde a intentos almacenados |
| 5. Constructor de clases | Draft, simulación, rúbricas, aprobación y versiones | Publicación no altera versiones anteriores |
| 6. Clases asignadas | Grupos, asignación, sesión por alumno, ejercicios obligatorios | Dos alumnos completan recorridos distintos con misma versión |
| 7. Panel docente | Revisiones, progreso por objetivo, casos con atención humana pendiente | Solo se ven alumnos vinculados y datos autorizados |
| 8. Resiliencia y seguridad | Límites, errores 429, logs, auditoría, pruebas E2E y concurrencia | No se pierden ni duplican intentos tras caída recuperable |
| 9. Piloto | Clase SQL de referencia, tutores ficticios, guías y medición | Flujo completo documentado y evaluable |
| 10. Integraciones (futuro) | Derivación y conector institucional si hay permiso | No se requiere para MVP |

### 18.1 Definición de terminado de una iteración

Código compila; pruebas relevantes pasan; migraciones reproducibles; endpoints documentados; controles de autorización probados; errores visibles al usuario; secretos fuera del repositorio; README/decisión actualizada; demostración manual mínima; sin tocar proyectos ajenos.

## 19. Riesgos y mitigaciones

| Riesgo | Consecuencia | Mitigación |
|---|---|---|
| Cuotas gratis insuficientes | Retrasos o bloqueo con muchos alumnos | Mock, límites, cola, grupos piloto pequeños y métricas |
| Alucinaciones/evaluación inexacta | Retroalimentación errónea | Material aprobado, pruebas deterministas, evidencias y revisión |
| Modelos heterogéneos | Diferencias pedagógicas entre sesiones | Plan fijo, contrato común, pruebas comparativas, versionado |
| Fuga de información estudiantil | Impacto legal y de confianza | Datos ficticios primero, permisos, minimización, política de proveedor |
| Sobredependencia de IA | Menor aprendizaje autónomo | Pistas antes que soluciones, evaluación sin ayuda, tutor humano disponible |
| Integración institucional inviable | Alcance atrasado | MVP independiente y conexión opcional posterior |
| Concurrencia y sesiones duplicadas | Reportes corruptos | Idempotencia, transacciones, índices únicos y estado en servidor |
| Complejidad excesiva | MVP inconcluso | Monolito modular, alcance congelado y progresión por iteración |
| Uso no autorizado como nota oficial | Consecuencias académicas injustas | Etiquetas formativas, revisión docente, sin módulo de calificación oficial |

## 20. Decisiones arquitectónicas (ADR iniciales)

**ADR-001. Web primero:** Angular responsive; no MAUI en MVP.  
**ADR-002. Monolito modular:** ASP.NET Core + PostgreSQL; sin microservicios.  
**ADR-003. IA intercambiable:** `IAiProvider` con proveedor simulado y adaptadores.  
**ADR-004. Dos modalidades, un motor:** tutoría libre y clase aprobada comparten sesiones/evidencias, no las políticas.  
**ADR-005. Versionado obligatorio:** planes y políticas inmutables tras aprobación.  
**ADR-006. Evaluación trazable:** hechos primero, explicación de IA después.  
**ADR-007. Privacidad por defecto:** tutoría libre personal no visible automáticamente al profesor.  
**ADR-008. Integración universitaria opcional:** no asumir acceso al portal de tutorías.  
**ADR-009. Experiencia dentro de AulaTutor:** nada de pestañas externas como parte normal de la tutoría.  
**ADR-010. Piloto sin notas oficiales:** sistema de apoyo formativo, no sustituto del juicio docente.

## 21. Escenario de demostración end-to-end

**Alumno A:** inicia una tutoría «No entiendo `IF EXISTS`». El tutor pregunta qué hace `EXISTS`, plantea un caso corto, proporciona pistas si falla y pide escribir una validación. El alumno completa dos ejercicios, pausa, reanuda y cierra. Se genera un reporte personal con áreas de mejora; puede decidir compartirlo con su docente si está habilitada esa relación.

**Profesor B:** crea «Procedimientos almacenados - introducción», define objetivos y tres ejercicios obligatorios, usa simulación, corrige una pista demasiado reveladora, aprueba versión 1 y la asigna al grupo SQL-01. Dos alumnos progresan a distinta velocidad; el profesor ve resultados por ejercicio y evidencia sin que la IA cambie la rúbrica oficial.

**Cambio de proveedor:** administrador cambia de Groq a Gemini en entorno de ensayo; una nueva sesión utiliza Gemini, mientras se conserva la referencia de proveedor/modelo en reportes anteriores. Las `LessonVersion` permanecen idénticas.

## 22. Criterios de aceptación del MVP global

- [ ] El alumno inicia, completa y reanuda una tutoría libre en la misma aplicación.
- [ ] El estudiante recibe explicaciones breves, ejercicios, retroalimentación y pistas reguladas.
- [ ] El profesor crea, simula, aprueba y asigna una clase versionada.
- [ ] Dos estudiantes tienen sesiones independientes sobre la misma clase aprobada.
- [ ] Cada intento queda guardado con su propia evaluación y trazabilidad.
- [ ] El docente puede revisar únicamente reportes y evidencias que tiene derecho a ver.
- [ ] El estudiante controla la compartición de tutorías privadas según modalidad/política.
- [ ] Hay al menos dos proveedores reales integrables, además de `Mock` (la disponibilidad efectiva depende de cuentas y cuotas).
- [ ] Errores de IA o límite de cuota no destruyen el avance confirmado.
- [ ] Las pruebas de autorización, recuperación y flujos E2E pasan.
- [ ] Existen instrucciones de despliegue local, variables de entorno y datos de demostración.
- [ ] No se efectúan evaluaciones oficiales ni integraciones universitarias sin aprobación explícita.

## 23. Primer encargo recomendado a Codex (iteración 0)

> Trabaja exclusivamente en la iteración 0 del proyecto AulaTutor IA, usando este documento maestro como fuente de verdad. Crea un repositorio y estructura de solución limpios para Angular, ASP.NET Core .NET 10 y PostgreSQL. Prepara README, `AGENTS.md`, plan de iteraciones, decisiones arquitectónicas, `.gitignore`, `.env.example` y Docker Compose. Incluye un `MockAiProvider` y un endpoint básico de salud sin conectar cuentas externas. No implementes todavía funcionalidades de tutoría, clases, registro de usuarios o reportes. Prioriza el disco/ruta del proyecto que se te indique, evita tocar otros proyectos y entrega un resumen con archivos, comandos probados, errores y próximos pasos. No coloques claves reales en el código ni subas contenido a repositorios remotos sin autorización.

## 24. Fuentes y documentación de referencia

**Fuentes técnicas oficiales consultadas el 12 de octubre de 2026. Los planes, precios, características y límites de terceros pueden cambiar.**

1. Microsoft. Ciclo de vida de .NET: https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core
2. Groq. Límites y cuotas por modelo: https://console.groq.com/docs/rate-limits
3. Groq. Compatibilidad con clientes OpenAI: https://console.groq.com/docs/openai
4. Groq. Structured Outputs: https://console.groq.com/docs/structured-outputs
5. Google AI for Developers. Precios y condiciones de Gemini API: https://ai.google.dev/gemini-api/docs/pricing?hl=es
6. Ollama. Compatibilidad OpenAI: https://github.com/ollama/ollama/blob/main/docs/api/openai-compatibility.mdx
7. OpenAI. Data controls API: https://developers.openai.com/api/docs/guides/your-data
8. 1EdTech. Learning Tools Interoperability: https://www.1edtech.org/standards/lti
9. OWASP. LLM01 Prompt Injection: https://github.com/OWASP/www-project-top-10-for-large-language-model-applications/blob/main/2_0_vulns/LLM01_PromptInjection.md
10. UNESCO. Guidance for Generative AI in Education and Research: https://www.unesco.org/en/articles/guidance-generative-ai-education-and-research

---

**Control de cambios:** v1.0 integra tutorías bajo demanda como modalidad prioritaria y clases aprobadas por profesores como segunda modalidad principal. Documento listo para iteración 0; requisitos y decisiones podrán evolucionar mediante ADR y versiones posteriores sin modificar retroactivamente la evidencia del piloto.
