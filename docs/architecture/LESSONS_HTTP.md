# Contratos del constructor docente — Iteración 5

Todas las rutas exigen sesión. Solo Teacher activo/aprobado y propietario del
piloto. Admin no recibe permiso implícito. 401 anónimo/revocado, 403 sin capacidad
para crear/listar, 404 recurso ajeno, 400 datos/CSRF/HTTPS, 409 revisión/estado,
429 límite o RetryNotBefore. Cookies/CSRF conforme ADR-012. OpenAPI Development
documenta DTO, cookie, X-XSRF-TOKEN, códigos y 202 en generaciones pendientes.

| Método y ruta | Datos / resultado |
|---|---|
| GET /api/lessons/?page=1 | Clases propias, páginas de 50, título/estado/revisión/fecha |
| POST /api/lessons/ | requestId, content → 201 LessonView; admite borrador incompleto, sin IA |
| GET /api/lessons/{id} | Borrador, validaciones, propuestas, versiones, operaciones y simulaciones propias |
| PUT /api/lessons/{id}/draft | revision, content → nueva revisión; versión aprobada nunca editable |
| POST /api/lessons/{id}/design | requestId, revision, action, text, retry → 200/202; Plan/Content/Exercises/Rubrics |
| POST /api/lessons/{id}/design/{requestId}/cancel | {} → cancela/cerca resultado, sin borrar solicitud |
| POST /api/lessons/{id}/apply-proposal | requestId, revision, targetId → aplicación expresa; rechaza desactualizada |
| POST /api/lessons/{id}/discard-proposal | Mismos campos → conserva propuesta como descartada |
| POST /api/lessons/{id}/review | requestId, revision → InReview y resumen/validaciones |
| POST /api/lessons/{id}/approve | requestId, revision → Approved + LessonVersion completa inmutable |
| POST /api/lessons/{id}/publish | requestId, revision, targetId=versionId → publicación expresa vigente |
| POST /api/lessons/{id}/new-draft | requestId, revision → copia editable nueva; conserva versiones |
| POST /api/lessons/{id}/archive | requestId, revision → archivo de clase/publicaciones sin borrar contenido |
| POST /api/lessons/{id}/simulations | requestId, revision → copia aislada de revisión; sin generación |
| GET /api/lessons/{id}/simulations/{simulationId} | Posición, contenido/consigna pública y conversación de prueba; sin soluciones privadas |
| POST /api/lessons/{id}/simulations/{simulationId}/messages | requestId, revision de simulación, action, text, retry → 200/202 |
| POST /api/lessons/{id}/simulations/{simulationId}/stop | requestId, revision → detiene y cancela turno vigente |
| POST /api/lessons/{id}/simulations/{simulationId}/resume | requestId, revision → reabre posición, sin IA |
| POST /api/lessons/{id}/simulations/{simulationId}/cancel | requestId nuevo, revision, targetId=turno pendiente → cancelación cercada |

LessonContent: title/subject (160), description/prerequisites (1000), level (100),
durationMinutes (0–480), methodology/instructions (1500), objectives[] y sections[].
Objetivo: id (1–40 ASCII alfanumérico/guion/underscore), text (500). Sección: id,
title (160), content (2500), exercises[]. Orden por array, sin drag-and-drop.
Actividad: id, objectiveId existente, required, activity de Iteración 4
(prompt/kind/options/acceptedAnswers/rubric) y rubric docente
(criteria[≤6,300], referenceSolution[1500], hintRules[600]). IDs únicos en todo el plan.
Los borradores admiten consignas/rúbricas/opciones incompletas; aprobación y propuestas
IA requieren reglas completas verificadas por LearningOrchestrator.ValidateExercise. Máximo
20k caracteres de contenido completo, 12 objetivos/secciones y 24 actividades.

LessonView.content/proposals/versions son DTO privados del propietario docente:
contienen claves/rúbricas necesarias para revisión. Nunca se devuelven a Student.
LessonSimulationView.Exercise solo id/kind/prompt/options; no claves, referencias
ni rúbricas internas. Answer acepta texto (1–2000); Message también, y ninguna
otra acción de simulación acepta texto. Acciones: Start/Answer/Message/Hint/Simplify/Next.
Start inicia una sola vez. Next exige respuesta en actividad obligatoria, conservando
resultado incorrecto/orientativo para que el docente pueda comprobar el flujo.

IA siempre es explícita: crear/leer/editar/aplicar/revisar/aprobar/publicar/abrir/
reanudar no generan. LessonOperation devuelve clave, action/text, DraftRevision,
estado, isGenerating, error seguro, attempts, retryNotBefore, reply y outcome/type.
No expone ContextJson, lease ni secretos. Retry usa misma clave/action/text con
revisión actual; máximo tres ejecuciones; no retry automático desde GET. Una
operación fallida bloquea la siguiente hasta retry/cancel; edición manual sigue posible.

Referencias: ADR-021, TUTORING_HTTP.md y LEARNING_HTTP.md. No assignments ni
endpoint de ejecución estudiantil; no actualización directa de versiones.

Ampliación IA-1: [contrato externo y rutas de importación](LESSON_IMPORT.md),
ADR-027. Crea exclusivamente clases nuevas tras vista previa; el ciclo anterior
permanece intacto. Asignaciones posteriores siguen ADR-023.

IA-2/ADR-028 añade PreloadedPlan y LessonImportV2, manteniendo V1. Modalidad y
ayudas se congelan con el contenido académico; simulación precargada es local.
Editor y resumen privados incluyen las ayudas completas. Student recibe solo las
entregadas por el servidor. [Contrato precargado](PRELOADED_CLASSES.md).
