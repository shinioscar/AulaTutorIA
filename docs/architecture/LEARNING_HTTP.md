# Contratos de aprendizaje — Iteración 4

Todas las rutas exigen sesión Identity activa. POST/DELETE requieren HTTPS y CSRF.
Propiedad Student se verifica en servicios; recursos ajenos 404, anónimos 401,
sin capacidad general 403, datos 400, conflicto/revisión 409 y límite 429.

## Acciones privadas

POST /api/tutoring-sessions/{id}/messages reutiliza el contrato de ADR-017:
`{requestId,revision,action,text?,retry?,exerciseId?}`. Nuevas acciones:

| Acción | Datos / efecto |
|---|---|
| DiagnosticQuestion | Una consigna nueva, hasta límite; requiere respuesta a la anterior o análisis para terminar. Puede iniciar sesión Created. |
| AnalyzeDiagnosis | Cierra diagnóstico explícito y genera versión preliminar o fundamentada. Puede iniciar Created sin respuestas. |
| GeneratePlan | Diagnóstico vigente; text opcional solo para ajuste (500). Nueva versión de propuesta, máximo 3. |
| ActivatePlan | Acepta propuesta vigente y comienza primera actividad en la misma conversación. |
| SubmitAttempt | text de 1–2000 y exerciseId propio/presentado; guarda intento/regla/ayudas antes de orientación IA. |
| NextActivity | Requiere actividad respondida según ADR-018; servidor controla posición y terminación. |
| GenerateExercise | Ejercicio libre identificado; no permitido durante plan activo. |
| FinalReport | Reporte vigente/versionado, explícito; permite Completed sin reabrir chat. |

Devuelve TutoringSessionView 200 o 202 pendiente. Learning contiene evidencias,
último diagnóstico/plan, revisiones de reportes, recomendaciones y autorizaciones.
Las proyecciones excluyen soluciones AcceptedAnswersJson y ContentJson privado.
TurnView añade exerciseId y retryText para recuperar la misma petición tras recarga.
Retry conserva clave/acción/texto/ejercicio, respeta RetryNotBefore y máximo 3.
Cancel usa la ruta de turnos existente. GET nunca genera ni reintenta.

CreateTutoringRequest admite SourceRecommendationId opcional; se valida propiedad,
estado Completed y correspondencia con tema/meta de origen en backend. Preferir
la ruta de recomendaciones para derivar datos desde servidor.

## Compartición y continuidad

| Método / ruta | Contrato |
|---|---|
| GET /api/tutoring-sessions/{id}/share-recipients | ShareRecipient[] (ID/nombre, sin email); relaciones vigentes, solo propietario. |
| POST /api/tutoring-sessions/{id}/shares | {requestId,reportId,recipientId,consent:true}; ShareView; clave ligada a destinatario/reporte. |
| DELETE /api/tutoring-sessions/{id}/shares/{shareId} | 204; revocación idempotente y solo propietario. |
| GET /api/teacher/reports/ | Hasta 100 ReportView autorizados con relación vigente. |
| GET /api/teacher/reports/{reportId} | ReportView de versión compartida, sin conversación/respuestas/soluciones. |
| POST /api/tutoring-sessions/{id}/recommendations/{recommendationId}/start | {requestId}; TutoringSessionView nueva privada, sin IA al crear. |

El flujo de cierre sigue siendo local. Generar reporte después es acción explícita;
FinalReport conserva un turno de generación durable pero no agrega mensajes al
historial: puede ejecutarse incluso al alcanzar los cien mensajes, sin reabrir el chat.
si falla se conservan evidencias/resumen y se ofrece recuperación del mismo turno.
El motor no ejecuta SQL/código de alumnos ni produce calificaciones oficiales.

## IA-2: recorrido precargado y evaluación agrupada

Las sesiones AssignedClass con policyVersion=preloaded-class-v1 registran las
respuestas y ayudas mediante /cards, usando los mismos intentos, comparadores,
revisiones y permisos. Las actividades ordinarias no llaman a IAiProvider.
FinalReport se solicita explícitamente mediante /messages solo tras Completed;
genera LearningReport con promptVersion=preloaded-evaluation-v1 y conserva aparte
assigned-summary-v1. Incluye todos los intentos dentro del presupuesto, sin
recortarlos ni enviar soluciones privadas, nombre o correo. Las referencias se
validan contra las evidencias enviadas. Un fallo conserva el cierre y reporte local;
reintento y cancelación mantienen los contratos existentes.

Contrato, límites y compatibilidad: [Clases precargadas](PRELOADED_CLASSES.md).
