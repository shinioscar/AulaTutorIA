# Clases asignadas — HTTP de Iteración 6

IA-2 añade [modalidad precargada y tarjetas](PRELOADED_CLASSES.md) sobre las mismas
versiones/asignaciones/sesiones. Las versiones previas conservan su modalidad.

Todas las rutas exigen sesión HTTPS del mismo origen y Cache-Control no-store.
Mutaciones exigen X-XSRF-TOKEN. Actor/organización proceden de Identity; ningún DTO
acepta studentId/teacherId/organizationId. Reglas completas en ADR-023.

| Método / ruta | Capacidad y respuesta |
|---|---|
| GET /api/teacher/assignments/published-versions | Teacher aprobado; publicaciones propias no archivadas con ID/número/hash/título/asignatura/fecha |
| GET /api/teacher/assignments/authorized-groups | Solo grupos con membresía Teacher vigente del actor, incluso con rol Admin adicional |
| GET /api/teacher/assignments/ | Historial propio con relación vigente, estado/revisión/fechas y cambios; máximo 300 |
| GET /api/teacher/assignments/{id} | Estado/historial propio; destino del Location de creación, 404 sin relación/capacidad |
| POST /api/teacher/assignments/ | CreateAssignmentRequest; 201 con AssignmentView, también replay |
| POST /api/teacher/assignments/{id}/schedule | Fechas opcionales UTC; conserva versión y grupo |
| POST /api/teacher/assignments/{id}/deactivate | Inactive; conserva evidencia, bloquea trabajo |
| POST /api/teacher/assignments/{id}/activate | Reactiva si no hay equivalente Active |
| POST /api/teacher/assignments/{id}/withdraw | Withdrawn terminal |
| GET /api/students/me/classes/ | Solo relaciones vigentes del estudiante; progreso propio y disponibilidad |
| GET /api/students/me/classes/{id} | Detalle de versión asignada sin soluciones/reglas de corrección privadas |
| POST /api/students/me/classes/{id}/start | GUID requestId; 200 con sesión propia única, sin generar IA |

CreateAssignmentRequest: requestId, lessonVersionId, groupId, availableFrom?,
availableUntil?. Solo versiones publicadas propias. Clave ligada a todos los datos,
normalizando instantes a UTC. Fecha final debe ser posterior a la inicial.
AssignmentCommandRequest: requestId, revision y fechas únicamente para schedule.
Respuesta 200 con estado/revisión/historial; replay no duplica el cambio. Fechas
son intervalo inclusivo inicial/exclusivo final. Null elimina el extremo temporal.

AssignmentView incluye IDs fuente/grupo/docente, número/hash, título/asignatura,
nombres relacionados, estado, revisión, creación, fechas, Available y Changes.
AssignedClassView excluye identidades de compañeros, borradores, acceptedAnswers,
soluciones y rúbricas privadas. Incluye objetivos/secciones seguros, sesión propia
si existe, StudentState Available/InProgress/Paused/Completed/Unavailable,
DeliveredRequired/TotalRequired y VerifiedRequired (coincidencias de reglas cerradas).

La sesión usa las rutas existentes /api/tutoring-sessions/{id}: GET, messages,
pause/resume/complete y turns/{requestId}/cancel. TutoringSessionView añade Mode y
AssignedClass (clase, ejercicio actual, sección, CanComplete, motivo solo lectura).
ExerciseView añade LessonVersionId/ApprovedExerciseId/ApprovedObjectiveId/Required/
IsComplementary. AttemptView conserva ejercicio/versión/respuesta/resultado/ayudas/fecha;
el vínculo a la versión fuente permanece en LearningExercise y la sesión.

Start/Message/Hint/Simplify/SubmitAttempt/NextActivity/GenerateExercise/FinalReport
son acciones explícitas sobre el mismo motor, con sus límites/revisión/retry. No
DiagnosticQuestion/AnalyzeDiagnosis/GeneratePlan/ActivatePlan en clase asignada.
Comenzar conversación es un turno posterior explícito; iniciar/leer/reanudar no IA.
El contexto reserva política/esquema y material aprobado dentro de 32k caracteres;
la ventana secundaria puede ser menor que el máximo heredado. Las evidencias completas
permanecen en PostgreSQL y no se recorta el núcleo aprobado para adaptarlo al límite.

Código 401 sin sesión/rol revocado; 403 capacidad general insuficiente; 404 oculta
recursos/relaciones ajenas; 400 DTO/fechas/CSRF; 409 revisión, fuente no publicada,
duplicado/disponibilidad/obligatorios pendientes/cambio de modelo; 429 límite de
seis turnos por alumno/minuto/proceso o RetryNotBefore. Pending replay 202;
confirmado 200 sin otra llamada. Fallos tipados seguros conservan turno/evidencia.

Unavailable conserva lecturas existentes si sigue la relación. Retirar membresía,
archivar grupo o revocar al docente produce 404 incluso sobre sesión/reporte
previamente conocido; no elimina filas. Teacher/Admin no reciben conversaciones
por la asignación. Compartir una versión de reporte sigue siendo una acción separada.

El cierre devuelve reporte local trazable (Application/deterministic), sin cuota ni
nota oficial. Una entrega abierta NeedsReview no se muestra como verificación.
No hay asignación individual, edición de destinos/versiones, eliminación física ni
endpoints analíticos docentes en 6. No se modifica el contrato heredado de salud.

## Ampliación BYOK-1

La sesión asignada fija la selección del Student al iniciarse, según [PERSONAL_AI](PERSONAL_AI.md). Nunca usa claves del docente responsable. La admisión de cada intento físico revalida propietario, conexión/revisión y relación académica tras esperar el lock; el resultado tardío también se descarta ante revocación. Las reglas de disponibilidad, cierre, evidencia y supervisión conservan sus permisos.
