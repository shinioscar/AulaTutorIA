# ADR-027 — Contrato externo versionado y borrador docente

Estado: implementado; verificación final en IA_1.md. Encargo IA-1.

UI-1 conserva un único formulario de opciones. «Generar con IA integrada» mantiene
IAiProvider; «Usar ChatGPT» genera texto determinista en Application, sin proveedor,
red externa, credenciales ni cambios globales. El enlace oficial abre una página
vacía: el profesor decide qué copiar. Puede usar otra herramienta compatible.

El sobre `LessonImportV1` contiene `format="aulatutor-lesson-v1"` y `lesson`, que
reutiliza **LessonDesignPayload** de ADR-022. No hay un segundo modelo académico.
El esquema se deriva del tipo y se incluye idéntico en el prompt y la validación
estricta. Se comparte la comprobación de forma del orquestador integrado sin
modificar su política ni su contrato. La versión se verifica además por valor.

El backend rechaza campos desconocidos/faltantes/duplicados/null, tipos erróneos,
profundidad >12 y entrada >32000 caracteres. Reconstruye LessonContent sin perder
soluciones/rúbricas/pistas y aplica ValidateDraft/Review: 12 objetivos/secciones,
24 ejercicios, ocho por sección y 20000 caracteres de contenido. Exige además
solución privada no vacía para importaciones. El texto hintRules representa las
indicaciones de pistas, no una nueva colección de mensajes. Orden por arrays;
referencias sectionId/objectiveId deben existir e IDs deben ser únicos.

No se eliminan Markdown ni campos; no se reparan ni normalizan contenidos
académicos silenciosamente. Las instrucciones de corrección contienen solo errores
seguros generados por el servidor, sin reflejar el JSON completo ni valores hostiles.
Errores de forma son genéricos; errores de actividad identificables indican número
o ID. La validación estructural no acredita calidad curricular ni ausencia de
instrucciones hostiles en el texto: el contenido sigue siendo datos no confiables.

Corrección posterior al piloto 9: los errores de dominio ya no se sustituyen por
una lista genérica de posibles causas. Los validadores compartidos emiten rutas,
posiciones y números seguros, sin reflejar valores de entrada. Un exceso de tamaño
indica longitud interna/máximo/diferencia, conservando el límite de 20000 y los
escapes originales. Los prompts V1/V2 comparten las restricciones por campo y
explican la diferencia entre entrada y contenido serializado. No cambia el contrato,
la política académica ni la validación estricta;
[QA de corrección](../iterations/QA_IMPORT_DIAGNOSTICS.md).

La vista previa no persiste. «Guardar borrador y abrir constructor» vuelve a validar
en el servidor y reutiliza creación serializada por docente/requestId, historial
inicial y cuota de clases. El actor se revalida después del advisory lock. Replays
iguales recuperan el recurso; misma clave con contenido distinto da 409. Se registra
LessonImported atómicamente con GUID de recurso, actor, fecha y correlación nueva
del servidor; sin título, JSON, soluciones ni datos académicos. Replay no lo duplica.
Publicaciones/asignaciones conservan la auditoría anterior.

Solo se importa como **clase nueva**, nunca sobre un borrador existente. No hay
endpoint que acepte lessonId/propietario/organización/grupo/estado. No se necesita
migración ni se altera ninguna versión, asignación o evidencia previa. La revisión
optimista del editor sigue rechazando guardados antiguos después de importar.

Teacher activo/aprobado del piloto, sesión HTTPS y CSRF en las tres rutas nuevas.
Student/Admin/Pending no pueden usarlas. El propietario es el actor actual; docentes
ajenos no reciben acceso por importar. El resumen reutiliza LessonSummary y
SafeResponse, con soluciones plegadas y acceso solo docente. Student conserva su
proyección sin claves. Simulación aislada y revisión/aprobación/publicación siguen
siendo acciones explícitas; no se cambia el motor del estudiante.

Archivo opcional .json: solo lectura en navegador, máximo 128 KiB y 32000 caracteres;
se envía el mismo texto al backend, no una ruta ni un upload persistente. Límite
HTTP global 1 MiB intacto. Nada académico en storage/URL/logs. Sin dependencias,
API OpenAI, automatización externa, reparaciones IA ni motor IA-2.
