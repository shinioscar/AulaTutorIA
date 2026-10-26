# Tutorías y diagnósticos preparados — LEARN-1

## Guía del estudiante

En **Mis tutorías**, elige Tutorías o Diagnósticos y una de las dos tarjetas.
Conversacional conserva el diálogo actual. Preparada permite trabajar con contenido
generado de antemano. Indica tema, nivel, objetivo opcional y extensión compatible.
El modelo procede de **Proveedor de IA**: no necesitas introducir otra clave.

Crea la sesión y pulsa **Preparar tutoría/diagnóstico** para enviar la solicitud.
La preparación debe terminar y validarse antes de comenzar. Durante las actividades
no se solicita IA: responder, pistas de tutoría, otra explicación, avanzar, pausar,
reanudar y finalizar usan datos PostgreSQL. Puedes recargar y consultar el estado.
Un envío incierto se recupera con la misma solicitud; no crees otra para repetirlo.

En tutoría encontrarás explicación, ejemplo, actividad y ayudas progresivas.
En diagnóstico hay una respuesta por pregunta y no se entregan ayudas ni resultados
de corrección hasta finalizar. Las preguntas están fijadas desde el comienzo.
Las respuestas abiertas y código quedan pendientes de revisión; nunca se ejecutan.

Al finalizar tienes siempre el reporte local. La evaluación cualitativa es una acción
adicional explícita. En diagnóstico intenta producir análisis y plan juntos, sin
una tercera solicitud para el plan. Si falla, conserva cierre/evidencias y permite
recuperación explícita. Consultar el reporte no vuelve a generar.

En **Comenzar mi plan**, elige un objetivo y modalidad. Se registra tu aceptación y
se abre una tutoría nueva; iniciar/preparar contenido es otra acción tuya. El
diagnóstico cerrado se conserva. Trabajado significa que registraste evidencia,
no dominio certificado ni nota oficial. Revisa el reporte antes de compartirlo con
un docente relacionado; puedes revocar futuras consultas.

## Contratos y límites independientes

| Recurso | Extensión | Actividades | Salida solicitada |
|---|---|---:|---:|
| Tutoría preparada | Corta / media / larga | 3 / 6 / 9 | 4096 / 8192 / 12288 |
| Diagnóstico preparado | Corto / medio / largo | 4 / 8 / 12 | 4096 / 6144 / 8192 |
| Evaluación preparada, con plan cuando corresponde | Una operación agrupada | Evidencias íntegras | 4096 |
| Generación docente IA-2 | Contrato anterior | 1–4 | 4096 |

Estos son presupuestos de aplicación, no consumo ni máximos comerciales garantizados.
Solo se habilita lo que cabe en contexto/salida conocidos. Se usa una provisión
conservadora basada en bytes UTF-8 más salida y margen; no se añade un tokenizer
ni una petición de conteo. Entrada total de transporte 32000 caracteres, contexto
final íntegro hasta 24000, guion persistido hasta 64000 caracteres. No hereda 20000
del importador docente. Hasta tres pistas/alternativas por actividad, textos acotados.
La validación no garantiza que un proveedor complete el presupuesto: un truncado
se rechaza sin reparación. Cuotas, razonamiento interno y disponibilidad externa
pueden impedir una solicitud admisible localmente.

OpenRouter conserva context_length y top_provider.max_completion_tokens del catálogo
con vigencia BYOK. Modelos existentes sin esos metadatos necesitan refresco explícito.
Institucional: techo conservador de contexto 32768/salida 4096 para Groq gpt-oss
20b/120b y Gemini 2.5 revisados; otros modelos y Ollama con contexto de runtime
desconocido no habilitan preparación automáticamente. Mock usa límites simulados,
sin cuota/uso ficticio. Los catálogos gratuitos BYOK y aceptación histórica se conservan.

Fuentes de capacidad revisadas: [metadatos OpenRouter](https://openrouter.ai/docs/api/api-reference/models/get-models),
[GPT-OSS en Groq](https://console.groq.com/docs/model/openai/gpt-oss-20b),
[contexto y conteo Gemini](https://ai.google.dev/gemini-api/docs/tokens).
No se hicieron inferencias externas para revisarlas.

## HTTP y recuperación

Base y autorizaciones de [TUTORING_HTTP](TUTORING_HTTP.md), cookies/HTTPS/CSRF.

- GET `/api/tutoring-sessions/prepared-options`: extensiones compatibles del
  proveedor activo, sin inferencia ni instalación de un vínculo persistente.
- POST creación: añade experience=Tutoring/Diagnostic, modality=Conversational/Prepared,
  length=Short/Medium/Long. Omisión mantiene el contrato conversacional anterior.
- POST `/{id}/messages`: PrepareSession o FinalReport explícitos con el contrato de
  turno existente; lease/retry/errores siguen ADR-017/030.
- POST `/{id}/cards`: Start/SubmitAttempt/Hint/Simplify/NextActivity locales.
- Pause/resume/complete y cancel conservan rutas, revisión y semántica existentes.
- POST `/{id}/prepared-plan/start`: requestId, planId, objectiveOrder, modality, length;
  acepta objetivo propio y crea/recupera su tutoría privada sin generación.

Generación Requested/Generating/Ready/Failed/Interrupted/Cancelled se deriva de los
turnos durables; ejecución Created/Active/Paused/Completed permanece independiente.
El contenido validado y la evaluación confirmada son inmutables. La proyección
Student solo entrega actividad autorizada, ayudas reveladas y resultados permitidos;
no guion privado completo, soluciones, futuras ayudas ni rúbricas privadas.

Los límites de facturación, revocación, reportes compartidos y datos reales siguen
[PERSONAL_AI](PERSONAL_AI.md) y [ADR-031](../decisions/ADR-031-aprendizaje-preparado.md).
