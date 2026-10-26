# Contratos HTTP — tutorías privadas (Iteración 3)

Contrato heredado conservado. La Iteración 4 amplía acciones, LearningView,
reportes/compartición y refuerzo según [LEARNING_HTTP.md](LEARNING_HTTP.md).
Iteración 6 reutiliza estas rutas para Mode=AssignedClass, con vínculo a versión y
validación adicional de relación/disponibilidad. El listado privado excluye clases;
detalle en [ASSIGNMENTS_HTTP.md](ASSIGNMENTS_HTTP.md). Sus requisitos obligatorios
y reporte local de cierre no cambian el contrato de la tutoría privada.

Base: `/api`, HTTPS y cookies del mismo origen. Todas las rutas exigen sesión
activa; todas las escrituras exigen `X-XSRF-TOKEN` obtenido de `/api/auth/csrf`.
La identidad procede de Identity. Nunca se acepta `studentId` como propietario.

| Método / ruta | Entrada | Resultado normal |
|---|---|---|
| POST /tutoring-sessions/ | requestId GUID, topic, goal, level, directReview | 201 SessionView; creación idempotente, sin generación |
| GET /students/me/tutoring-sessions?page=1 | Página positiva, tamaño fijo 50 | 200 SessionItem[] propios |
| GET /tutoring-sessions/{id} | Identificador | 200 SessionView con historial y turnos |
| POST /tutoring-sessions/{id}/messages | requestId, revision, action, text?, retry | 200 SessionView; 202 si la misma operación sigue generando |
| POST /tutoring-sessions/{id}/pause | revision | 200 SessionView pausada |
| POST /tutoring-sessions/{id}/resume | revision | 200 SessionView activa; no genera IA |
| POST /tutoring-sessions/{id}/complete | revision | 200 SessionView cerrada con resumen local |
| POST /tutoring-sessions/{id}/turns/{requestId}/cancel | revision | 200 SessionView; conserva intento y descarta resultados tardíos |

Crear/listar requiere Student activo del piloto (403 sin ese permiso). Consultar,
enviar y transicionar requiere además propiedad: Student ajeno, Teacher y Admin
reciben 404 aunque compartan organización o grupo. Una cuenta con roles múltiples
solo puede consultar sus propias tutorías como Student. Anónimos: 401.

## DTO y límites

`CreateTutoringRequest`: requestId no vacío, topic 2–160, goal 2–500 caracteres,
level Beginner/Intermediate/Advanced, directReview booleano (false predeterminado).
Los textos se recortan y se validan también en el servicio. Tema, objetivo,
configuración pedagógica y proveedor/modelo quedan fijados al crear.

`TutoringTurnRequest`: requestId no vacío, revision no negativa, action
Start/Message/Hint/Simplify. Message requiere text no vacío de hasta 2000 caracteres.
Otras acciones no admiten texto arbitrario: el servidor prepara el intento.
retry=false inicialmente. Recuperar usa la misma clave, acción y texto con retry=true.
No debe generarse otra clave para recuperar una respuesta incierta.

SessionView contiene id/topic/goal/level/directReview/state/revision, fechas UTC,
providerId/modelId/policyVersion/activity/currentFocus, messages/turns,
pendingRequestId y summary. Los mensajes contienen secuencia, rol Student/Tutor,
texto, requestId y metadatos disponibles de proveedor/modelo/política/tokens.
No contiene claves, prompts de sistema ni datos de otros estudiantes.

TurnView indica estado Pending/Succeeded/Failed/Cancelled, attempts,
isGenerating, canRetry, errorCode/error seguros, retryNotBefore y durationMs.
Un lease vencido se presenta como Interrupted recuperable sin escribir en GET.
La ausencia de tokens reportados se conserva como null; Mock no inventa consumo.

Límites: 100 sesiones por estudiante, 100 mensajes por sesión contando ambos roles,
respuesta hasta 6000 caracteres, foco hasta 200; 6 envíos de generación/minuto por
cuenta/proceso. Se reservan los dos espacios del turno antes de generar.
La ventana IA usa hasta 11 mensajes en total entre confirmados recientes e intento actual,
con presupuesto de 12.000 caracteres de conversación. El historial completo
persiste aunque salga de esa ventana. Detalles: ADR-016/017.

## Estados y recuperación

Created permite Start; crear/leer no genera. Start activa la sesión. Active permite
Message/Hint/Simplify. Paused requiere resume antes de continuar. Completed es
terminal y sigue legible. Complete funciona incluso tras fallo IA, sin nueva cuota.
Pausa/cierre requieren resolver o cancelar una generación vigente.

Una operación pendiente bloquea otra clave. Repetir una solicitud completada devuelve
el resultado guardado sin IA; repetir una pendiente vigente devuelve 202. Una clave
reutilizada con datos diferentes, revisión vieja, estado incompatible o cambio de
proveedor/modelo devuelve 409. El cliente debe refrescar antes de decidir.

Los errores del proveedor conservan el intento; pueden devolver 200 con TurnView
Failed y error seguro. Timeout, cuota/red y respuesta inválida no exponen cuerpos
externos. Se permiten hasta 3 ejecuciones explícitas del mismo turno respetando
RetryNotBefore; no se reintenta al consultar ni al reanudar. Fallo inesperado puede
devolver 503 seguro. La interfaz refresca para recuperar el estado durable.

Cancelar un requestId propio admite revisión anterior para resolver la carrera con
HTTP abortado, pero nunca cancela un turno diferente. Cancelar no garantiza detener
el procesamiento o cobro externo. Tras caída entre respuesta remota y commit,
recuperar podría consumir cuota nuevamente; no se promete facturación exactly-once.

400: CSRF/datos; 401: sin sesión o revocada; 403: función sin permiso; 404: recurso
ajeno/inexistente; 409: conflicto; 429: límite API. No hay acceso administrativo
ordinario ni compartición del historial, borrado ni ejecución de código. La
compartición de versiones de reportes de 4 se regula separadamente. OpenAPI solo Development.
## Ampliación IA-2

Las clases precargadas reutilizan estas sesiones y sus garantías; añaden la ruta
local `/api/tutoring-sessions/{id}/cards`, sin proveedor, y evaluación FinalReport
opcional posterior al cierre. [Contrato y límites](PRELOADED_CLASSES.md).

## Ampliación BYOK-1

LEARN-1 añade ejes experience/modality y extensión opcionales, PrepareSession,
tarjetas privadas y plan diagnóstico agrupado, sin convertir sesiones anteriores.
[Contrato preparado](PREPARED_LEARNING.md).

[Conexiones personales](PERSONAL_AI.md) fijan conexión/revisión/modelo al crear la sesión. Cambiar selección afecta creaciones posteriores; revocar la conexión impide nueva inferencia y descarta resultados tardíos. Replays y evidencias persistidas se conservan. Las sesiones anteriores mantienen proveedor institucional. Errores seguros adicionales: CreditsRequired y PaymentAuthorizationRequired; nunca cambian de proveedor automáticamente.
