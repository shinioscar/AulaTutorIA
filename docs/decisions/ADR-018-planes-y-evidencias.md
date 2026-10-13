# ADR-018 — Diagnóstico, planes personales y evidencias formativas

Fecha: 2026-10-13. Estado: implementado y verificado en Iteración 4.
Encargo explícito de Iteración 4: amplía el maestro con generación y ejecución de
planes personales. El maestro v1.0 permanece intacto. No autoriza Iteración 5.

## Decisión

Se reutilizan TutoringSession/Turn y la frontera de propiedad de ADR-016/017.
Las acciones DiagnosticQuestion, AnalyzeDiagnosis, GeneratePlan, ActivatePlan,
SubmitAttempt, NextActivity, GenerateExercise y FinalReport recorren la misma
idempotencia/revisión/lease cercado. Ninguna lectura, recarga, creación o reanudación
genera IA. FinalReport admite sesión cerrada; no reabre la conversación. Se conserva
el resumen determinista aun con error del proveedor. FinalReport no añade mensajes
al chat y funciona incluso al alcanzar su límite de historial. El límite de seis envíos por
minuto permanece: un recorrido largo puede requerir esperar antes de la siguiente acción.

LearningExercise identifica la consigna/version/reglas privadas; ExerciseAttempt
conserva respuesta, orden, ayudas y validación antes de esperar al proveedor. El chat
libre nunca produce automáticamente intentos. Las tutorías antiguas permanecen libres
y pueden incorporar nuevas preguntas/ejercicios explícitos. No se transforma el
historial conversacional anterior en evaluación estructurada retroactiva.

DiagnosticAssessment contiene revisiones de análisis; las preguntas abiertas quedan
NeedsReview, distinguidas de resultados Deterministic. Los límites configurables
Learning:MaxDiagnosticQuestions (1–8, predeterminado 5) y MinimumEvidence (1–máximo,
predeterminado 2) acotan el diagnóstico. El estudiante puede finalizar con menos
respuestas: se marca preliminar y no se aceptan fortalezas/dificultades categóricas.
La suficiencia cuenta consignas distintas, no repeticiones de una respuesta.

PersonalizedLearningPlan es independiente de LessonVersion. Objetivos/actividades
son valores inmutables validados en ContentJson (LearningPlanObjective/Activity),
con ejercicios normalizados e identificados aparte. Cada propuesta tiene 3–5 objetivos,
una actividad por objetivo, dificultad y minutos orientativos. Máximo tres propuestas;
ajustar requiere texto explícito y crea versión nueva. Iniciar congela la propuesta;
guarda aceptación y presenta la primera actividad en la misma sesión. NextActivity
exige intento correcto según regla o respuesta no verificable presentada; el alumno
decide continuar con esa limitación visible. Trabajado no significa dominado. Una
respuesta incorrecta determinista requiere otro intento antes de avanzar. Preguntas,
pistas y simplificación no avanzan automáticamente. Pausar/reanudar conserva posición.
Cerrar una sesión con ruta incompleta no certifica ni completa sus objetivos pendientes.

## Validación y proveedores

LearningOrchestrator en Application usa exclusivamente IAiProvider. Casos separados
para pregunta/análisis diagnóstico, propuesta, explicación guiada, orientación de
intento, ejercicio libre y reporte. Prompt learning-v1 del servidor; datos user no
conceden permisos. Salida Schema cuando disponible, JSON si soportado y texto con
objeto JSON solicitado en caso contrario, siempre validado localmente. No aceptar
Markdown externo, IDs inexistentes, campos extra, tamaños o estados incompatibles.
Una salida inválida queda como fallo recuperable, sin fallback a otro proveedor.

Contexto: hasta ocho intentos recientes, respuestas acotadas a 650 caracteres,
cuatro mensajes confirmados acotados a 500, metadatos/diagnóstico/ruta pertinentes;
28k caracteres máximo y 2048 tokens de salida. La DB conserva respuestas completas.
Los intentos largos pueden requerir revisión humana porque el contexto IA es acotado.
No se envían claves de ejercicios al modelo de conversación ni a Angular. Las reglas
propuestas por IA pueden ser pedagógicamente erróneas: coincidencia determinista
certifica solo esa regla, no su validez curricular ni dominio. No hay validador oficial.

Opción múltiple/verdadero-falso/alternativas cortas: comparador normalizado, trim y
comparación sin distinguir mayúsculas. No equivalencia semántica ni tolerancias
numéricas implícitas. OpenResponse/Code: NeedsReview con rúbrica, sin ejecución.
Ausencia de regla: NotEvaluable. Pistas contabilizadas solo cuando se entregan;
Simplificaciones se registran aparte de pistas y también clasifican un acierto como
con ayuda; cada intento congela ambas ayudas recibidas. Máximo diez intentos/ejercicio y diez ejercicios
libres. Una falla de orientación IA nunca altera el resultado determinista guardado.

Mock conserva sus contratos previos y solo devuelve guiones JSON rotulados en los
nuevos casos de texto. No simula comprensión de materias ni personalización semántica.
La elección pedagógica de objetivos con Groq/Gemini/Ollama requiere revisión manual
autorizada posterior; no se consumen cuotas durante implementación/verificaciones.

## Persistencia y recuperación

Migración aditiva LearningEvidence, después de PrivateTutoring; no modifica las dos
migraciones previas ni cuentas. FKs compuestas vinculan ejercicio/intento y
diagnóstico/plan/reporte/recomendación/permiso a una misma sesión; borrado restrictivo.
Únicos por intento/clave, ejercicio/número y recurso/versión. Contexto de generación
persistido antes de IA para reproducir un retry. Clave liga acción/texto/ejercicio.
Aplicación de propuestas ocurre dentro del mismo commit cercado que la respuesta.
No hay transacción abierta durante IA. Falla/cancelación conserva evidencia primaria;
replay confirmado no genera, pendiente devuelve 202, resultado tardío se descarta.
El actor activo Student se vuelve a comprobar antes de guardar cualquier salida.

Se mantienen límites/riesgos de ADR-017 (facturación no exactly-once, por proceso)
y política de contraseña ADR-015. Solo datos ficticios locales; retención,
consentimiento institucional y tratamiento de datos reales siguen pendientes.

Referencias técnicas: [concurrencia EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
y [CSRF ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
