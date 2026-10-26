# Conexiones personales de IA — BYOK-1

[Decisión ADR-030](../decisions/ADR-030-conexiones-personales-ia.md),
[auditoría y QA](../iterations/BYOK_1.md). El proveedor institucional sigue siendo
predeterminado; su configuración Groq y claves privadas permanecen intactas.
Implementación REST, sin paquetes/SDK nuevos ni segundo motor académico.

## Uso para docentes y estudiantes

1. Abre **Proveedor de IA** en el menú lateral.
2. Para OpenRouter, pulsa **Conectar OpenRouter**, inicia sesión y autoriza en
   su página. Google, cuando se ofrezca, se completa allí por el propio usuario.
3. Regresa a AulaTutor y pulsa **Elegir modelo**. Solo se muestran modelos gratuitos
   con precio confirmado, identificados con **Gratis**.
4. Revisa proveedor/modelo, acepta el envío de contenido y activa la selección.
   Los modelos con costo o precio desconocido no se ofrecen en los catálogos personales.
5. **Usar institucional** cambia la selección de nuevas operaciones. Las sesiones
   existentes mantienen su conexión y modelo originales.

En móvil se usa navegación completa; en escritorio, popup desde el clic del usuario.
Si está bloqueado, **Continuar en esta pestaña** permite seguir por navegación normal.
Cerrar/cancelar o vencer la autorización permite iniciar otra; nunca se solicita
copiar una clave en el flujo OAuth habitual.

Para Gemini, Claude o Groq, abre la consola enlazada, crea una clave de API en tu
cuenta, pégala y guárdala. Después elige un modelo y actívalo. Una clave guardada
indica custodia local, no una inferencia de prueba. Su disponibilidad se comprueba
al solicitar una operación académica; no se genera al abrir, guardar o refrescar.
Claude admite un workspace opcional para claves que actúan en varios workspaces.
Las suscripciones de chat y los créditos/permisos de API se gestionan por separado.

**Desconectar** retira el uso en AulaTutor y obliga a elegir explícitamente otra
conexión o el institucional. Las sesiones ligadas a esa revisión conservan sus
evidencias y reportes, pero ya no generan. La revocación remota se gestiona en la
consola del proveedor; AulaTutor no promete haber eliminado la clave externa.
Cambiar una clave también invalida sus vínculos anteriores.

## Propiedad, persistencia y cifrado

Teacher aprobado y Student activos administran únicamente sus conexiones. Los
servicios revalidan actividad, organización y rol en BD, también tras esperar el
lock. Admin no recibe permiso implícito ni lecturas de secretos; cuentas con rol
académico adicional pueden abrir sus propias conexiones desde el diagnóstico.
Ningún formulario acepta propietario, callback, host o conexión de otro usuario.

La migración aditiva `PersonalAiConnections` conserva las siete anteriores:

| Registro | Contenido |
|---|---|
| PersonalAiConnection | Propietario/organización, proveedor, ciphertext, SHA256, revisión, estado, método, UTC, modelo preferido y workspace opcional |
| PersonalAiSelection | Institucional, personal o sin selección; modelo y aceptación de posibles cargos |
| AiInferenceBinding | Snapshot inmutable de propietario, conexión/revisión, proveedor/modelo, capacidad Schema y consentimiento de pago |
| PersonalAiOAuth | Hashes de state/browser/sesión/stamp, verifier/código cifrados y temporales, estado, vencimiento y UTC |
| PersonalAiModel | Catálogo validado, compatibilidad/precio conocidos y vigencia |

DTO de lectura: identificador, proveedor, huella parcial, estado/método, fecha y
modelo. Nunca ciphertext, clave, código OAuth ni verifier. La auditoría append-only
registra GUID/tipo/actor/UTC/correlación/cambio, sin esos secretos ni contenido
académico. Las restricciones compuestas y triggers impiden transferir propietarios,
reescribir vínculos o atribución de reservas y reutilizar estados OAuth consumidos.

`PersonalAiVault` usa Data Protection estándar con anillo y application name
independientes de las cookies. Propósito `credential/v1/owner/connection/revision`;
la huella no reemplaza el cifrado. Antes de declarar custodia disponible realiza
un roundtrip local con un valor ficticio aleatorio. No hay protector efímero ni
clave maestra en BD/código. Los secretos solo se recuperan en servidor para el envío.

| Entorno | Custodia requerida |
|---|---|
| Windows | Anillo persistente protegido por DPAPI del usuario de servicio. Por defecto, subdirectorio `personal-ai` del KeyPath de cookies; anillo/purpose separados. |
| Linux | KeyPath explícito, privado y distinto del de cookies; certificado PEM con clave privada externa. Directorio sin acceso de grupo/otros y clave privada sin acceso de grupo/otros. Sin estos requisitos, BYOK queda deshabilitado. |

Parámetros de servidor: `PersonalAi__KeyPath`, `PersonalAi__CertificatePath` y
`PersonalAi__CertificateKeyPath`. No son rutas elegibles desde Angular. Mantener
permisos del usuario de servicio y volúmenes privados persistentes; los archivos
de certificado/clave no se versionan. La protección de cookies Linux heredada sigue
siendo un requisito institucional pendiente, independiente de este anillo.

Respaldo y recuperación: conservar por separado BD cifrada, anillo completo y
protector externo. Windows requiere recuperar también el contexto DPAPI del
usuario/máquina; copiar solo XML no garantiza portabilidad. Linux requiere el
certificado y su clave privada correspondientes a las claves del anillo. No borrar
claves anteriores de Data Protection: son necesarias para descifrar credenciales
previas. Antes de cambiar certificado/protector, validar una restauración aislada
con el protector anterior. No hay herramienta automática de rotación de certificado
ni migración masiva de ciphertext en esta entrega. La rotación operativa del
protector y recuperación del despliegue Linux deben ensayarse en destino antes de
habilitarlo para personas reales; no se declaran demostradas por pruebas Windows.

## OAuth PKCE y cookie Strict

Autorización fija `https://openrouter.ai/auth`; intercambio fijo
`https://openrouter.ai/api/v1/auth/keys`, JSON con code, code_verifier y S256.
Verifier aleatorio de 32 bytes, challenge SHA256/base64url, state independiente.
Transacción de cinco minutos ligada a usuario, sesión y security stamp. Los secretos
temporales usan además Data Protection con vencimiento criptográfico.

La cookie principal conserva Secure/HttpOnly/SameSite Strict, path `/` y dos horas
sin sliding. Una cookie auxiliar `__Host-AulaTutor.AiAuthorization`, Secure/HttpOnly,
Lax y cinco minutos, permite validar el GET cross-site sin relajar la sesión.
El callback consume una sola vez el código en servidor y redirige a una URL limpia.
La página de retorno finaliza mediante POST autenticado con CSRF; solo entonces
se intercambia y guarda la clave. Código/verifier no pasan por DTO Angular, storage
o postMessage. Antes de navegar al proveedor, el popup elimina opener. El padre
consulta su estado cada cinco segundos, sin generar IA, y no interpreta
WindowProxy.closed como cancelación porque COOP puede separar las ventanas.
Un mensaje opcional de retorno solo acelera esa consulta; su contenido es una
constante y se comprueban origen y fuente.

El inicio usa exclusivamente `PersonalAi__PublicOrigin`, que debe estar incluido
literalmente en `PersonalAi__AllowedOrigins__0` (o índices posteriores), ser HTTPS
y carecer de credenciales/ruta/query/fragmento. Predeterminado localhost:4200.
El proxy local conserva `/api/**`; despliegue requiere mismo origen/TLS confiables.
Nunca construir retorno desde Host/Origin del navegador.

Login/cambio de cuenta/logout cancelan intentos pendientes del navegador. Dos
pestañas del mismo navegador conservan solo el intento más reciente. El intercambio
se reclama y vacía secretos antes del HTTP; no hay transacción abierta durante la
red ni retry automático de un intercambio incierto. Se revalidan sesión/stamp/rol
al guardar. Ante fallo o pérdida de respuesta, iniciar otra autorización y revisar
en OpenRouter las claves externas que pudieran haberse creado.

Se suprime el log de URL de Hosting.Diagnostics y los loggers HttpClient de IA y
metadatos. El proxy de producción también debe excluir query de callback, cuerpos,
cabeceras de autorización y cookies de sus logs. Las cabeceras no-store/no-referrer
se conservan. Intentos abandonados vencen; un nuevo inicio limpia su material
cifrado vencido sin borrar el registro mínimo. La política de retención de esos
metadatos sigue pendiente antes de datos reales.

## Selección e inferencia

IAiProvider pasa a una fachada scoped con proveedor institucional independiente.
Salud pública y diagnóstico Admin consultan siempre el institucional, sin selección
personal ni red. Sin configuración personal, se conserva el comportamiento anterior.
Una selección retirada queda en `None`, sin fallback automático.

Al crear sesión, operación docente o simulación se captura el vínculo; sesiones
institucionales heredadas tienen vínculo nulo. Diagnóstico, planes, conversación,
refuerzo, propuestas, generación precargada y evaluación agrupada reutilizan los
orquestadores actuales. La simulación docente usa la conexión propia del docente;
evaluación estudiantil usa el vínculo propio de la sesión. No hay tareas autónomas
nuevas sin actor. Lecturas, reportes locales, supervisión y ejercicios/ayudas
precargados continúan sin llamadas IA.

Antes de cada HTTP se revalidan conexión/revisión/actor y cuotas en PostgreSQL.
Desconexión/rotación y confirmación de resultados comparten el lock de admisión;
una respuesta tardía de revisión retirada se descarta. No se reescriben intentos,
evidencias, versiones ni reportes anteriores. Replays confirmados no generan;
recuperación conserva clave/acción/texto/modelo y los límites existentes.

## Modelos, costos y transporte

| Proveedor | Fuente/capacidad inicial | Precio y disponibilidad |
|---|---|---|
| OpenRouter | GET oficial `/api/v1/models`, TTL 15 min; entrada/salida texto, contexto ≥32768, salida ≥4096, response_format + structured_outputs; excluye routers y retiros conocidos | Gratis solo con precios numéricos no negativos y todos cero. Cuenta/modelo concretos pueden no estar disponibles. |
| Groq | Catálogo conservador revisado 23/10/2026: openai/gpt-oss-20b y 120b; adaptador existente | Costo/cuota dependen de la cuenta; aceptación explícita. |
| Gemini | gemini-3.8-flash y 2.5-flash/flash-lite/pro, formatos existentes | Los 2.5 pueden requerir acceso previo. Costo/cuota desconocidos para la cuenta. |
| Claude | claude-opus-5-5, Messages versión 2026-10-21, output_config.format json_schema | Modelo de API, costo/acceso según cuenta y workspace. |

Los catálogos locales caducan treinta días tras la revisión fechada; ampliar esa
fecha requiere revisión técnica de las fuentes, no un refresco que invente vigencia.
OpenRouter actualiza metadatos desde backend sin enviar una clave al navegador.
Los límites de contexto/salida nulos, ausentes o no numéricos dejan ese modelo
incompatible, sin impedir cargar el resto del catálogo ni inferir capacidades.
Un catálogo vencido/fallido no autoriza usar información antigua para generar.
No se asume gratuidad por sufijo `:free`. Por encargo del 24/10/2026, GET y refresco
de todos los catálogos personales devuelven únicamente Free + PriceKnown. La UI
filtra también ambos campos, etiqueta Gratis y no ofrece mostrar modelos de pago
ni aceptar cargos. Un catálogo vacío válido se informa como falta de modelos
gratuitos confirmados, separado de un fallo de actualización. Groq/Gemini/Claude
no tienen gratuidad confirmada en el catálogo local actual, por lo que su selector
queda sin opciones; disponer de una clave o de un cupo gratuito por cuenta no
acredita precio cero del modelo. Se conservan los metadatos completos en servidor
y los contratos/vínculos previos, sin reescribir sesiones ni retargeting.
La interfaz envía paidAccepted=false para sus selecciones. La aceptación histórica
se congela por modelo en el vínculo. Un modelo gratuito cuyo precio cambia se rechaza
si no hay consentimiento; el envío gratuito además fija max_price cero para
prompt/completion/request/image, require_parameters=true y allow_fallbacks=false.
No se compran créditos ni se proponen modelos alternativos automáticamente.

OpenRouter/Claude usan Schema para el sobre conversacional del motor existente,
sin duplicar política académica. Todos pasan por límites/validación JSON locales,
timeouts/cancelación, límites de respuesta, rechazo de truncado/echo de clave y
reservas del transporte existente. Solo 429/503 admiten como máximo un retry
configurado, respetando Retry-After; predeterminado cero. Sin cookies/redirecciones
salientes ni URL arbitrarias. Ollama mantiene exclusivamente su configuración
administrada; OpenAI directo permanece reservado.

## Cuotas y atribución

Se separan `Institutional`, `Personal` y `PersonalQa`. Configuración personal:
`AiQuota__PersonalLimits__*` y `AiQuota__PersonalProviders__OpenRouter/Groq/Gemini/Anthropic__*`.
Los valores iniciales son conservadores como ADR-025; no equivalen a cuota real del
tercero. Concurrencia global de proceso/BD permanece compartida como límite de
recursos, con límites personales adicionales.

Cada reserva personal conserva conexión/revisión/huella, cuenta externa cuando se
conozca, proveedor/modelo, tipo+GUID de operación y correlación del HTTP. Cada retry
físico reserva nuevamente. Usuario/proveedor se agregan entre claves y revisiones;
el pool compartido conservador por proveedor evita presentar cuentas desconocidas
como independientes. No se infiere identidad de cuenta por una clave distinta.
Actualmente el intercambio OAuth solo acredita key: ExternalAccount permanece
desconocido. Workspace manual no se presenta como identidad externa verificada.

Se mantienen días UTC, ventanas móviles, budgets de provisión, cargos fallidos o
inciertos, leases y enfriamiento persistente. Las estimaciones no son uso facturado;
uso ausente permanece null. No se calculan dinero, créditos o solicitudes restantes.
Authentication marca la revisión actual inválida; 402 produce CreditsRequired.
Un cambio de precio sin consentimiento produce PaymentAuthorizationRequired;
429 conserva espera; ningún error cambia a otra clave o al institucional.

## Contrato HTTP

Base `/api/ai-connections`, Teacher aprobado/Student activos, HTTPS/cookie y CSRF
en escrituras. Sin actor/propietario enviados por navegador. 401/403/404/409 siguen
los contratos existentes; 503 indica almacenamiento/custodia/catálogo indisponible.

| Método / ruta | Acción |
|---|---|
| GET `/` | Configuración propia saneada e institucional |
| POST `/manual` | provider, apiKey y workspace opcional; guarda cifrado, sin prueba IA |
| PUT `/selection` | connectionId/model/privacyAccepted/paidAccepted; null elige institucional |
| DELETE `/{id}` | Desconecta una conexión propia, conserva metadatos |
| GET `/models/{provider}` | Catálogo cacheado; metadatos oficiales si requiere actualización |
| POST `/models/{provider}/refresh` | Refresco explícito, limitado a uno por minuto de frescura |
| POST `/openrouter/start` | Transacción PKCE y URL oficial; 3 inicios/min por actor |
| GET `/openrouter/callback` | Excepción cross-site: cookie de transacción/state, sin instalar key ni debilitar auth |
| POST `/openrouter/complete` | Claim único, intercambio y custodia con sesión/CSRF originales |
| POST `/openrouter/cancel` | Cancela el intento del navegador |

La gestión tiene límite HTTP por cuenta; no sustituye admisión de inferencia.
No hay endpoint de lectura de secretos, prueba personal automática ni diagnóstico
público del modelo. Las rutas administrativas anteriores conservan permisos.

## QA y límites de despliegue

`PersonalAi__QaSimulation=true` solo funciona en Development, Mock, external calls
false y BD `aulatutor_http_GUID`/`aulatutor_test_GUID`. El handler carece de fallback
de red, admite solo hosts fijos y claves ficticias qa_; reutiliza el guion Mock,
etiqueta modelos simulados y no inventa uso. Sus reservas usan PersonalQa.
El runner Edge intercepta exclusivamente la página de autorización con una pantalla
ficticia; callback/CSRF/Identity/cifrado/cuotas y servicios reales siguen en backend.
La página simulada declara UTF-8 y COOP para comprobar el retorno sin depender
de opener. El reinicio verifica descifrado y continuación del vínculo personal.
`scripts/test-byok-log-safety.ps1` revisa los cuatro logs QA con lectura compartida;
admite archivos vacíos y rechaza marcadores de claves, código OAuth, contraseñas
ficticias y campos privados de protocolo. Sus resultados no son una garantía sobre
configuraciones futuras de logging o proxies externos.

La prueba con cuenta OpenRouter real, Google real, cuotas reales y calidad semántica
no se ejecuta ni queda autorizada por QA. Las fuentes oficiales se verifican para
implementar el contrato, separadamente de esa prueba externa pendiente. Persisten
los bloqueantes de [READINESS](../pilot/READINESS.md): privacidad institucional,
retención, credenciales/identidad, TLS, mínimo privilegio y respaldos. BYOK no
autoriza datos reales ni sustituye el consentimiento institucional.
