# LEARN-1 — Tutorías y diagnósticos preparados

Estado: implementación y QA simulada verificadas, 26/10/2026. Activación de la
base principal pendiente de autorización expresa. Encargo explícito.
Solo datos ficticios, Mock y HTTP simulado. Sin push ni inferencias externas.

## Auditoría y decisiones antes de programar

Se revisaron el maestro (alcance/arquitectura), índice, ADR-012/013/014/016–019,
022/025/028/030, contratos de tutoría/aprendizaje/IA-2/BYOK, guías UI-0 y UX-2,
TutoringService/Orchestrator, LearningWorkflow/Orchestrator, AssignedClassWorkflow,
PreloadedRules, LessonImportV1/V2, persistencia EF y pruebas PostgreSQL existentes.

Reutilización: TutoringSession/Turn, GUID/hash, revisión y lease cercado; ejercicios,
intentos y comparadores; PreloadedExercise/PreloadedRules para ayudas y feedback;
LearningReport, DiagnosticAssessment y PersonalizedLearningPlan; proyecciones seguras,
consentimiento report-only-v1, reservas PostgreSQL por HTTP y vínculo BYOK original.
No se crea una clase publicada, un propietario Teacher ni otro motor académico.

Persistencia propuesta: registro privado PreparedLearning ligado 1:1 a la sesión,
con extensión, contrato, guion completo validado y huella inmutables, límites de
capacidad congelados y posición controlada. Generación y evaluación son turnos
independientes que reutilizan la recuperación existente. Migración aditiva; ninguna
sesión anterior se convierte. Catálogo conserva capacidad conocida para decisiones
de tamaño; límites desconocidos bloquean generación preparada, sin inventarlos.

Tutoría: explica, ejemplifica y entrega ayudas progresivas locales; cerrados requieren
coincidencia para avanzar, abiertos/código quedan NeedsReview. Diagnóstico: preguntas
fijas, sin ayudas ni resultados inmediatos, un intento por pregunta; avanzar exige
entrega, aunque sea incorrecta. Ambos cierran y conservan reporte determinista sin IA.
La evaluación diagnóstica agrupa interpretación y propuesta de plan en una llamada.
Aceptar el plan y elegir modalidad son acciones explícitas; no se reabre el diagnóstico.

Límites independientes por operación y extensión; se rechazan antes de envío los
presupuestos incompatibles. Sin reparación, lotes, fallback ni retry ocultos.
Una invocación lógica válida inicial y otra final son objetivos verificables; los
intentos físicos se contabilizan aparte. Calidad curricular y dominio no se certifican.

## Línea base

Git en main con cambios previos de catálogo gratuito BYOK en 12 archivos y un informe
nuevo. Se preservan y no se atribuyen a LEARN-1. No había API/Angular escuchando en
4200/5080/7080; PostgreSQL disponible. Línea base real descubierta: 426 pruebas
(180 unitarias + 246 integración). Pasaron 410; fallaron 10 casos BYOK porque el
comando inicial inyectó AllowExternalCalls=false sobre un harness con transporte
simulado propio que no usa el modo PersonalQa, y 6 casos durante una recuperación
transitoria PostgreSQL. No se atribuyen a LEARN-1 ni se cuenta ese pase como válido.
Los resultados históricos BYOK no se cuentan como verificación actual.

## Implementación

[ADR-031](../decisions/ADR-031-aprendizaje-preparado.md) y
[contrato/guía](../architecture/PREPARED_LEARNING.md) describen tipos, límites y HTTP.
La migración PreparedLearning es la novena, aditiva; las ocho anteriores permanecen
intactas. Nuevas columnas conservan valores conversacionales por defecto. Se
protegen origen, contenido/hash, ejercicios y resultados preparados en PostgreSQL.

El Student elige experiencia y modalidad. Las tarjetas reutilizan SafeResponse,
ReportCard, LearningPanel, estilos de IA-2, tokens, tema e iconos locales. La
proyección entrega solo la actividad autorizada y ayudas efectivamente reveladas.
El diagnóstico oculta outcome/ValidationType/feedback de corrección hasta cerrar.
Los intentos primarios se conservan con su resultado real, sin ejecutar código.

Preparación y evaluación usan PreparedOrchestrator sobre IAiProvider existente.
La finalización usa el mismo lease cercado y revalida cuenta/stamp/conexión. Una
respuesta tardía, cancelada o revocada no instala un guion ni un reporte. La
evaluación conserva además su JSON y hash de evidencias. No se reescribe el reporte
local. El plan diagnóstico se valida y persiste atómicamente con la interpretación,
sin GeneratePlan adicional. Aceptar una meta abre una tutoría privada vinculada y
registra la aceptación en el plan; cerrar con evidencia registra trabajo del objetivo.

Los presupuestos del catálogo personal se conservan y verifican nuevamente antes
de enviar. Valores desconocidos bloquean preparación. Modelos institucionales no
revisados y Ollama con contexto de runtime desconocido no se habilitan por inferencia.
Las restricciones de catálogo gratuito BYOK previas se mantienen.

## Limitaciones pedagógicas y operativas

El diagnóstico es fijo, sin asistencia; no se afirma adaptación en tiempo real.
Coincidencia significa coincidencia con una regla generada, cuya corrección
curricular requiere contraste humano. NeedsReview permanece separado de las
interpretaciones IA. Mock prueba el flujo, no comprensión ni personalización real.
La validación local detecta estructura, referencias y revelaciones literales
obvias; no demuestra coherencia curricular ni ausencia de revelaciones semánticas.

No se trunca el conjunto final para lograr que quepa: un historial muy largo puede
conservar únicamente reporte local y recibir un conflicto antes de IA. No hay
garantía de tiempo, disponibilidad, cuota, completitud del proveedor ni facturación
exactly-once. Cada retry físico conserva admisión/cargo según ADR-025/030.
No hubo inferencias o autorizaciones externas reales, datos reales ni otra iteración.

## Verificación de la entrega

| Comprobación | Resultado actual |
|---|---|
| Release | Compilación final sin advertencias/errores. |
| Unitarias nuevas | 24 aprobadas, incluidas referencias inventadas/cruzadas y propiedades duplicadas. |
| PostgreSQL focal preparado | 13 aprobadas, sin omisiones: generación/local/final, privacidad, diagnóstico, replay, cancelación, plan e inmutabilidad. |
| BYOK preparado focal | 3 aprobadas: una reserva física inicial, cero locales, una final, vínculo conservado y revocación durante espera. |
| Angular/TypeScript | Check estricto y build producción finales aprobados. Inicial 300,77 kB; chat diferido 76,95 kB. |
| Temas | 80 comprobaciones de contraste y tokens aprobadas. |
| Maestro | SHA256 original comprobado. |
| Integral | Verify.ps1 -Smoke -Http aprobado: 466 .NET (204 unitarias + 262 PostgreSQL), cero omisiones/fallas; EF sin cambios pendientes, Angular/TypeScript, temas, Compose, diff y smoke/proxy. |
| HTTPS | 224 comprobaciones aprobadas; base aislada retirada al finalizar Verify. |
| Dependencias | .NET sin vulnerabilidades reportadas; npm producción cero. Persisten @angular/build moderado y undici alto en desarrollo, sin paquetes/locks nuevos. |
| Documentación | 130 enlaces locales comprobados, ninguno faltante. |
| Edge/Playwright final | 321 comprobaciones: 40 recorridos funcionales y 281 mediciones/capturas de viewport. Cero errores JavaScript. |
| Reinicio real | Cookie, historial, descifrado BYOK, guion/hash preparado, ayudas, reporte y replay conservados. Sesión preparada pausada reanudada; cero duplicados. |
| Logs y limpieza | Control BYOK de cuatro logs aprobado. Base/procesos HTTP QA y archivo privado .cache/http-qa.json retirados por el runner. |

El primer integral de esta implementación aprobó 200 unitarias y 260/262 de
integración. Se corrigieron dos regresiones concretas: conteo histórico de
migraciones (8→9) y conservación del contrato HTTP 200 del turno conversacional
fallido tras logout personal. El control adicional de stamp se limita a preparados;
en conversacionales se conserva el descarte existente por vínculo BYOK. El segundo
pase integral anterior incorpora esas correcciones, las cuatro pruebas nuevas y
las protecciones finales de snapshots/resultados.

## Métricas de inferencia comprobadas

| Recorrido preparado válido | Generación lógica | Actividades/ayudas/pausa/lectura | Evaluación lógica |
|---|---:|---:|---:|
| Tutoría | 1 | 0 | 1 |
| Diagnóstico, incluyendo propuesta de plan | 1 | 0 | 1 |

Las pruebas BYOK cuentan separadamente reservas de inferencia HTTP simulada:
1 generación, 0 locales y 1 evaluación por recorrido. Replays y lecturas conservan
el contador. Catálogo/OAuth son operaciones distintas. Los fallos y retries físicos
siguen cobrando reserva; estos resultados controlados no garantizan dos HTTP reales.

## Navegador, cobertura y revisión visual

Se conservaron los recorridos de Iteraciones 0–9, IA-1/IA-2, UX-2 y BYOK, y se
añadieron seis recorridos funcionales LEARN-1: selección de ambas modalidades,
diagnóstico conversacional existente, tutoría preparada con pérdida de respuesta,
diagnóstico sin resultados anticipados y análisis/plan conjuntos, privacidad
Teacher/Admin y generación/evaluación con OpenRouter simulado. Las pausas, ayudas,
lecturas, navegación y cierre no agregan reservas de inferencia.

El ensayo cubre 320/390/768/1440 px en Light/Dark, teclado, Escape, foco de regreso,
ausencia de desbordamiento y storage limitado al tema. Se generaron 49 capturas
LEARN-1 y se inspeccionaron [16 representativas](evidence-learn-1/README.md),
incluidos diagnóstico, plan móvil, reporte sin cuota y drawer. Se revisaron como
PNG reales; no se sustituyen por datos del DOM. No se declara certificación WCAG.

El primer E2E se detuvo en una aserción previa BYOK que buscaba un nombre ficticio
distinto al del transporte. Se corrigió para identificar el modelo por su value y
comprobar su etiqueta Gratis, conservando las pruebas de exclusión de pago.
El segundo completó BYOK y la tutoría preparada local, pero heredó la ventana HTTP
de solicitudes directas de BYOK. Se añadió una espera de la ventana, sin debilitar
políticas. Además se mejoró el mensaje Student cuando el limitador devuelve un
429 vacío y se añadió su aserción manteniendo cierre/reporte. Se repitieron
TypeScript, temas y build después de ese ajuste exclusivamente frontend.
El tercer pase aprobó las 321 comprobaciones, el reinicio y los logs; los intentos
interrumpidos no se cuentan como pases.

| Cobertura solicitada | Evidencia actual |
|---|---|
| Selección y conservación conversacional | learn-qa.mjs, TutoringTests y LearningTests existentes en el pase integral |
| Generación, JSON/truncado, límites y objetivos | PreparedLearningTests (24 casos nuevos), generación y rechazo antes de llamada |
| Persistencia/inmutabilidad/replay/recuperación | prepared-learning-tests.cs, triggers PostgreSQL, concurrencia, cancelación y reinicio real |
| Ejecución cero IA, ayudas, comparadores, abiertos y diagnóstico | Contadores 1/0/1, resultados diferidos y posición del servidor; recorridos A/B/F |
| Cierre sin proveedor, evaluación/plan y referencias | Fallos cuota/timeout/inválido, reporte local, hash de evidencias, IDs ajenos rechazados y propuesta en la misma respuesta |
| BYOK, cambio de selección y revocación | prepared-personal-ai-tests.cs, reservas físicas y suite PersonalAi/Admission/Transport existente |
| Dos estudiantes, privacidad y consentimiento | Sesiones/identidades independientes; nuevos 404 Teacher/Admin/Student, política report-only-v1 y suite de compartición vigente |
| Planes y clases docentes | Plan aceptado en tutoría separada; regresión completa AssignedClass/Preloaded/Supervision y recorridos previos conservados |

## Entrega y entorno

Maestro y .env intactos; proveedores, credenciales, cuentas y filas de la base
principal sin cambios. Se tomaron únicamente conteos privados de preservación.
La migración principal y el arranque de API/Angular con la nueva versión requieren
autorización, porque el encargo prohíbe modificar bases principales sin ella.
No se arrancó contra un esquema incompatible ni se migró automáticamente al iniciar.

Los cambios previos del catálogo BYOK se preservan separados del trabajo LEARN-1.
Sin push, nueva iteración, usuarios reales ni inferencias externas. El entorno de
QA ha sido retirado. El inicio local anterior estaba detenido en 4200/5080/7080;
la activación queda pendiente de la respuesta del usuario.
