# ADR-014 — Proveedores intercambiables y diagnóstico administrativo

Fecha: 2026-10-13. Estado: aceptado para Iteración 2 por encargo explícito.
Amplía el maestro con integración funcional de Gemini y Ollama, además de Groq.
No modifica el documento fuente ni autoriza tutorías, clases o evaluación.

## Decisión

Application conserva IAiProvider, mensajes system/user/assistant, respuestas,
uso reportado (sin estimaciones inventadas), CancellationToken, capacidades y
AiProviderException/AiErrorCode. AiStreamChunk reserva la forma de eventos;
StreamingImplemented=false para todos. Domain no adquiere dependencias IA.

Infrastructure usa REST oficial mediante IHttpClientFactory, sin nuevos paquetes
ni SDK de terceros. Mock conserva respuesta, cancelación, ausencia de red/uso y
rechazo de JSON originales. Selección explícita al inicio: Mock predeterminado;
Groq/Gemini requieren clave y modelo; Ollama requiere modelo y URL local válida.
Una selección incompleta o desconocida falla sin fallback ni conexión externa.
OpenAI queda reservado y se rechaza; los modelos gpt-oss no habilitan su API.

Groq utiliza /openai/v 1/chat/completions y max_completion_tokens; Gemini adapta
systemInstruction, contents user/model y generationConfig.responseFormat.text;
Ollama usa /api/chat, stream=false y options.num_predict. No se permiten URLs
remotas configurables para Groq/Gemini. Ollama permite http/https, solo localhost,
127.0.0.1, ::1 o host.docker.internal, sin credenciales/rutas/query/fragmentos.
No hay URL elegible desde el navegador, redirecciones HTTP ni cookies salientes.

## Capacidades y JSON

El catálogo local conservador se revisó el 2026-10-13. No descubre modelos en red
durante arranque/salud ni garantiza su disponibilidad para una cuenta concreta.

| Proveedor / modelo | JSON | Schema | Estricto del proveedor |
|---|---|---|---|
| Mock | No | No | No |
| Groq openai/gpt-oss-20b, openai/gpt-oss-120b, qwen/qwen3.8-27b | Sí | Sí | Sí |
| Groq openai/gpt-oss-safeguard-20b | Sí | Sí | No |
| Otros modelos Groq | Modo JSON | No declarado | No |
| Gemini gemini-3.8-flash y 2.5-flash/flash-lite/pro | Sí | Sí | No se equipara a strict de Groq |
| Ollama gpt-oss, gpt-oss:20b, gpt-oss:120b locales | Sí | Sí | No declarado |
| Otros modelos Gemini/Ollama | Solo texto hasta revisión | No declarado | No |

StreamingTransport describe el transporte de texto de las APIs, no una función
implementada ni compatibilidad con todas las combinaciones de formato. Groq no
admite streaming con Structured Outputs. Todas las llamadas actuales son completas.

Formatos: text, json (objeto), json-schema (objeto raíz). Subconjunto portable:
type, properties, required, additionalProperties=false, items, enum, description;
tipos object/array/string/integer/number/boolean/null. Sin referencias externas,
uniones ni restricciones omitidas silenciosamente: una palabra clave desconocida
se rechaza antes de llamar. Strict requiere todas las propiedades obligatorias.
Validación local de JSON, tipos, required, enum y propiedades adicionales; no
quita Markdown ni acepta respuestas truncadas. El esquema es máximo 16.000
caracteres/profundidad 16, salida JSON profundidad 32; no es un validador completo.

## Límites y protección

- Tiempo total 30 s (1–120), incluyendo espera por cupo; concurrencia 2 (1–8) por
  proceso. Entrada 32 mensajes/32.000 caracteres, salida solicitada 1–2048 tokens;
  máximo 2 MB de respuesta HTTP y 64.000 caracteres de texto normalizado.
- Reintentos 0 por defecto, máximo 1, solo 429/503; Retry-After delta/fecha respetado.
  Si supera espera permitida (5 s, configurable 1–10), devolver error para que el
  operador espere. No reintentar errores de conexión con consumo incierto.
- Errores tipados de credencial, permiso, cuota, indisponibilidad, conexión,
  timeout, contrato, formato y respuesta inválida. Se descarta el cuerpo de error
  externo; no hay inner exception sensible. Cancelación del llamante se propaga.
- Sin claves en URL, Angular, resultados o logs; rechazo de salida que reproduzca
  una clave configurada. Deshabilitado logging automático del cliente HTTP.
  Telemetría propia solo proveedor/modelo, duración, estado y, en diagnóstico,
  correlación, versión fija del prompt y consumo. Sin prompt/respuesta almacenados.
- Salud conserva contrato heredado iteration=1 por compatibilidad: configuración
  IA, sin red ni readiness DB. Cambiar proveedor no dispara generación.
- GET /api/admin/ai/ consulta configuración saneada; POST /test usa prompt fijo
  sin parámetros académicos,128 tokens y 3 intentos/min por cuenta/proceso (también
  cuentan intentos con CSRF inválido). Ambos verifican Admin activo en backend;
  POST exige CSRF/HTTPS. UI usa acción explícita y advierte consumo externo.

Las cuotas distribuidas, auditoría persistente, presupuesto por institución,
streaming y orquestación por caso de uso quedan para encargos futuros. El modelo
no sustituye permisos de recursos ni cambia la privacidad de conversaciones.

## Fuentes oficiales

- [Groq REST](https://console.groq.com/docs/api-reference) y
  [Structured Outputs](https://console.groq.com/docs/structured-outputs): formatos,
  modelos con strict, required/adicionales y restricción de streaming.
- [Gemini GenerateContent](https://ai.google.dev/api/generate-content),
  [JSON](https://ai.google.dev/gemini-api/docs/structured-output) y fichas de
  [3.8 Flash](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash),
  [2.5 Flash](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash),
  [Lite](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash-lite) y
  [Pro](https://ai.google.dev/gemini-api/docs/models/gemini-2.5-pro). Los 2.5 tienen
  acceso limitado a usuarios previos; no presuponer acceso en cuentas nuevas.
- [Ollama chat](https://docs.ollama.com/api/chat) y
  [Structured Outputs](https://docs.ollama.com/capabilities/structured-outputs):
  format, stream=false, métricas y ejemplo gpt-oss. No se instala/descarga un modelo.
