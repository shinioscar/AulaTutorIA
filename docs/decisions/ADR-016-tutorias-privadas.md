# ADR-016 — Tutorías privadas, política y contexto pedagógico inicial

Fecha: 2026-10-13. Estado: implementada por encargo de Iteración 3.
El maestro permanece intacto. No autoriza clases, calificaciones, evidencias
validadas, compartición, informes docentes ni ejecución de código estudiantil.

## Modelo y privacidad

TutoringSession pertenece a StudentId/OrganizationId derivados del actor activo.
Su FK compuesta a Identity evita cruces de organización. TutoringMessage y
TutoringTurn están ligados a la sesión; DTO nunca acepta propietario del cliente.
Autenticación y propiedad se verifican en backend en cada endpoint. Student ajeno,
Teacher (aunque comparta grupo) y Admin ordinario reciben 404 en un recurso privado;
sin rol Student no pueden crear/listar tutorías (403). Un rol adicional no concede
acceso a conversaciones ajenas. No hay excepciones administrativas ni compartición.

Migración aditiva PrivateTutoring: tres tablas, índices únicos de creación por
propietario, turno por sesión/clave, mensaje por turno/rol y secuencia por sesión.
Estados/checks y borrado restrictivo; InitialIdentity permanece intacta. Solo
migración explícita, nunca al arranque web. Historial máximo 100 mensajes, solicitudes
de 2000 caracteres, respuestas visibles de 6000; tema 160/objetivo 500/foco 200.
Listado propio de 50 por página y máximo 100 sesiones por estudiante en el piloto.

## Política y orquestación

TutoringOrchestrator vive en Application, sin EF/SDK. Política immutable de código
tutoring-v1: diagnóstico breve o repaso directo, explicación corta, una consigna
principal por turno, esperar intento, pistas antes de solución, simplificación y
dificultad gradual sugerida. Nivel/repaso directo se fijan al crear la sesión;
no se cambian retroactivamente política/proveedor/modelo. Un cambio futuro requiere
versionar la política, no editar historiales. No hay UI de políticas institucionales.

SystemPolicy procede exclusivamente del servidor. Tema/objetivo/nivel/foco/acción
se serializan como datos user, separados del sistema; historial también no confiable.
No hay herramientas, archivos, acceso a credenciales o ejecución SQL para el modelo.
La aplicación controla permisos y estados, nunca obedece un cambio de permisos
propuesto por el modelo. La inyección aún puede afectar la calidad del texto:
no se promete que un LLM siempre respete instrucciones pedagógicas.

Contexto: metadatos persistidos más últimas intervenciones confirmadas y el intento
actual, máximo 11 mensajes en total/12.000 caracteres (ventana sin respuesta tutor
huérfana). No reenvía todo el historial ni resume automáticamente mediante otra IA.
El historial completo sigue disponible solo al propietario. El foco es descripción
orientativa, no dominio probado. Intentos fallidos/cancelados no se reenvían después
de resolverse; al recuperar se incluye el mismo intento una sola vez.

Cuando el adaptador declara JsonSchema, solicita objeto text/activity/focus con
subconjunto portable y strict cuando existe; valida formato, actividad permitida,
longitudes, modelo/proveedor. Sin soporte usa texto. Activity no valida aprendizaje:
Diagnosis/Explanation/Exercise/Feedback/Hint son orientaciones, nunca Validated.
Todos los mensajes muestran aviso formativo, sin nota ni evaluación objetiva.

Mock conserva sus 8 pruebas originales y solo añade casos tutoring_start/review/
message/hint/simplify. Son guiones deterministas de SQL explícitamente rotulados;
no comprenden temas arbitrarios ni validan intentos como un LLM. Proveedores REST
se reutilizan sin duplicar HTTP. No se implementa streaming.

## Cierre, representación y conservación

Completed registra fecha y resumen descriptivo local basado en tema/objetivo,
turnos completados, solicitudes de ayuda y último foco sugerido. No califica ni
certifica objetivos; no requiere cuota/disponibilidad del proveedor y no es el
módulo de reportes/evidencias de Iteración 4. La sesión cerrada puede leerse,
pero no reanudarse ni añadir mensajes. No existe envío de resumen a docentes.

Angular representa un subconjunto seguro: párrafos, código inline/fenced y listas,
siempre por interpolación de texto. No innerHTML, HTML arbitrario, enlaces activos,
bypassSecurityTrustHtml, ejecución de código o procesador matemático remoto.
Expresiones simples se leen como texto; fórmulas TeX avanzadas quedan pendientes.
[Angular recomienda su enlace de datos seguro](https://angular.dev/best-practices/security).

Conservación provisional: SOLO datos ficticios en BD local persistente hasta
limpieza explícita del piloto. Sin borrado automático, sin endpoint de eliminación,
sin exportación/compartición en 3. Respaldos también requieren acceso restringido.
Antes de usuarios reales: entidad responsable debe aprobar plazos, consentimiento,
rectificación/eliminación y políticas del proveedor. Esta decisión no autoriza datos
personales reales. La UI informa privacidad, contexto enviado a IA y carácter formativo.

La contraseña mínima de 5 se conserva por autorización previa, pero es un riesgo
pendiente y no se considera apropiada para un piloto con usuarios reales.

## Validación real y límites

El usuario informó diagnóstico Groq exitoso antes de 3: openai/gpt-oss-20b,
555 ms, 97 tokens entrada/77 salida. Se registra como evidencia aportada por el
usuario, no como prueba ejecutada por el agente. .env privado conserva Groq;
las pruebas y QA fuerzan Mock y retiran claves externas del proceso temporal.
No se ejecutan nuevas llamadas reales, gratuitas ni de pago, sin autorización.
La pedagogía real de Groq/Gemini/Ollama no se validó en 3: solo Mock y respuestas
controladas; el diagnóstico previo no demuestra calidad pedagógica de tutorías.
