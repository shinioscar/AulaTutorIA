# Clases precargadas — IA-2

## Guía docente

En Crear con apoyo de IA selecciona **Clase precargada**. Configura tema/nivel y
1–4 ejercicios, con 1–3 pistas. Puedes usar generación integrada o copiar un prompt
para una herramienta externa. La solicitud integrada intenta preparar todo en una
operación, sin partes ni reparaciones ocultas; si no cabe, reduce el alcance.
Un error no aprueba ni publica contenido. Una propuesta integrada requiere aplicar;
una importación requiere validar, revisar y guardar como en IA-1.

En el mismo constructor, Contenido → Contenido precargado y modalidad permite
editar introducción, pistas, alternativas y feedback de cada actividad. Si añades
o eliminas actividades, sincroniza sus ayudas con el botón explícito. Los borradores
incompletos se pueden guardar; simular/aprobar exigen completitud. Comprueba que
las primeras pistas y textos públicos no revelen claves o soluciones. La estructura
válida no comprueba corrección curricular ni todos los significados del texto.

Simula, revisa, aprueba, publica y asigna la versión exacta mediante los controles
existentes. La modalidad queda congelada con sus ayudas en esa versión. Un nuevo
borrador no cambia versiones ni asignaciones anteriores.

## Guía estudiante

Abre la clase asignada e inicia el recorrido. Cada tarjeta contiene una actividad,
su objetivo, contenido y campo de respuesta. Dame una pista y Otra explicación
entregan una ayuda local por vez y la registran. Los consejos de errores/refuerzo
se entregan con el feedback predefinido; no son ejercicios obligatorios adicionales.
Una actividad cerrada obligatoria requiere coincidencia con su regla para avanzar;
abiertos/código quedan NeedsReview. Nunca se ejecuta SQL ni código.

Puedes pausar, recargar y continuar: la evidencia está en PostgreSQL, no en storage.
Un envío sin confirmación permite recuperar la misma solicitud sin duplicar ayuda
o intento. El servidor controla disponibilidad, avance y cierre. Tras completar,
siempre tienes el reporte local; puedes solicitar aparte la evaluación IA preliminar.
Sin cuota o con timeout, el cierre y el reporte local siguen disponibles. Reintentar
una evaluación fallida es explícito y puede consumir otra solicitud.

## Contrato externo V2

Fuente: LessonImportV2/PreloadedContent en Application/Lessons/preloaded-content.cs.
El esquema se genera desde esos tipos y se reutiliza en prompt, generación y
validación. [Ejemplo ficticio completo](examples/lesson-import-v2.json).

- Raíz: format="aulatutor-lesson-v2", lesson y preloaded, sin campos extra.
- lesson conserva LessonDesignPayload de V1: objetivos, secciones y ejercicios
  vinculados por IDs locales, con reglas y soluciones privadas.
- preloaded: introduction y exercises[]. Cada elemento referencia exerciseId y
  contiene hints[], alternatives[], correctFeedback, incorrectFeedback,
  reviewFeedback, commonErrors[] y reinforcementSuggestions[].
- Una entrada por cada ejercicio, referencias exactas y únicas. Hasta tres pistas
  y tres alternativas, al menos una de cada tipo al aprobar/importar. Hasta tres
  consejos de errores/refuerzo. Cada texto nuevo: máximo 600 caracteres.
- Hasta 24 ejercicios importados/editados, ocho por sección, 12 secciones/objetivos,
  20000 caracteres de contenido completo. Entrada externa 32000, profundidad 12,
  archivo .json 128 KiB y límite HTTP global 1 MiB conservados.

V1 sigue aceptado y conversacional, sin ayudas inventadas ni conversión automática.
El constructor explica cómo preparar explícitamente el contenido faltante. Generación
integrada/prompt externo se acotan a cuatro ejercicios para intentar una solicitud
breve dentro de 4096 tokens, sin garantizar que todo modelo/cuenta la complete.
Truncados/incompletos se rechazan. No se implementa generación por partes.

## HTTP y persistencia

Se conservan las rutas de IA-1 y del constructor. external-prompt admite preloaded
opcional (false por defecto); import/preview e import reconocen V1 y V2 por format.
Design admite action=PreloadedPlan; text contiene opciones LessonExternalOptions
durables, preparadas por el formulario, no una URL/modelo/API key del navegador.
Solo Teacher propietario activo/aprobado, sesión HTTPS y CSRF como antes.

POST /api/tutoring-sessions/{id}/cards recibe el TutoringTurnRequest existente.
Acciones locales: Start, Hint, Simplify, SubmitAttempt y NextActivity; texto/ExerciseId
solo en SubmitAttempt. RequireRead/RequireWork revalidan propietario Student,
organización, grupo, docente, disponibilidad y publicación. 401/403/404/400/409
siguen los contratos existentes. CSRF obligatorio. La ruta no acepta FinalReport
ni sesiones conversacionales y no llama a IAiProvider. Máximo 500 turnos locales.

Pausa/resume/complete usan las rutas originales. FinalReport usa /messages, con
sus límites IA y solo después de Completed. Primera preparación guarda contexto
completo, EvidenceRevision y evidenceHash; reintento reutiliza ese contexto. No
hay transacción abierta durante IA. Replays no generan; otra clave sobre reporte
vigente no genera. Una llamada física de retry sigue contabilizada por admisión PG.

Sin tablas/migraciones nuevas: modalidad en snapshots JSON inmutables existentes;
avance/contadores en sesión/ejercicio; ayudas en turnos con ejercicio, número/tipo,
fecha y mensaje efectivamente entregado. No se anticipan ayudas futuras ni claves
en proyecciones Student. La simulación docente conserva su almacenamiento aislado.

## Evaluación y supervisión

El contexto final incluye versión/hash, objetivos, todos los intentos íntegros,
resultados deterministas, texto/fecha de ayudas entregadas y criterios. El hash
del conjunto cubre evidencias y ayudas. No incluye nombre/correo, chat,
acceptedAnswers ni referenceSolution. Si no cabe, se rechaza antes de llamar;
no se recortan evidencias ni se envían lotes ocultos. ReportContent valida IDs
contra lo efectivamente suministrado. PromptVersion=preloaded-evaluation-v1.

El reporte determinista assigned-summary-v1 permanece separado. Observaciones
IA referenciadas se incorporan a la supervisión autorizada; una revisión humana
es independiente y no sustituye resultados, ayudas o intentos. Sin notas oficiales
ni equivalencia pedagógica garantizada con el tutor conversacional.
