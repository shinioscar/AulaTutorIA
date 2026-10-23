# Importación externa de clases — v1

Ampliación compatible IA-2: [V2 y clases precargadas](PRELOADED_CLASSES.md).
V1 sigue aceptado y conversacional; no se convierte automáticamente.

## Guía breve para profesores

1. En Mis clases, abre Crear con apoyo de IA. Configura el tema y las opciones de
   siempre. Elige **Usar ChatGPT**.
2. Copia el prompt y abre ChatGPT. Pégalo manualmente allí (u otra herramienta
   compatible). No incluyas alumnos, conversaciones ni información sensible.
3. Copia la respuesta completa y pégala en **Importar clase**, o selecciona su archivo
   .json. Pulsa **Validar y revisar**.
4. Ante errores, copia las instrucciones de corrección para pedir otra respuesta;
   vuelve a pegarla. AulaTutor no llama a un proveedor para corregirla.
5. Revisa la vista previa y guarda el borrador. En el constructor puedes editar,
   guardar, simular, pasar a revisión, aprobar y publicar por separado. Después
   puedes asignar la versión publicada como cualquier otra clase.

La validez del formato no garantiza calidad pedagógica. Verifica especialmente
respuestas, criterios, contenido público sin soluciones y coherencia de las pistas.
Abrir ChatGPT no transmite datos. Sin acceso al portapapeles, selecciona el prompt
y usa la función de copiar de tu navegador. Conserva tu texto si falla la red;
reintentar el mismo guardado recupera el borrador sin crear otro.

## Contrato y límites

El contrato fuente es `LessonImportV1` en Application/Lessons/lesson-import.cs;
`LessonImport.Schema` se deriva del tipo real, sin un esquema manual divergente.
El sobre tiene exactamente `format` y `lesson`. lesson es el transporte existente
LessonDesignPayload: metadatos, explanation, objectives[], sections[] (body) y
exercises[] referenciados por sectionId/objectiveId. Ejemplo ficticio completo en
[lesson-import-v1.json](examples/lesson-import-v1.json).

No se admiten IDs administrativos ni estados. Los IDs internos son locales al
contenido, únicos de 1–40 caracteres ASCII alfanuméricos/guion/underscore. Orden por
array; al reconstruir, cada sección conserva los ejercicios que la referencian.
Máximo 12 objetivos/secciones, 24 ejercicios totales y ocho por sección. Entrada
32000 caracteres, profundidad 12; contenido reconstruido 20000 caracteres. Límites
de texto y ejercicio en [LESSONS_HTTP](LESSONS_HTTP.md). referenceSolution privada
debe estar presente y no vacía. HintRules admite hasta 600 caracteres; la cantidad
solicitada orienta este texto, sin garantizar un número de respuestas del tutor.

## HTTP

Las tres rutas exigen Teacher aprobado/activo, sesión HTTPS y CSRF:

| Ruta POST | Entrada | Resultado |
|---|---|---|
| /api/lessons/external-prompt | topic, subject, level, goals, exerciseCount, difficulty, activityTypes, hintCount, durationMinutes, notes, rules | 200 {prompt}, determinista y sin IA |
| /api/lessons/import/preview | {json:string} | 200 {content?,errors,warnings,correctionInstructions}; ningún guardado |
| /api/lessons/import | {requestId:GUID,json:string} | 201 LessonView en Draft; validación repetida, creación idempotente |

Preview estructural inválida devuelve content=null con errores. Import inválida
400; sin sesión 401; rol insuficiente 403; clave usada con otro contenido/límite de
clases 409. El límite HTTP de 1 MiB se conserva. No hay importación sobre una clase
existente. Tras guardar, el editor usa revisión actual y los controles habituales.

No se envía ni conserva el JSON original como documento externo: se guarda el
contenido académico validado en revisiones normales. No se registran cuerpos en
logs. El evento LessonImported no contiene datos académicos.
