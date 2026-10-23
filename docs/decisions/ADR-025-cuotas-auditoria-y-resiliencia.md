# ADR-025 — Reservas IA, auditoría y recuperación

Estado: implementado en Iteración 8; la entrega registra las verificaciones finales.
No modifica maestro, contraseña mínima ni permisos académicos de ADR-012–024.

## Reserva antes de cada intento de transporte

AiHttpTransport usa IAiCallAdmission con PersistentAiAdmission. Cada HTTP físico,
también un retry permitido 429/503, necesita una reserva confirmada antes de enviar.
La implementación crea un scope/contexto independiente; no prolonga una transacción
de tutoría, clase o respuesta mientras espera al proveedor. Mock no usa transporte,
no genera consumo ficticio y sigue disponible sin red externa.

Una transacción breve PostgreSQL ReadCommitted toma pg_advisory_xact_lock(8128001).
Ese lock es por base; no afecta otros proyectos. Revalida actor/rol/actividad y
atribución de grupo antes de admitir, usa clock_timestamp() del servidor, comprueba
presupuestos y guarda AiReservation. El bloqueo de admisión está limitado a tres
segundos; un fallo DB no habilita un envío sin cuota. No hay Redis ni SDK nuevo.

Ventanas móviles de sesenta segundos por usuario y proveedor, días UTC por usuario,
proveedor, organización y grupo. Concurrencia global y por usuario mediante leases
persistentes timeout+30 segundos. La ventana móvil incluye el minuto anterior al
cruce de medianoche. Lecturas, replays y consultas no reservan. Un intento fallido
no devuelve la solicitud al presupuesto diario. Reservar no prueba que el proveedor
recibió o facturó la solicitud: puede interrumpirse antes de enviar.

La finalización solo cambia Reserved una vez: Succeeded indica transporte JSON
recibido (no validación académica/normalización final), Failed una respuesta HTTP
de rechazo y Uncertain un timeout, cancelación, conexión perdida o JSON incompleto.
Uncertain y Reserved mantienen el cupo hasta vencer el lease; vencido no ocupa
concurrencia pero conserva el cargo diario y aparece como incierto en metadatos.
No hay refund, reenvío automático de pendientes ni promesa exactly-once externa.
Resultados tardíos académicos siguen sujetos a los tokens cercados existentes.

429 conserva Retry-After como enfriamiento compartido del proveedor; si falta,
sesenta segundos conservadores. Los rechazos por concurrencia/ventana indican
fecha de liberación, y los diarios el siguiente día UTC. Reintentos HTTP siguen
limitados por ADR-014; un nuevo proceso no elimina reservas ni enfriamiento.
El motor existente muestra el error/espera y conserva intento y requestId.

## Tokens y configuración

AiQuota:Limits predetermina usuario 6/min y 100/día, proveedor 20/min y 1000/día,
organización 1000/día, grupo 300/día, concurrencia global 2 e individual 1.
Son límites locales conservadores, no garantías de aceptación ni las cuotas
contractuales del proveedor. Los límites previos HTTP/proceso permanecen como
defensa adicional; puede aplicar el más restrictivo.

AiQuota:Providers:Groq/Gemini/Ollama permite límites específicos. Los diccionarios
OrganizationDailyLimits y GroupDailyLimits usan GUID del servidor. Solo las sesiones
AssignedClass del propio actor atribuyen grupo; no se elige desde Angular. El
constructor y tutorías autónomas consumen usuario/organización/proveedor, sin
atribuirlos arbitrariamente a todos los grupos del usuario.

Variables .NET, por ejemplo AiQuota__Limits__ProviderPerMinute=15,
AiQuota__GroupDailyLimits__GUID=100 y AiQuota__AllowExternalCalls=false. No se añaden
claves a Angular ni al loader .env; usar variables del proceso/contenedor explícitas.
AllowExternalCalls=false bloquea llamadas externas nuevas al aplicar configuración;
Mock y consultas siguen disponibles. El cambio requiere reiniciar controladamente.
Todas las instancias deben compartir DB y la misma política validada. No se promete
consistencia de límites si operadores despliegan configuraciones contradictorias.

EstimatedTokens es una provisión de planificación: bytes UTF-8 del contexto más
salida solicitada y margen 4096, no un tokenizer ni uso confirmado. El presupuesto
DailyEstimatedTokenBudget (0 desactivado) limita esa provisión, cargando el mayor
entre provisión y uso reportado disponible. Tokens reportados se guardan únicamente
cuando upstream envía enteros no negativos; unknownUsage permanece explícito.
Los campos reportados no garantizan incluir todos los conceptos facturables del
proveedor (por ejemplo razonamiento). No se calcula dinero ni se inventan tarifas;
un presupuesto absoluto de facturación externa no está garantizado por estimaciones.

## Auditoría mínima transaccional

AulaTutorDbContext captura cambios sensibles en SaveChanges/SaveChangesAsync,
incluidas sobrecargas, y guarda AuditEvent en la misma transacción que la escritura.
Un rollback revierte ambos. Categorías: creación de cuenta, aprobación docente,
rol, estado/revocación de sesiones, grupo/membresía, publicación/asignación,
compartición/revocación de reporte y revisión docente. No duplica contenido de
negocio: solo GUID, tipo, actor de Identity, fecha UTC, resultado Committed,
correlación y Added/Modified/Deleted. Mantenimiento explícito sin HTTP tiene actor
nulo y correlación generada; no se atribuye una operación de sistema a un usuario.
La retirada de membresía usa borrado seguido y SaveChanges en una transacción
corta, serializada con el alta; no ExecuteDelete, que eludiría el registro. Es
idempotente si ya no existe. Operaciones administrativas revalidan rol/estado
después de esperar el lock, sin confiar en el Actor cacheado al iniciar el request.

No se guardan claves, contraseñas/hashes, cookies, IP, correos, prompts,
conversaciones, respuestas, comentarios ni versiones completas en la auditoría.
La tabla es append-only por trigger PostgreSQL. Reservas protegen origen y estado
terminal, sin borrado automático. Los rechazos HTTP relevantes se registran como
metadatos estructurados status/correlationId en JSON console, sin cuerpo; no se
pretende que ese log sea una decisión de negocio confirmada en la tabla.

GET /api/admin/operations/audit?page=1 devuelve hasta 50 eventos ordenados,
solo Admin activo revalidado. GET /usage devuelve agregados diarios, estimaciones,
uso reportado y desconocido, sin contenido académico. Estos endpoints no conceden
acceso a sesiones privadas ni a evidencias. La retención/borrado siguen requiriendo
política institucional y proceso explícito antes de datos reales.

## Operación y consultas

Liveness heredado intacto; readiness PostgreSQL Admin y errores DB 503 seguros,
correlación generada por servidor en respuestas y ProblemDetails. Cabeceras
nosniff, no-store, no-referrer y DENY; no CORS abierto. La autorización puede fallar
antes de readiness si DB cae; el pipeline convierte NpgsqlException en 503 genérico.
No se declara disponible el modelo por comprobar conectividad DB/configuración.

Dashboard agrupa cohortes y carga snapshots autorizados en lote, sin consultar
cada grupo/asignación individualmente. Se conserva snapshot RepeatableRead y las
reglas de cohorte, versiones y privacidad. No se truncan denominadores para acelerar.
Se mide número de consultas y carga limitada; no certifica grupos masivos. Paginación
de evidencias y agregados SQL adicionales siguen siendo extensiones si la medición
del piloto lo requiere, sin relajar autorización ni cargar chats.

Una migración aditiva OperationalResilience sigue a las seis históricas intactas.
Restauración, reinicio y límites del piloto se documentan en OPERATIONS/ITERATION_8.
Las futuras clases precargadas, importación JSON, claves individuales, evaluación
agrupada y motor híbrido pueden utilizar el mismo punto de admisión por llamada;
no se implementan ni se simula integración OpenRouter.

Fuentes primarias consultadas para transacciones y operación:
[bloqueos PostgreSQL](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS),
[respaldo lógico PostgreSQL](https://www.postgresql.org/docs/current/backup-dump.html) y
[health checks ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).
