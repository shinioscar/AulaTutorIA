# ADR-022 — Formatos Groq y recuperación de cuota

Fecha: 2026-10-13. Corrección posterior a Iteración 5, por solicitud explícita.
El usuario autoriza pruebas reales de Groq para investigar el constructor y Student.

## Evidencia y decisión

El constructor devolvía `Rejected` para HTTP 400 `json_validate_failed`. Las pruebas
ficticias reprodujeron objetos convertidos en arrays/cadenas y JSON mal formado con
el esquema de clase anidado, incluso con `strict=true`. Reducir razonamiento o
aumentar tokens de forma aislada no lo resolvió. No se atribuye el fallo a cuota
ni a credenciales: el caso observado es validación de salida, no 401/429.

`lesson-design-v2` usa un DTO de transporte poco anidado: metadatos, objetivos,
secciones y ejercicios en la raíz. Cada ejercicio referencia sectionId/objectiveId.
El servidor reconstruye LessonContent y rechaza referencias huérfanas, duplicados,
reglas privadas inválidas y objetivos sin actividad. No repara JSON defectuoso ni
relaja validadores. No cambia el contrato HTTP, snapshots o versiones existentes.
Mock emite ese formato solamente para sus guiones docentes y sigue sin red/uso.

El prompt incluye estructura de ejemplo y reglas explícitas por tipo de ejercicio.
Solo Plan/Content/Exercises/Rubrics docentes permiten hasta 4096 tokens de salida;
el resto conserva máximo 2048 y los presupuestos específicos previos. GPT-OSS de
Groq usa reasoning_effort=low para diseño/aprendizaje, medium para tutoring_*
(el valor habitual de tutorías), include_reasoning=false y temperatura 0.2 en JSON.
Otros modelos conservan sus parámetros. No hay fallback tras un fallo.

Infrastructure reconoce exclusivamente `error.code=json_validate_failed` en HTTP
400 de Groq como InvalidResponse; también tool_use_failed, cuando el modelo intenta
emitir una función que no fue habilitada. Lee como máximo 16 KiB/profundidad 8, descarta
message/failed_generation y nunca copia cuerpos externos a HTTP/logs/excepciones.
Errores desconocidos, cuerpos inválidos o grandes permanecen Rejected. Se mantiene
Retry-After, cancelación, límite de concurrencia y cero reintentos por defecto.

## Student

`learning-v2` aclara el cierre preliminar impuesto por el servidor y las reglas de
ShortAnswer/TrueFalse. Evidence.outcome se envía como nombre, no ordinal numérico,
y feedback incluye el resultado del comparador. Se indica que la orientación no
puede sustituir ese resultado. Se reconoce el mapa ordinal solo para contextos
durables anteriores; no se reescriben chats, claves ni evidencias existentes.
La calidad curricular sigue requiriendo revisión humana: coincidencia de una regla
y validez estructural no verifican la corrección pedagógica del contenido generado.
El transporte del plan personal elimina el array activities: cada objetivo contiene
los campos de su única actividad. Application reconstruye PlanContent y mantiene
las validaciones de 3–5 objetivos, tiempos, evidencias y reglas privadas. HTTP y
persistencia conservan su formato; tampoco cambia la posición de rutas existentes.
El pequeño sobre text/activity/focus de tutoría/simulación selecciona JSON object
desde el inicio en proveedores con JSON. La política incluye formato y ejemplo;
Application exige exactamente esos tres campos, tipos/longitudes/actividad válidos.
Se conserva tutoring-v1 compatible con sesiones anteriores. No se aceptan envoltorios
name/arguments ni se activan herramientas para satisfacer una respuesta rechazada.

Las consignas usan el renderizador seguro existente para bloques SQL. El conteo
diagnóstico excluye ejercicios libres. Tras RetryNotBefore, Student refresca una
vez la elegibilidad mediante GET; nunca vuelve a generar automáticamente. Docente
y simulación muestran errores en español y bloquean el reintento durante la espera.
Una lectura posterior exitosa elimina el aviso de carga fallida anterior; conserva
mensajes sobre envíos no confirmados o conflictos para poder resolverlos.

## Pruebas y límites

Verify/CI/TestHost siguen usando Mock/HTTP simulado, sin claves externas. El script
separado Probe-Groq.ps1 exige -AllowRealCalls, configura una BD temporal con cuentas
@example.invalid, limita a 10 llamadas, espera al menos 65 s entre ellas y se detiene
en cualquier fallo, incluido 429. -StudentOnly omite la llamada docente cuando ya
fue comprobada. -KeepRunning conserva la base también si falla, para diagnóstico
o revisión visual; Cleanup-GroqProbe.ps1 elimina proceso/base/estado privado.
Nunca se escribe .env ni se utilizan conversaciones del usuario.

La cuenta probada informa 8000 TPM. Groq publica para gpt-oss-20b gratuito 30 RPM,
1000 RPD, 8000 TPM y 200000 TPD; los límites exactos dependen de la organización.
El espaciamiento del script controla esta prueba, no es un presupuesto global para
todos los usuarios/procesos. La gestión integral de presupuesto continúa para 8.

Fuentes oficiales consultadas: [Structured Outputs](https://console.groq.com/docs/structured-outputs),
[Reasoning](https://console.groq.com/docs/reasoning),
[Rate Limits](https://console.groq.com/docs/rate-limits).
Resultados funcionales, consumo observado y QA en QA_GROQ_FIX.md.
