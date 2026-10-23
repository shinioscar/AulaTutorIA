# Demostración docente — datos ficticios

Preparar primero el [entorno aislado](README.md). Entrar como el profesor SQL del
escenario, nunca como una cuenta institucional. La biblioteca contiene tres
versiones publicadas, cada una asignada al grupo SQL Server del piloto.

## Crear y preparar

1. Mis clases → Crear clase manualmente. Guardar tema, asignatura y descripción.
2. Completar objetivos, secciones, actividades y rúbricas en Contenido. El guion
   de referencia está en [JSON V2](sql-procedures-v2.json); para cargarlo completo,
   usar el recorrido externo descrito abajo. No ejecutar SQL.
3. Guardar y recargar: comprobar que los textos se conservan. Simular si procede;
   la simulación no crea evidencia de alumnos.
4. Revisar → aprobar revisión → publicar versión → asignar versión concreta al
   grupo autorizado. Las acciones son separadas; editar después no modifica esa
   publicación ni las asignaciones previas.

En Crear con apoyo de IA se puede seleccionar Clase precargada, hasta cuatro
ejercicios y tres pistas. Generar con IA integrada utiliza **Mock durante esta
demo**. Aplicar su propuesta no la aprueba. El runner sustituye posteriormente
el contenido genérico por la referencia SQL revisada, mediante una edición del
borrador. No presentar el guion Mock como planificación SQL de calidad validada.

Para la vía externa: configurar el mismo formulario → Usar ChatGPT → copiar
prompt V2 → obtener JSON manualmente o usar el ejemplo ficticio → Validar y
revisar → Guardar borrador y abrir constructor. Revisar ayudas y referencias
privadas, simular, aprobar y publicar. La automatización usa el ejemplo local;
no abre una cuenta personal de ChatGPT ni le envía datos.

## Supervisar y ofrecer feedback

1. Supervisión docente → asignación SQL precargada. Comprobar tres alumnos
   elegibles: A/B finalizados y C sin iniciar. No interpretar falta de inicio
   como dificultad o desinterés.
2. Revisar progreso por objetivo: doce oportunidades obligatorias de la cohorte,
   ocho entregas A/B. Entrega no equivale a verificación o dominio.
3. Abrir B: ocho intentos, cuatro errores históricos, cinco pistas y una explicación
   alternativa. El guion conserva NeedsReview aunque se haya finalizado la clase.
4. Consultar las observaciones IA y sus identificadores de evidencia. Son Mock y
   preliminares; los resultados deterministas aparecen separados.
5. Registrar una decisión o comentario humano fundamentado. Referenciar evidencia
   vigente si se emite un resultado. El estudiante recibe ese texto; no es una
   nota interna ni una calificación oficial. La decisión no cambia sus intentos.
6. Revisar casos de atención; Code requiere juicio humano. La evaluación IA no
   cierra ni aprueba automáticamente ese caso.

El docente solo ve evidencia de su asignación y cohorte autorizadas. No puede
abrir conversaciones privadas de tutorías autónomas, grupos ajenos ni recursos
de otros docentes por compartir organización.

## Qué registrar manualmente

Anotar problemas observados de navegación, claridad y feedback utilizando la
[plantilla pedagógica](PEDAGOGICAL_VALIDATION.md). Dejar sin puntuación los aspectos
que no se hayan revisado. Los perfiles A/B son guiones automatizados ficticios;
no son entrevistas, resultados de aprendizaje ni comparación experimental.
