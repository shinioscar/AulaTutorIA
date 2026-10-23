# Supervisión docente — contratos de Iteración 7

IA-2 conserva esta autorización e incorpora observaciones referenciadas del reporte
preliminar preloaded-evaluation-v1. No transforma NeedsReview ni revisiones humanas;
véase [clases precargadas](PRELOADED_CLASSES.md).

Cookies HTTPS de mismo origen, sesión Identity y autorización en backend. Todos los
recursos se consultan en PostgreSQL; GET no escribe ni llama a IA. ADR-024 define la
relación académica y la conservación. No hay chat docente-estudiante general.

| Método / ruta | Resultado |
|---|---|
| GET /api/teacher/supervision/?groupId=GUID | Grupos autorizados con estudiantes elegibles, publicaciones propias y asignaciones/métricas. Filtro ajeno: 404. |
| GET /api/teacher/supervision/assignments/{id} | Versión/hash/grupo/fechas, participación, roster elegible, objetivos, ejercicios, casos y foco descriptivo de refuerzo. |
| GET /api/teacher/supervision/assignments/{id}/students/{studentId} | Solo esa clase: objetivos, consignas/respuestas/intentos/ayudas, resultados, casos, historial docente y observaciones IA referenciadas. |
| POST /api/teacher/supervision/assignments/{id}/students/{studentId}/reviews | Añade entrada de revisión/feedback/seguimiento. CSRF; 200 con historial del destino. |
| GET /api/students/me/classes/{id}/feedback | Solo el propietario, con acceso de ADR-023; decisiones e historial de su clase. Vacío si aún no tiene sesión/comentarios. |

Anónimo 401. Listar supervisión sin capacidad Teacher aprobada: 403. Recursos
ajenos, Admin sin relación o relación perdida: 404 uniforme. DTO/CSRF: 400.
Conflicto de revisión/evidencias/clave/límite: 409. No hay mutaciones con 204 ficticio.
OpenAPI de desarrollo conserva esquemas de respuestas, cookie y cabecera CSRF.

## Revisión y feedback

ReviewRequest: requestId GUID no vacío; revision del destino (cero si nuevo);
evidenceRevision actual de la sesión; exerciseId opcional (null = comentario de
clase); state Pending/InReview/Reviewed/FollowUpNeeded; attention
Pending/InProgress/Closed; outcome NotAssessed/Supported/NeedsReinforcement/
InsufficientEvidence; flagged boolean; comment obligatorio, observation y
recommendation opcionales, cada texto hasta 1000 caracteres; evidenceIds hasta
diez GUID distintos de intentos examinados de la misma actividad/sesión.

Un resultado distinto de NotAssessed requiere observation y referencias. Para
Reviewed/Closed sobre actividad con intentos se referencia el último. Closed
requiere Reviewed. La marca explícita y FollowUpNeeded requieren una actividad
concreta presentada, evitando casos generales sin evidencia identificable.
Los textos de todas las entradas son visibles al estudiante: no notas internas.
No se edita una entrada: actualizar implica añadir otra. Máximo cien por destino.

Una transacción breve bloquea asignación y sesión, revalida relación tras esperar, verifica revisión y
evidencia; guarda entrada, referencias y revisión del agregado atómicamente. Una
clave idéntica recupera el historial actual sin otra entrada aun si cambiaron las
evidencias después. La misma clave con otros datos es conflicto. Un fallo antes
de commit permite repetir la solicitud original. No cambia EvidenceRevision del
estudiante, el comparador, los intentos disponibles ni el avance académico.

## Denominadores y origen

Elegibles: estudiantes activos con rol Student y membresía vigente del grupo.
Abrieron: existe sesión AssignedClass de esta asignación/versión. Comenzaron:
la sesión salió de Created. Finalizaron: estado Completed, separado de cumplir
entregas. Todos los conteos de estudiantes son únicos dentro de su asignación.

Respondidos: pares alumno/ejercicio con al menos un intento. Entregas para avanzar:
coincidencia en cerrado o NeedsReview en abierto, conforme ADR-023. Correctos
por objetivo: ejercicios distintos que coinciden con regla, prioridad sin ayuda
si hay ambas categorías. Incorrectos/NeedsReview/no evaluables y total de intentos
se cuentan como intentos. Oportunidades obligatorias = ejercicios obligatorios
del objetivo × elegibles. Opcionales se identifican aparte; no porcentajes de dominio.
Refuerzos sin ApprovedObjectiveId se muestran individualmente y no inflan objetivos.

Los casos indican razones y referencias: RequiresHumanReview, NotEvaluable,
RepeatedErrors (tres fallos sin acierto), AttemptsExhausted (diez sin coincidencia),
TeacherFlagged. La última decisión que cubre el último intento puede cerrar el
seguimiento; otra respuesta reabre la cobertura. Acierto posterior resuelve la señal
automática de errores. Las decisiones históricas siguen consultables. No hay
inferencias por tiempo conectado/inactividad ni aprobación/reset automáticos.

Las evidencias y casos incluyen las opciones públicas de la consigna, nunca claves
de corrección. SupervisedAttempt separa AutomaticFeedback de AiFeedback usando el marcador
persistido del motor; ambos conservan origen, fecha y versión. Las observaciones
de reportes IA solo se incluyen con referencias existentes en esta clase, con
reporte/versión/proveedor/modelo/prompt y marca de desactualización. No se exponen
resúmenes libres sin referencias ni reportes personales. Decisiones humanas tienen
autor/fecha/estado y referencias propias. El resumen grupal IA queda diferido.

## Pérdida de relación y límites

Solo responsable de la asignación; co-docente y Admin no son excepciones. Grupo
archivado, docente revocado/retirado o estudiante retirado/inactivo: ocultar lecturas
y escrituras futuras conservando filas. Withdrawn termina supervisión docente.
Inactive/fechas/publicación archivada permiten revisión histórica con relación
vigente. Feedback propio respeta la política de lectura Student de ADR-023.

Consultas por asignación sobre el piloto existente (hasta 300 asignaciones propias,
100 sesiones/alumno y límites de actividades/intentos heredados), sin truncar cifras
silenciosamente. Listado y detalle cargan la cohorte actual completa. Paginación y
optimización de agregaciones para grupos grandes se medirán antes del piloto real;
no se prometen rendimiento ni escalabilidad institucionales. No se persisten
duplicados de respuestas, estadísticas derivables o resúmenes grupales innecesarios.
