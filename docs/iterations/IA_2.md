# IA-2 — Clases precargadas, tarjetas y evaluación agrupada

Estado: **completada y verificada**, 2026-10-23. Base limpia main 4eda5eb, sin push.
Encargo explícito IA-2. No autoriza Iteración 9 ni publicación remota.

## Auditoría

AGENTS e índice leídos; contratos/ADR de clases, importación, asignaciones,
turnos, aprendizaje, supervisión, proveedores, cuotas y diseño consultados.
Se conserva la base histórica: 312 casos en Verify IA-1 y uno posterior,
313 distintos entre pases; 191 HTTP/34 E2E. No son resultados nuevos de IA-2.
Se revisaron LessonDraft/Revision/Proposal/Version, HintRules, importación V1,
constructor UI-1, AssignedClass, TutoringService/LearningWorkflow/orquestadores,
comparadores, auditoría y proyecciones de supervisión. API y Angular propios
identificados/detenidos antes de compilar; PostgreSQL principal conservado.

## Implementación

- Modalidad fijada en LessonContent.Preloaded dentro de LessonVersion inmutable;
  nuevas sesiones usan preloaded-class-v1. Contenido anterior conserva serialización,
  versiones, asignaciones y modo conversacional. Sin tablas ni migraciones nuevas.
- LessonImportV2 comparte LessonDesignPayload y esquema generado del tipo con
  prompt externo/generación integrada. V1 sigue conversacional. Constructor conserva
  edición, simulación, revisión, aprobación, publicación y asignación explícitas.
- Introducción, pistas progresivas, alternativas, feedback, errores frecuentes y
  sugerencias de práctica se almacenan por ejercicio. HintRules es independiente.
  Sugerencias de refuerzo son textos opcionales, no nuevas obligaciones académicas.
- PreloadedPlan intenta una GenerateAsync, hasta cuatro ejercicios/4096 tokens;
  valida límites, estructura y truncamiento. Sin reparación ni partes ocultas.
- Tarjetas reutilizan AssignedClass/intentos/turnos, autorizaciones y comparadores.
  /cards entrega ayudas bajo demanda y registra ejercicio/tipo/número/fecha; no IA,
  claves ni pistas futuras. Cinco tipos conservados; código/abiertos NeedsReview.
  Cierre, pausa, reanudación, concurrencia e idempotencia son del motor existente.
- FinalReport opcional tras Completed reúne todos los intentos y ayudas entregadas,
  objetivos/criterios, versión/revisión/hash; rechaza exceso sin recortar ni llamar.
  Sin nombre/correo/soluciones privadas. Resultado IA separado del reporte local,
  referencias verificadas, recuperación explícita con cuota/lease del motor.
  Supervisión muestra observaciones referenciadas y mantiene revisión humana aparte.

Guía, contratos y límites: [PRELOADED_CLASSES](../architecture/PRELOADED_CLASSES.md).
Decisión: [ADR-028](../decisions/ADR-028-clases-precargadas-y-evaluacion-agrupada.md).

## Verificación

Pase focalizado inicial: cinco casos unitarios y siete de integración aprobados.
Pase intermedio: 140 unitarias y 192 integración aprobadas, modelo EF consistente.
Después se reforzaron entradas inválidas, primera pista y contenido/hash de ayudas;
esos resultados intermedios no sustituyen el pase completo sobre código final.
El pase final .NET aprobó 335 casos (140 unitarios/195 integración, cero omitidos),
con PostgreSQL real aislado, compilación Release sin advertencias/errores y modelo
EF consistente. Conserva los 313 casos distintos anteriores y añade 22. Integración:
14 min 6 s. Auditoría NuGet sin vulnerabilidades reportadas. No hubo llamadas reales.

### Llamadas simuladas medidas

Contadores de IAiProvider.GenerateAsync, comprobados por aserciones automatizadas;
no representan medición de HTTP físico ni facturación de un proveedor real.

| Caso | Invocaciones |
|---|---:|
| Generación integrada completa precargada | 1 |
| Recorrido local, intentos, pistas, alternativas, navegación y cierre, incluso con proveedor fallando | 0 |
| Evaluación final agrupada válida | 1 |
| Recarga/replay o clave nueva con reporte vigente | 0 adicionales |
| Evidencia que excede presupuesto | 0 |
| Error de cuota y reintento explícito permitido, conservando contexto | 2 en total |

La evaluación de diez intentos conserva los diez; la de quince respuestas largas
rechaza el exceso antes de GenerateAsync y conserva los quince intentos y cierre.
Cuota, timeout y referencias inventadas conservan reporte local/NeedsReview.

Verify -Smoke -Http final aprobado: 335 .NET, 191 HTTP HTTPS, consistencia EF,
restore locked, Angular/TypeScript, 70 contrastes, Compose config y smoke/proxy Mock.
Npm reportó cero vulnerabilidades. Tras ajustar avisos de las tarjetas se repitieron
TypeScript/temas/build Angular de producción, todos aprobados. Logs privados:
artifacts/ia-2-verify-final.log e ia-2-angular-final.log; NuGet en
ia-2-dependency-audit.log. Contadores y aserciones en
[pruebas unitarias](../../tests/AulaTutor.UnitTests/preloaded-tests.cs) e
[integración](../../tests/AulaTutor.IntegrationTests/preloaded-tests.cs).

### Navegador, recuperación y QA visual

Test-E2E.ps1 aprobado con Edge/Playwright ya instalados, sin descargas. Conserva
las 34 comprobaciones anteriores y pasa 45 en total, cero errores JavaScript.
Incluye docente que genera/edita/simula/aprueba/publica/asigna contenido precargado;
validación HTTP del ejemplo V2; estudiante con pista progresiva y respuesta perdida
recuperada sin duplicar; pausa/recarga/reanudación/cierre con la ruta IA bloqueada;
evaluación agrupada, recarga sin regeneración y decisión humana independiente.
Los bloqueos 429 y la pérdida de respuesta son simulados explícitamente; no son
fallos reales de un proveedor externo. Integración prueba también cuota/timeout.

31 capturas y 31 mediciones de viewport, sin desbordamiento horizontal de página.
Tarjetas Light/Dark en 320/390/1440; teclado/foco, reduced-motion, almacenamiento
limitado al tema. Revisión de las 31 PNG en conjunto y cuatro pantallas nuevas a
tamaño original. Evidencia ficticia en [evidence-ia-2](evidence-ia-2/):
[320 Light](evidence-ia-2/cards-320-light.png),
[escritorio Dark](evidence-ia-2/cards-1440-dark.png),
[cierre sin cuota](evidence-ia-2/cards-completed-no-quota-dark-390.png) y
[reporte IA](evidence-ia-2/cards-evaluation-light-1440.png).
No es certificación WCAG ni validación pedagógica.

Reinicio real de API Mock aprobado: cookie e historial, intentos, turnos de ayudas
y evaluación, reportes y replay sin duplicados conservados. Artefactos privados:
ia-2-e2e.json, ia-2-e2e-suite.log, ia-2-restart.json e ia-2-call-metrics.json.

### Operación, seguridad y cierre

Cero bases QA test/http/restore y sin .cache/http-qa.json al terminar. PostgreSQL
principal con siete migraciones, sin cambios de datos durante QA. API y Angular
locales restaurados; GET salud mediante proxy confirma Groq configurado, sin
generación real. Evidencia privada: artifacts/ia-2-restored.json.

Revisión de publicación sobre archivos y todo el historial de texto alcanzable,
sin coincidencias con secretos configurados; .env, certificados, logs y estados
privados excluidos. Evidencia binaria revisada separadamente. Diff --check aprobado.
No se añadieron dependencias ni se alteraron credenciales o política de contraseñas.
Maestro SHA256 conservado:
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

Incidencias resueltas durante implementación: conflicto de nombre local y aserciones
de pruebas al compilar, comillas del template Angular, configuración JSON inválida
y ayudas nulas. Se reforzó detección de primera pista literal y se incluyeron ayudas
entregadas en el contexto/hash final antes del pase .NET definitivo. Los últimos
avisos de interfaz se comprobaron con nuevo build Angular y el E2E final.
Sin funcionalidad obligatoria pendiente del encargo. Cierre con commit local tras
revisión de publicación; sin push y sin avance a Iteración 9.

## Límites

La validación estructural no certifica corrección curricular ni equivalencia
pedagógica con el tutor conversacional. El guard de primera pista detecta claves
literales, no toda revelación semántica; revisión docente obligatoria de contenido
público antes de aprobar. Ninguna QA real de ChatGPT/Groq/Gemini; solo Mock o
transporte controlado. Una invocación lógica puede tener reintentos físicos sujetos
a admisión PostgreSQL y no garantiza facturación exactamente una vez.

Clases/evidencias que exceden el presupuesto necesitan reducir contenido o revisión
humana; no hay división automática ni flujo por partes. No se implementa modo híbrido,
diagnóstico precargado, notas oficiales, ejecución de código ni nuevos proveedores.
Riesgos institucionales previos, contraseña mínima cinco y retención permanecen;
no se declara preparado un piloto con datos reales. No push ni avance de iteración.
