# ADR-021 — Constructor docente, aprobación y simulación aislada

Fecha: 2026-10-13. Encargo explícito de Iteración 5. Maestro v1.0 intacto.

## Frontera de aprobación y publicación

Lesson identifica clase, docente creador y organización. LessonDraft conserva el
contenido editable actual; LessonDraftRevision registra cada copia completa con
revisión creciente. LessonObjective/Section/Exercise/Rubric son valores validados
dentro de JSON, sin duplicar las entidades de evidencia estudiantil. Las referencias
internas usan IDs únicos; el orden de los arrays es el orden de las actividades.
Los borradores admiten metadatos y actividades incompletas dentro de sus límites;
la aprobación aplica la validación completa compartida de consignas y reglas.
ExerciseProposal y ExerciseValidators de Iteración 4 se reutilizan.

Draft → Simulation → InReview → Approved → Published → Archived. Simulation es
una indicación del trabajo docente; el ciclo de cada prueba es independiente.
Editar o aplicar una propuesta vuelve a Draft y exige otra revisión. No se requiere
completar una simulación para cada cambio; antes de aprobar sí se validan objetivos,
contenidos, actividades, asociaciones, reglas de ejercicios y criterios/pistas.
Abiertos/Code conservan advertencia de revisión humana. Nada ejecuta SQL/código.

**Approved congela el contenido**, resolviendo la ambigüedad Approved/Published
del maestro. Aprobar crea LessonVersion con snapshot académico completo, número,
revisión origen, responsable, fecha y SHA-256 del JSON canónico. Publicar es otra
decisión expresa y crea LessonPublication sin reescribir la versión. Archivar
modifica disponibilidad de publicaciones, nunca su contenido. No hay endpoints de
modificación de versiones. Triggers PostgreSQL rechazan UPDATE/DELETE de versiones
y revisiones; otro trigger protege el origen de simulaciones. Un futuro proceso
de retención/borrado deberá aprobar y diseñar expresamente esa operación.

Crear nuevo borrador copia el contenido vigente y registra otra revisión. La próxima
aprobación crea otro número, conservando versiones previas. LessonPublication se
separa de contenido para que la futura asignación apunte a un LessonVersion.Id
estable. La Iteración 6 queda pendiente: no asignaciones, inscripciones ni ejecución
por estudiantes reales.

## Propuestas y concurrencia

LessonOrchestrator usa IAiProvider para Plan/Content/Exercises/Rubrics con prompt
lesson-design-v1 y un contrato de plan completo portable. JSON Schema si hay capacidad,
JSON o texto en el resto; todos pasan comprobación estructural local recursiva,
límites y validación de referencias/ejercicios. No se aceptan campos extra, Markdown,
propiedades faltantes ni estructura incoherente. Los campos docentes y propuestas
son datos user no confiables; nunca reemplazan la política de servidor.
La conversación puede refinar la última propuesta pendiente de la misma revisión
sin aplicarla: se identifica esa base expresamente en el contexto. Una edición
guardada descarta esa base antigua y vuelve al borrador actual. Se incluyen hasta
cuatro pares recientes, abreviados a 500 caracteres por intervención.

Una respuesta IA crea LessonProposal, no cambia LessonDraft. Aplicar/descartar
son acciones expresas. La propuesta conserva DraftRevision de origen; una edición
posterior la vuelve desactualizada e impide aplicarla, incluso si llega tarde.
El editor conserva campos locales durante polling, errores y consultas de estado.
Los cambios sin guardar se distinguen del resumen guardado y de versiones congeladas.

Todas las escrituras bloquean brevemente la fila propia de Lesson. Revision es
token de concurrencia, y las revisiones de contenido se numeran por separado.
Creación usa clave docente/requestId; las decisiones usan LessonCommand con hash
de acción/revisión/destinatario. Replays devuelven estado actual sin duplicar la
decisión. Unique lesson/version y lesson/draftRevision serializan aprobaciones.
No se garantiza que una respuesta de replay sea el mismo DTO histórico si después
se hicieron otras acciones; sí la misma operación durable.

LessonOperation guarda solicitud/contexto, proveedor/modelo, clave, lease cercado,
errores y resultado. Sin transacción abierta durante IA. Pending vigente → 202;
replay terminado no genera; retry explícito recupera mismo contexto y clave, máximo
tres ejecuciones. GET vencido muestra Interrupted, sin generar ni escribir.
Cancelar/stop cerca resultados tardíos. Revocación docente se comprueba de nuevo
en BD antes de guardar. Cambio de proveedor/modelo da 409, sin fallback.
Timeout configurado y lease timeout+30 s; fallos conservan borrador/respuestas.
Retry-After se respeta; límites de seis acciones IA/minuto/docente/proceso.
No se promete exactly-once de facturación, auditoría integral ni presupuesto diario.

## Un motor pedagógico, almacenamiento separado

LessonSimulation congela el borrador/revisión/proveedor/modelo al iniciarse.
LessonOrchestrator adapta ese contenido al **TutoringOrchestrator existente**:
misma SystemPolicy, ventana corta, ayudas, contrato de respuesta y adaptadores.
TutoringSession/TutoringMessage se usan solo como proyección temporal en memoria,
sin insertarse en sus tablas. La conversación durable y los resultados de ensayo
están en LessonOperation ligados a LessonSimulation. No se crean ExerciseAttempt,
LearningReport, evaluaciones del docente, membresías ni asignaciones.

El servidor selecciona sección/actividad y exige respuesta explícita antes de
avanzar una actividad obligatoria. Las dudas/pistas/simplificación no avanzan.
El comparador compartido produce resultados deterministas para cerrados; abiertos
y código quedan NeedsReview. Cada respuesta y su resultado se confirman antes de
orientación IA, incluso ante fallos; retry no duplica respuestas. La simulación permite
avanzar tras probar una respuesta incorrecta: su finalidad es inspeccionar el plan,
no certificar al profesor. Historial y posición sobreviven stop/resume/recarga.
Pistas y simplificaciones entregadas cuentan como ayuda para clasificar un acierto.

El modelo de conversación no recibe claves de corrección ni soluciones de referencia.
La proyección de simulación excluye esos campos y rúbricas internas. El constructor
y la vista congelada sí los muestran al docente propietario autorizado. No existen
endpoints estudiantiles para clases. El futuro recorrido estudiantil podrá reutilizar
motor/versión/selección de actividad, pero no se promete equivalencia exacta entre
modelos ni entre recorridos adaptativos.

## Privacidad, límites y operación

Cada lectura/escritura exige docente activo, aprobado, Teacher y propietario de
la organización piloto. Teacher ajeno/Student/Admin ordinario/Pending reciben
404 en recursos; crear/listar sin capacidad docente → 403; anónimo → 401.
CSRF/HTTPS en todas las mutaciones. Ninguna relación de grupo abre tutorías privadas.

Límites piloto: 100 clases/docente; 12 objetivos/secciones, 24 actividades; 20k
caracteres JSON por plan; 30 propuestas, 20 versiones, 20 simulaciones por clase;
50 turnos/simulación, 10 respuestas por actividad y 300 decisiones por clase.
Contexto diseño hasta 28k caracteres/cuatro solicitudes anteriores; simulación
hasta cuatro pares confirmados más turno actual, acotados por el motor existente.
Máximo 2048 tokens de salida para diseño y 1024 para tutor. Un plan amplio puede
necesitar diseño manual o ajustes más breves; no se amplía cuota automáticamente.

Migración aditiva TeacherLessons, después de las tres previas intactas. Mantiene
retención ficticia local sin borrado automático. Sin nuevos paquetes, SDK, servicios
externos, fuentes o librerías visuales. UI Light/Dark por tokens UI-0. Groq/.env se
conservan; pruebas/QA solo Mock o transportes controlados, sin cuota externa.
Calidad curricular y nuevos prompts con Groq/Gemini/Ollama requieren revisión manual
autorizada posterior. Contraseña mínima de cinco no cambia y sigue como riesgo real.
