# ADR-023 — Asignaciones y sesiones individuales de clases

Fecha: 2026-10-14. Encargo explícito de Iteración 6. Maestro v1.0 intacto.

## Versión, destinatario y permisos

LessonAssignment referencia una LessonPublication y una LessonVersion específicas,
con LessonId, organización, grupo y docente propietario. Solo Teacher activo/aprobado
puede asignar su clase a un grupo vigente donde tenga membresía Teacher. Admin no
obtiene esa facultad por su rol. Ni el borrador, ni Approved sin publicación, ni una
publicación archivada son elegibles. Publicar otra versión no cambia asignaciones.
Se pueden seleccionar publicaciones anteriores no archivadas, identificadas por número.

FKs compuestas impiden cruzar organización/grupo/docente/clase/versión. La creación
se serializa por docente y bloquea brevemente Lesson como publicar/archivar. Clave
GUID/hash conserva idempotencia; índice parcial grupo/versión impide duplicados
Active, incluso programados o vencidos. Desactivar libera esa equivalencia; reactivar
vuelve a comprobarla. No se reactiva automáticamente según el reloj.

Target actual: grupo. La elegibilidad está centralizada en AssignedClassAccess,
separada del motor y del DTO académico. Una futura asignación individual extenderá
esa política con tipo de destinatario y StudentId, conservando GroupId como relación
académica verificable y FKs/índices adecuados mediante migración. No se implementan
ahora asignaciones individuales, invitaciones ni un permiso docente nuevo: se prioriza
el flujo grupal obligatorio y se evita un segundo camino de autorización incompleto.

## Disponibilidad y conservación

Active permite trabajar durante [AvailableFrom, AvailableUntil), extremos opcionales,
normalizados a UTC; Angular edita en horario local. Inactive admite reactivación;
Withdrawn es terminal. Cambiar fechas/estado exige revisión y GUID/hash; AssignmentChange
conserva acción, actor, fecha, estado y disponibilidad resultante. Fuente académica,
destinatario y creación son inmutables; cambiar grupo/versión requiere otra asignación.
Hasta 300 asignaciones por docente y 100 cambios por asignación en este piloto.

| Cambio | Nuevos inicios | Sesión existente |
|---|---|---|
| Alta posterior de alumno en grupo | Disponible si cumple relación y fechas | Sesión individual única |
| Desactivar/retirar/fuera de plazo | Bloqueados | Solo lectura; evidencia conservada, sin mensajes/reanudar/cerrar |
| Archivar publicación | Bloqueados | Solo lectura del origen ya asignado |
| Retirar membresía Student o Teacher | Bloqueados | 404 en cada acceso a sesión/evidencia/reporte relacionado |
| Desactivar/revocar rol/aprobación Teacher | Bloqueados | 404; la relación deja de ser válida |
| Archivar grupo | Bloqueados | 404 mientras no exista relación vigente; filas conservadas |
| Publicar versión posterior | Sin cambio en asignación anterior | Misma versión/hash/consignas |

Un grupo con FKs restrictivas no se elimina físicamente; archivar no borra evidencia.
Si se restablece una relación válida y la disponibilidad, se recupera la misma sesión.
Cancelar un turno propio o pausar sigue siendo posible en modo de solo lectura para
resolver operaciones pendientes, siempre con membresía vigente. Una consulta no
reanuda ni genera. El docente conserva su historial de asignaciones propias mientras
mantenga la membresía Teacher, incluso si el grupo se archiva; no puede modificarlo
con grupo archivado. La retirada de relación no borra copias ya obtenidas.

## Un solo motor, progreso independiente

TutoringSession añade Mode: Private, Personalized o AssignedClass. Aceptar plan personal
marca Personalized; la migración identifica también planes aceptados existentes.
Private y Personalized permanecen privados. La simulación docente sigue en sus tablas
aisladas, nunca como sesión Student. AssignedClass conserva AssignmentId/VersionId;
índice único asignación/alumno y lock corto por alumno evitan duplicados entre claves,
doble clic y pestañas. El listado privado excluye clases; estas aparecen en Mis clases.

Iniciar clase crea/recupera la sesión sin cuota. Dentro del chat, «Comenzar conversación»
es la acción explícita que genera el primer turno y presenta la primera actividad.
Se reutilizan TutoringService, LearningWorkflow, LearningOrchestrator,
TutoringOrchestrator, IAiProvider y sus adaptadores, sin otro cliente HTTP ni motor.

Al crear se copian TODOS los ejercicios aprobados a LearningExercise con IDs propios,
LessonVersionId, ApprovedExerciseId, ApprovedObjectiveId, Required y orden. Las reglas
privadas y la consigna proceden del snapshot; criterios privados/solución permanecen
en la versión y nunca se envían al modelo conversacional ni a Angular. La rúbrica
pública Activity.Rubric se conserva en cada ejercicio. La aplicación mantiene la
actividad actual; preguntas y ayudas no avanzan. El contenido esencial completo y
los objetivos están disponibles en la vista segura, además de la sección actual.

NextActivity exige una entrega aceptable en el obligatorio actual. Cerrados requieren
CorrectWithoutHelp/CorrectWithHelp del comparador; abiertos/Code permiten continuar
con NeedsReview, sin verificación ni ejecución. Opcionales se pueden omitir. Cerrar
exige iniciar la sesión y entregar todos los obligatorios presentados; un cerrado
incorrecto o obligatorio futuro impide completar. Entregado nunca equivale a dominio.
No se requiere completar las actividades opcionales para cerrar.

GenerateExercise produce IsComplementary sin identidad de ejercicio aprobado ni
Required. El refuerzo se puede responder y consultar aparte; no modifica posición,
objetivos, claves o requisitos comunes. Hint/Simplify del chat se atribuyen a la
actividad aprobada actual; su contabilidad no se transfiere a otros alumnos. Hasta
diez refuerzos y diez intentos por ejercicio, límites heredados. Agotar intentos de
un cerrado sin coincidencia bloquea su entrega: requiere intervención académica y
una decisión posterior; no se inventa una aprobación ni un reset de evidencia.

## IA, evidencia y recuperación

assigned-class-v1 añade política explícita de núcleo fijo y ayudas adaptativas.
El contexto diferencia approvedAcademic de untrustedAttempts, conserva versión/hash,
objetivos/metodología/instrucciones/sección/consigna/reglas de pistas y estado propio.
No incluye acceptedAnswers ni ReferenceSolution del ejercicio aprobado. El contexto
durable del turno se reutiliza al recuperar. Los datos académicos tampoco pueden
sustituir reglas de seguridad. No se promete resistencia absoluta del texto LLM a
inyecciones: permisos, avance, contenido y cierre siempre se validan en backend.

El límite de entrada 32k incluye política, esquema y material académico. El tutor
reserva primero el núcleo aprobado y ajusta la ventana reciente. Aprendizaje reserva
política/esquema y, si hace falta, reduce mensajes e intentos antiguos del contexto
secundario; conserva el intento más reciente y los registros completos en PostgreSQL.
La suficiencia preliminar se calcula sobre evidencias efectivamente suministradas.
Una entrada que aún exceda el presupuesto se rechaza de forma segura antes de llamar
al proveedor. Nunca se recorta el contenido aprobado para aparentar cumplimiento.

ExerciseAttempt se confirma antes de esperar orientación IA. La sesión conserva
revisión, turno único, claves, lease/token cercado, retry explícito máximo tres,
RetryNotBefore y descarte tardío. No hay transacción PostgreSQL durante IA. Antes de
persistir se revalidan cuenta/rol, membresías, docente y disponibilidad: una retirada
cancela duramente el turno y descarta su respuesta, conservando el intento primario.
Cambiar proveedor/modelo sigue produciendo 409 sin fallback. GET/recarga no reintentan.

El cierre genera un LearningReport local con estadísticas PostgreSQL, versión/hash
y distinción de entregas y pendientes humanos. Provider=Application,
Model=deterministic, PromptVersion=assigned-summary-v1, sin cuota ni interpretación
IA ficticia. El resumen local también permanece. La acción IA de reporte existente
se conserva, pero una revisión vigente no se regenera por otra clave sin cambios.
Se mantiene el máximo de diez reportes: en clase asignada se reservan nueve
revisiones IA y una revisión local de cierre; finalizar no puede consumir un undécimo.

No se concede acceso implícito al chat/evidencia al docente responsable en 6. Un
reporte puede compartirse expresamente mediante ADR-019, verificando relación y
acceso asignado en cada consulta. La supervisión académica está reservada a 7.
El vínculo de evidencia responsable existe en la asignación; no abre tutorías privadas.

## Persistencia y límites

Migración aditiva AssignedClasses después de las cuatro anteriores intactas. Añade
LessonAssignments/AssignmentChanges y los vínculos de sesión/ejercicio. Triggers
protegen fuente de asignación/sesión/ejercicio aprobado e historial de cambios;
las versiones mantienen sus protecciones. Progreso/ayudas/entregas pueden evolucionar,
sin reescribir la consigna. Borrado institucional futuro requiere proceso autorizado.

Solo datos ficticios, sin políticas reales de retención/consentimiento nuevas. Se
conservan contraseña mínima cinco y riesgo documentado, límites por proceso y
facturación no exactly-once. Sin paquetes/SDK nuevos, SQL ejecutable, calificaciones,
panel grupal, streaming ni integración institucional. Mock usa guiones rotulados de
la consigna aprobada; no personalización semántica. Calidad curricular y proveedores
reales requieren pruebas manuales autorizadas posteriores, fuera del QA de 6.
