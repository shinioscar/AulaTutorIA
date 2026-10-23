# ADR-028 — Clases precargadas y evaluación agrupada

Estado: aceptado, implementado y verificado en IA-2. Amplía ADR-021/023/027; no cambia el
maestro ni el alcance privado de tutorías autónomas.

La modalidad se fija en LessonContent.Preloaded dentro de los snapshots JSON
existentes de borrador/revisión/simulación/versión. Ausencia significa conversacional.
JsonIgnore WhenWritingNull conserva la serialización de los contenidos anteriores.
No se reescriben versiones, asignaciones ni sesiones. PolicyVersion de una nueva
sesión asignada precargada se fija en preloaded-class-v1; la política completa se
obtiene de su LessonVersion inmutable. No hay migración automática de modalidad.

LessonImportV2 contiene format=aulatutor-lesson-v2, lesson (LessonDesignPayload de
V1) y preloaded. PreloadedContent contiene introducción y ayudas por exerciseId:
hints, alternatives, correctFeedback, incorrectFeedback, reviewFeedback,
commonErrors y reinforcementSuggestions. Los consejos de refuerzo son textos
opcionales, no sustituyen ni crean obligaciones. HintRules sigue siendo una regla
docente distinta. V1 se acepta sin convertirlo en precargado. El esquema generado
del tipo V2 es único para prompt externo, generación integrada e importación.

Hasta tres pistas y tres explicaciones alternativas de 600 caracteres cada una;
feedback/introducción 600, hasta tres consejos de cada clase. Los límites anteriores
de 24 ejercicios, 12 objetivos/secciones y 20000 caracteres de contenido continúan.
Borrador admite ayudas incompletas; importar, simular y aprobar exigen completitud.
Las ayudas deben corresponder exactamente a los ejercicios locales y sus IDs únicos.
Se rechaza una primera pista igual a una respuesta aceptada o que contenga
literalmente una clave de al menos tres caracteres. Esto no detecta todas
las revelaciones semánticas: el docente debe verificar que ayudas/feedback públicos
no revelen claves ni soluciones antes de aprobar.

Generación integrada PreloadedPlan: una GenerateAsync con máximo 4096 tokens,
1–4 ejercicios y 1–3 pistas. Input/schema/presupuesto se verifican antes del envío;
truncado o contenido incompleto no produce propuesta aplicable. No se encadenan
partes ni llamadas de reparación. El proveedor y el transporte siguen usando sus
cuotas persistentes, timeout, reintentos explícitos y concurrencia; una solicitud
lógica no garantiza una única llamada física ni facturación exactamente una vez.
Para contenido más grande se explica reducir el alcance, sin generación automática
por partes. Prompt externo usa los mismos límites conservadores y contrato.

Tarjetas son una proyección alternativa de la misma TutoringSession AssignedClass,
LearningExercise, ExerciseAttempt y TutoringTurn. No se crea un motor académico
paralelo. Start/Hint/Simplify/SubmitAttempt/NextActivity locales usan el lock,
revisión y GUID/hash del motor. El turno confirmado evita duplicados aun con
respuesta HTTP perdida. Ayuda registra ejercicio, número/tipo, fecha y texto
efectivamente entregado; no se proyectan ayudas futuras ni claves. Cada entrega
confirma intento y resultado del comparador en la misma transacción breve.
No hay IA ni leases remotos en acciones locales. Pausa/cierre/reanudación y
requisitos de entrega siguen el motor existente. Abiertos/Code siguen NeedsReview.

POST /api/tutoring-sessions/{id}/cards acepta exclusivamente acciones locales de
una clase precargada propia y vigente. No permite FinalReport ni puede utilizarse
para eludir límites de IA con sesiones conversacionales. Conserva autorización,
CSRF y límite HTTP 1 MiB. Máximo 500 turnos locales/sesión; no consume el límite
por minuto destinado a generaciones. GET no actúa. No hay conversación libre ni
modo híbrido; el punto de selección local/IA permite una ampliación posterior.

FinalReport precargado solo tras Completed y por acción explícita en /messages,
con rate limit IA normal. Usa LearningReport existente con promptVersion
preloaded-evaluation-v1, separado de assigned-summary-v1 determinista. Incluye todos
los intentos, resultados, ayudas y criterios pertinentes, versión/hash académico,
EvidenceRevision y hash del conjunto enviado. No envía nombre/correo, chat privado,
acceptedAnswers ni referenceSolution. No recorta evidencia: si no cabe dentro del
presupuesto, rechaza sin llamar y conserva el resultado determinista.

La generación cualitativa reutiliza LearningOrchestrator, su contrato ReportContent,
validación de IDs contra evidencia efectivamente enviada, lease/retry/cooldown y
finisher con revalidación de cuenta/relación/disponibilidad. Misma revisión de
evidencias ya evaluada no admite regeneración mediante otra clave. Replays con la
misma clave recuperan estado. Fallos conservan cierre/evidencia/reporte local;
recuperación explícita puede realizar otra llamada física contabilizada.
Las observaciones IA referenciadas aparecen en la supervisión existente; no
cambian NeedsReview, entregas, revisiones humanas ni producen notas oficiales.

No se requieren tablas ni paquetes nuevos: todos los datos y el origen inmutable
ya tienen persistencia PostgreSQL. QA debe comprobar consistencia EF/migraciones,
cero llamadas ordinarias y contadores separados de generación/evaluación Mock.
