# Plantilla de validación pedagógica — completar por una persona

Sin puntuaciones precargadas. Mock y JSON válido no permiten certificar calidad
curricular, utilidad de un modelo, mejora de aprendizaje o satisfacción.

Revisor y función: ______. Fecha UTC: ______. Asignatura/nivel: ______.
Proveedor/modelo/herramienta y fecha de versión conocida: ______.
LessonVersion y hash: ______. Modalidad: ______. PromptVersion: ______.
Evaluación/report ID y hash/revisión de evidencias: ______.
Autorización de prueba real y alcance: ______. Datos exclusivamente ficticios: ______.

Escala propuesta para revisión manual: 0 = incorrecto o perjudicial;
1 = necesita corrección importante; 2 = utilizable con ajustes;
3 = adecuado en el alcance revisado; NE = no evaluado. No sumar ni convertir esta
rúbrica en nota de alumno; justificar cada valoración con ejemplos identificables.

| Criterio | Comprobación manual | Valor 0–3/NE | Evidencia/defecto y corrección |
|---|---|---|---|
| Exactitud | Definición, parámetros, EXISTS y lotes coherentes con fuentes primarias | ___ | ___ |
| Secuencia | De reconocimiento a escritura; prerrequisitos y dificultad apropiados | ___ | ___ |
| Instrucciones | Se entiende qué entregar, límites y que SQL no se ejecuta | ___ | ___ |
| Pistas | Progresivas, pertinentes, sin solución directa en la primera | ___ | ___ |
| Ejercicios | Alternativas plausibles, claves correctas, abiertos con criterios claros | ___ | ___ |
| Feedback | Ayuda a revisar el razonamiento sin contradecir resultados guardados | ___ | ___ |
| Objetivos | Cada actividad permite observar el objetivo declarado | ___ | ___ |
| Reporte final | Afirmaciones sustentadas, límites claros y acciones de refuerzo concretas | ___ | ___ |

Contradicciones entre resultado determinista y comentario IA: ______.
Referencias existentes pero conclusión semánticamente injustificada: ______.
Soluciones filtradas en pistas/contenido público: ______.
Revisión humana necesaria en Code/NeedsReview: ______.
Problemas observados al usar teclado/móvil/tema: ______.
Decisión: requiere corregir / repetir muestra / apto solo para ensayo ficticio: ______.
Alcance no revisado y conflictos de interés: ______.

## Procedimiento posterior con proveedores reales — no ejecutado en 9

1. Solicitar autorización explícita nueva: proveedor/modelo, datos ficticios,
   operaciones previstas y máximo de solicitudes. Esta plantilla no la concede.
2. El operador prepara una base aislada nueva y configuración privada válida,
   conservando la principal y .env. Fijar proveedor antes de crear clases/sesiones;
   no convertir una sesión Mock cambiando su configuración. Verify y Test-E2E
   permanecen Mock: no tienen opción para eludir esta protección.
3. Comprobar cuota vigente de la cuenta y límites locales. Propuesta a autorizar:
   una generación, una evaluación final y, solo si hace falta, un diagnóstico Admin.
   Separar operaciones al menos 65 s, respetar Retry-After y parar al primer fallo.
   Un retry físico permitido por transporte también cuenta; no prometer tres HTTP
   exactos ni facturación exactamente una vez. No activar reintentos extra.
4. Revisar estructura y contenido antes de aplicar/aprobar/publicar. Guardar solo
   evidencia ficticia autorizada, metadatos seguros, duración y errores tipados;
   nunca claves, cookies o cabeceras privadas en reportes/capturas.
5. Completar esta rúbrica sobre consigna, claves privadas y resultados referenciados.
   Contrastar especialmente respuestas abiertas y SQL; no aceptar aprobación
   automática de NeedsReview. No ejecutar código de alumno en AulaTutor.
6. Para ChatGPT, el usuario copia el prompt y pega el JSON manualmente, sin
   automatizar su cuenta. Registrar herramienta/modelo declarado cuando se conozca;
   usar el mismo contrato/importador y revisión docente. No enviar datos reales.
7. Registrar denominadores y limitaciones. Una muestra pequeña no permite comparar
   modelos ni afirmar mejora educativa. Para personas reales son necesarios los
   requisitos e instancia de aprobación de [READINESS](READINESS.md).
8. Limpiar únicamente el entorno ficticio autorizado y restaurar configuración
   local. Conservar separado lo realmente ejecutado y lo que sigue pendiente.
