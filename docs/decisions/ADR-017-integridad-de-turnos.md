# ADR-017 — Idempotencia, revisiones y recuperación de tutoría

Fecha: 2026-10-13. Estado: implementada en Iteración 3.

## Ciclo de sesión

Created: guardada sin llamada de IA. Start explícito reclama primer turno y activa
la sesión. Active permite turnos; Paused conserva todo y requiere Resume explícito.
Completed es terminal. Complete puede cerrar incluso un turno fallido/interrumpido,
sin llamar IA; un turno vigente exige esperar o cancelar antes. Pausa/reanudación
y cierre usan Revision del servidor; repetir una transición ya alcanzada es inocuo.

La fase pedagógica sugerida se mantiene separada del ciclo de sesión. Se reduce
la máquina de estados conceptual del maestro a ciclo operativo y actividad
orientativa; no se inventan estados de aprendizaje validado ni escalamiento real.

## Creación y envío

Cliente genera GUID de solicitud, nunca credenciales. Creación idempotente con
índice propietario/CreationKey y advisory lock corto por estudiante; clave reutilizada
con otros datos: 409. Topic/Goal son inmutables. No se guarda conversación en storage
del navegador. Tras recarga, listado/ruta/GET recuperan el estado durable.

Un turno tiene clave compuesta SessionId/RequestId y hash del contenido/acción para
ligar reintentos al mismo intento. Una transacción corta bloquea SOLO la fila propia
con SELECT FOR UPDATE, comprueba revisión/estado y persiste mensaje Student antes
de IA. Revision también es token de concurrencia EF. No hay transacción/lock/lector
abierto durante generación. Respuesta y cambio de estado se confirman en otra
transacción corta. Lectura GET usa snapshot RepeatableRead breve para consistencia.
[EF Core documenta los tokens de concurrencia y transacciones breves](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

Una sesión solo admite un turno pendiente. Reenvío idéntico ya completado recupera
el resultado sin IA; pendiente vigente devuelve 202 y estado; otra clave simultánea
o revisión vieja: 409. Índices únicos impiden mensaje duplicado/orden repetido.
Fallo mantiene intento y bloquea nuevo turno hasta recuperación o cancelación.

## Recuperación

Cada ejecución reclama LeaseToken nuevo y vencimiento timeout configurado+30 s.
Finisher verifica token, estado Pending, operación pendiente y vigencia: descarta
resultado tardío después de cancelación/reclamación/vencimiento. También verifica
que el propietario siga activo y con Student en BD; revocación descarta respuesta
y devuelve 401. No confiar en roles cacheados durante una llamada prolongada.

Límite de generación global y de transporte más deadline por orquestación. Cancelar
HTTP persiste Canceled recuperable con token de persistencia independiente/10 s;
timeout persiste Timeout; caída del proceso deja lease vencido visible como Interrupted.
GET no escribe ni llama IA. El estudiante recupera explícitamente la misma clave,
acción y contenido; máximo 3 ejecuciones, con Retry-After/RetryNotBefore respetado.
No hay generación automática al abrir/refrescar/reanudar, ni recuperación en segundo plano.

Cancel de un requestId propio libera el turno sin borrar intento y cerca resultados
tardíos. Admite revisión anterior para el MISMO turno identificado, porque abortar
la petición puede cambiar su revisión al registrar fallo; nunca cancela otro turno.
Revisiones negativas se rechazan. Cancelación no garantiza que el tercero deje de
procesar/cobrar; recuperación puede consumir cuota de nuevo. La UI lo advierte.

No se promete exactly-once de facturación externa: caída tras respuesta remota y
antes de commit puede requerir nueva llamada. Se garantiza que el intento y la
respuesta confirmada no se duplican en BD por repetir la misma clave. Se guarda
metadata del último intento y uso reportado; auditoría integral de cada ejecución
es pendiente de 8, sin logs técnicos de conversaciones ni respuestas académicas.

Angular bloquea doble envío, mantiene solicitud incierta en memoria, consulta
estado y permite recuperar la misma clave. Poll de GET cada 2 s mientras hay
generación: no consume cuota. Al salir se aborta petición; historial confirmado
queda durable. Hint/Simplify equivalen a un único turno persistido, sin llamadas dobles.

## Límites y operación

Máximo 100 mensajes/sesión y 100 sesiones/alumno; 6 envíos/minuto/alumno/proceso,
incluidos replays/CSRF fallido. Presupuesto diario, cuotas distribuidas, alertas y
auditoría amplia permanecen para 8; estos límites no garantizan un presupuesto real.
Proveedor/modelo fijados al crear; si configuración cambia, 409 antes de generación.
Restaurar configuración original o crear otra sesión; nunca reenviar automáticamente
historial a otro proveedor. No se almacenan keys ni se muestran errores externos.

Todo Verify/HTTP/browser QA usa Mock, bases PostgreSQL aisladas y @example.invalid.
TestHost fuerza Mock en DI y configuración, y vacía claves de proveedores externos.
Los scripts restauran entorno al salir; .env/Groq principal quedan intactos.
