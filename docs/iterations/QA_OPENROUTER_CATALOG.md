# Catálogo OpenRouter — corrección y filtro gratuito

Encargo del 24/10/2026: resolver el error al pulsar **Elegir modelo**, identificar
los gratuitos y ofrecer únicamente modelos gratuitos en los catálogos personales.
No modifica el maestro, las credenciales, las sesiones ni los vínculos anteriores.

## Causa y cambio

El endpoint público oficial responde HTTP 200, pero algunos modelos incluyen
`top_provider.max_completion_tokens: null`. `JsonElement.TryGetInt32` lanza
InvalidOperationException si el elemento no es Number: una sola entrada provocaba
el 503 seguro «No se pudo actualizar el catálogo» para todo el listado.
Se reprodujo antes del arreglo: cuatro casos de la nueva teoría fallaban con
Null/String. Ahora se comprueba ValueKind antes de convertir contexto y salida;
límites desconocidos dejan ese modelo incompatible sin atribuirle capacidades.

GET y POST de refresco filtran Free + PriceKnown antes de construir el DTO.
Angular aplica la misma condición y elimina la opción de mostrar modelos de pago,
la aceptación de cargos y la preselección de modelos no gratuitos. Cada opción
conserva la etiqueta Gratis, incluso cuando está deshabilitada por incompatibilidad.
La activación desde la interfaz envía paidAccepted=false. Un catálogo vacío válido
se distingue de una petición fallida y permite actualizar de nuevo.

Se conserva el catálogo completo en servidor para revalidaciones y los contratos
de selección/vínculos previos. No se cambia el proveedor institucional ni se
reescriben sesiones. Los catálogos locales Groq/Gemini/Claude tienen precio
desconocido y por ello no ofrecen opciones en el selector actual. Un cupo gratuito
de la cuenta no se presenta como precio cero verificado del modelo.

## Verificación de esta corrección

- Prueba directa del lector corregido con GET público oficial, sin Authorization:
  HTTP 200; 447 entradas aceptadas, 7 límites de salida nulos, 20 modelos con
  precios cero y 5 gratuitos compatibles en esa respuesta. Son una observación
  puntual, no un catálogo fijo ni garantía de acceso/cuota de una cuenta.
- TypeScript, build Angular y 80 comprobaciones de contraste: aprobados.
- Edge aislado con respuestas HTTP ficticias: se ocultan pago y precio desconocido
  incluso si llegan al cliente; etiqueta Gratis, incompatibles deshabilitados,
  foco inicial, control de 44 px, activación sin consentimiento de pago, vacío
  separado de error y recuperación mediante refresco: aprobados.
- Seis capturas Light/Dark en 320/390/1440 px sin desbordamiento horizontal;
  las seis inspeccionadas visualmente. Artefactos privados ignorados en
  `.cache/openrouter-ui-check/`, sin claves/cookies de la sesión personal.
- Pase completo .NET después de corregir los límites y antes del filtro gratuito:
  180 unitarias y 243 de integración PostgreSQL aprobadas; cero fallidas/omitidas.
  No se atribuye a ese pase la validación posterior del filtro.
- Pase específico final de conexiones personales con el filtro ya aplicado:
  47 pruebas de integración PostgreSQL aprobadas; cero fallidas/omitidas.
  Incluye GET/refresco con límites nulos, exclusión de pago/precio desconocido,
  conservación de metadatos internos, catálogos locales vacíos y cero inferencias
  al consultar modelos; también propiedad, OAuth/custodia, revocación y cuotas.
- Diff y enlaces documentales revisados; check-publish-safety aprobado sin
  coincidencias de secretos. Maestro con SHA256 original
  FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
- Build Debug de API con cero errores/advertencias y arranque normal sin
  migraciones/bootstrap; API 7080 y Angular 4200 levantadas. Salud por proxy HTTP
  200, únicamente liveness; no acredita modelo ni cuota.

No se ejecutaron Verify.ps1 -Smoke -Http ni el runner E2E completo para este cambio.
Se añadieron aserciones al recorrido BYOK existente y se comprobó su sintaxis;
la ejecución visual de esta entrega corresponde al recorrido aislado descrito,
no se atribuye un nuevo pase a los 266 checks históricos de BYOK-1.

La herramienta de control de la ventana personal de Edge se detuvo al no poder
verificar su URL. La comprobación visual posterior utiliza otra instancia aislada
y respuestas ficticias; no equivale a una prueba de la cuenta conectada del usuario.
Los intentos de CLI que no conservaron su sesión no se cuentan como pases.
Un primer intento de integración se interrumpió porque la opción temporal
AllowExternalCalls=false bloqueaba también el transporte ficticio BYOK de las
pruebas; se retiró esa opción del proceso y se repitió el pase completo con Mock,
claves externas vacías y handlers simulados. Ese intento no se cuenta como pase.
No se generó IA real ni se consumió cuota. No se ejecutó OAuth externo ni se
declara validación semántica, de cuota real o del piloto con personas reales.

Fuente del contrato de metadatos:
[API de modelos de OpenRouter](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties).
Reglas vigentes: [PERSONAL_AI](../architecture/PERSONAL_AI.md) y
[ADR-030](../decisions/ADR-030-conexiones-personales-ia.md).
