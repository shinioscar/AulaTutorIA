# ADR-031 — Aprendizaje autónomo preparado

Fecha: 26/10/2026. Autorizado por encargo LEARN-1. Implementación y QA simulada
verificadas; resultados reales en [LEARN_1](../iterations/LEARN_1.md).
Amplía ADR-016–019/022/025/028/030; maestro intacto.

TutoringSession conserva Mode para Private/Personalized/AssignedClass. Los nuevos
LearningExperience (Tutoring/Diagnostic) y LearningModality (Conversational/Prepared)
son ejes explícitos e inmutables, con valores predeterminados compatibles para
históricos. Las clases asignadas conservan su contrato y contenido docente.
PreparedLearning pertenece 1:1 a una sesión Student privada: extensión, formato,
contenido íntegro validado, SHA-256, capacidad congelada, posición, evaluación e
identificador del conjunto de evidencias. No necesita Teacher ni publicación.

PrepareSession y FinalReport son operaciones independientes del motor de turnos.
Reutilizan GUID/hash, revisiones, leases, timeout/cancelación, máximo tres intentos
explícitos y persistencia antes/después de IA, sin transacción durante HTTP.
Start/Hint/Simplify/SubmitAttempt/NextActivity son locales, con las mismas
revisiones y evidencias; /cards rechaza las operaciones IA. GET no genera.
No hay segundo transporte ni motor académico, lotes, reparación o fallback.

El contrato prepared-learning-v1 reutiliza ExerciseProposal y PreloadedExercise;
no hereda los límites de importación LessonImportV1/V2 ni atribución docente.
Cada actividad tiene objetivo, explicación/ejemplo (solo tutoría), criterios y
solución privada. Toda respuesta pasa validación local, incluso sin Schema estricto.
Truncados, propiedades extra/duplicadas, referencias inválidas o campos incompletos
fallan; el guion solo se activa tras validar su totalidad y congelar su huella.

Tutoría acompaña y permite hasta diez intentos, ayudas progresivas y feedback local.
Cerrados usan los comparadores existentes; abiertos/código conservan NeedsReview,
sin ejecución. Diagnóstico preparado tiene preguntas fijas, un intento, sin ayudas
ni retroalimentación de corrección antes de cerrar; un error permite avanzar.
No se presenta como adaptativo. La validez estructural no acredita calidad curricular.

El cierre conserva resultados deterministas sin IA. FinalReport envía toda la
evidencia persistida y ayudas entregadas, sin chat, identidad, claves ni soluciones.
Si no cabe, falla antes de llamar; no se reducen denominadores ni se cortan respuestas.
prepared-evaluation-v1 devuelve reporte e interpretaciones por objetivo; para
diagnóstico incluye también AssessmentContent y LearningPlanPayload en la misma
respuesta. Referencias se contrastan con intentos/objetivos realmente enviados;
el plan conserva objetivos evaluados, evidencias y contrato de Iteración 4.

Aceptar un objetivo crea una tutoría privada separada y congela origen de plan,
objetivo y modalidad. No reabre el diagnóstico ni genera contenido automáticamente.
La aceptación, ejercicios propuestos y progreso trabajado reutilizan el plan
existente. Cada objetivo tiene una tutoría vinculada única; finalizarla con evidencia
registra trabajo, sin certificar dominio. Proveedor nuevo se resuelve explícitamente
para esa tutoría; el diagnóstico histórico mantiene su vínculo original.

BYOK conserva custodia, selección, privacidad y precio. Metadatos conocidos de
contexto/salida se conservan en catálogo; desconocidos bloquean modo preparado.
El vínculo original se usa en generación, evaluación y retries; cambiar selección
no lo retargetea. Revocación descarta resultados tardíos y conserva ejecución local.
Cada HTTP físico sigue requiriendo reserva PostgreSQL. Un objetivo de dos
invocaciones lógicas no garantiza dos HTTP ni cuota disponible del tercero.

Migración PreparedLearning aditiva; anteriores intactas. FKs/restricciones/triggers
protegen origen, snapshots y claves. Compartir sigue report-only-v1 explícito y
revocable; ni Admin ni Teacher heredan contenido privado. Solo datos ficticios.
