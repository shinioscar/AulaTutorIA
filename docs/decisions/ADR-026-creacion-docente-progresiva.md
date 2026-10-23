# ADR-026 — Entradas docentes separadas, un solo constructor

Estado: aceptada en UI-1 por encargo explícito.

La biblioteca docente presenta tarjetas, estados y filtros de la página actual.
Dos rutas con el mismo guard Teacher separan la información básica manual del
formulario guiado IA. Ambas crean Lesson mediante el servicio existente y vuelven
al único editor; no hay backend, contratos ni migraciones paralelos.

La creación IA persiste el borrador con sus indicaciones antes de generar. Si la
confirmación HTTP falla se abre ese recurso para inspeccionar la solicitud durable,
sin reintento automático. Las propuestas no se aplican, aprueban o publican solas.
El editor conserva la conversación avanzada, con información/contenido/revisión
progresivos y rúbricas/soluciones privadas plegadas. Ningún dato académico se guarda
en almacenamiento del navegador ni se introduce en parámetros de URL.

La navegación global se conserva; no se decide sidebar/drawer. Se reutilizan tokens,
SVG locales y temas UI-0. No hay estadísticas globales sin contrato que las respalde.
