# Iteración 9 — Piloto interno académico

Estado: **completada como piloto interno con datos ficticios**, 23 de octubre de
2026. Base main limpia f6d6532 (IA-2).
Encargo explícito de 9; no autoriza despliegue real, cuota IA real, push ni nuevas
integraciones. No se modifica el maestro ni políticas de contraseñas/credenciales.

## Auditoría y alcance

AGENTS e índice consultados; maestro §7/17/18 define clase SQL de referencia,
tutores ficticios, guías y medición. IA-1/IA-2 y contratos de importación, versiones,
asignación, turnos, aprendizaje, supervisión, cuota/recuperación revisados. Se
reutilizan fixtures PostgreSQL, Verify, HTTP HTTPS, Edge y el runner existente.
Git limpio, API/Angular propios identificados y detenidos antes de build; PostgreSQL
Compose activo conservado. Las 335 .NET/191 HTTP/45 E2E de IA-2 son línea base
histórica; se conservaron en la regresión completa de esta entrega.

Referencia y demo: [piloto](../pilot/README.md), [docente](../pilot/TEACHER_DEMO.md),
[estudiante](../pilot/STUDENT_DEMO.md), [matriz funcional](../pilot/FUNCTIONAL_MATRIX.md),
[usabilidad](../pilot/USABILITY.md), [plantilla pedagógica](../pilot/PEDAGOGICAL_VALIDATION.md),
[bloqueantes reales](../pilot/READINESS.md).

## Escenario e implementación de validación

Referencia SQL local V2: cuatro objetivos/ejercicios, ayudas y soluciones docentes
privadas; código guardado como texto. Tres recorridos usan el mismo ciclo académico.
La salida integrada Mock genérica se reemplaza mediante edición docente explícita
por la referencia SQL. No se afirma generación SQL semántica ni calidad de un LLM.

Integración: dos alumnos, misma versión precargada, 4/8 intentos, 0/4 errores,
1/5 pistas, 0/1 alternativas, un Code NeedsReview cada uno. Ambos finalizan con
proveedor indisponible y cero invocaciones ordinarias. Evaluaciones, retry y
revisión humana no alteran intentos. Clase conversacional SQL separada: dos alumnos,
errores/pista/refuerzo/pausa/reanudación/reportes/cierre y privacidad.

Navegador añade profesor nuevo aprobado, grupo con tres alumnos y tres versiones
publicadas/asignadas. A/B ejecutan el mismo núcleo SQL; C queda sin comenzar.
Test-E2E -KeepRunning prepara demo aislada reutilizable, nunca seed de principal;
Stop-PilotDemo valida propósito/base/PID/rutas y limpia solo ese entorno.

Único cambio de aplicación: texto de detalle Student distingue que las
actividades precargadas son locales. Las demás modificaciones son escenario,
instrumentación de pruebas, recuperación y documentación. Sin dependencias nuevas.

## Resultados verificados

Focalizadas: tres pruebas de piloto SQL y el caso existente de retry HTTP aprobaron
4/4, PostgreSQL real aislado. Incidencia de compilación por nulabilidad en aserción
nueva corregida antes del pase. Logs privados en artifacts/iteration-9-targeted.log.
E2E completo aprobado: 60 comprobaciones, conserva las 45 anteriores; cero errores
JavaScript. 42 capturas/mediciones sin desbordamiento horizontal de página, con
Light/Dark en 320/390/1440. Se revisaron las 42 PNG en conjunto y cuatro pantallas
SQL a tamaño original. No es auditoría WCAG ni estudio con usuarios.

Primer pase detectó una carrera del selector del test al pasar de lista a detalle.
Se añadió espera por URL y encabezado y se repitió el runner completo, aprobado.
Se precisó además H1 y se verificó ese selector en una navegación de C de solo
lectura, sin crearle sesión. No fue necesario modificar el router de la aplicación.

Reinicio real aprobado: cookie, historial y replay sin duplicados; dos sesiones
SQL completas conservan intentos, ayudas y reportes además de la sesión precargada
de regresión. KeepRunning dejó la demo disponible; Stop-PilotDemo pasó la limpieza
de sus procesos/base y archivos privados. Sin seed ni cambios de datos en principal.

La batería final `Verify.ps1 -Smoke -Http` aprobó sobre el código final, sin
verificaciones de compilación simultáneas. Los ajustes posteriores son documentales.

| Comprobación ejecutada | Resultado |
|---|---|
| Restore locked y .NET Release | Aprobados; cero advertencias/errores |
| Unitarias | 140/140, cero omitidas |
| Integración PostgreSQL | 198/198, cero omitidas; 14 min 34 s |
| Regresión .NET total | 338/338: conserva 335 anteriores y añade tres de piloto |
| Consistencia EF | Sin cambios pendientes del modelo; siete migraciones existentes |
| HTTP HTTPS con PostgreSQL | 191/191; entorno temporal eliminado al terminar |
| Angular/TypeScript | npm ci, check y build de producción aprobados |
| Temas | 70 comprobaciones de contraste/tokens y 42 capturas E2E |
| E2E | 60/60: conserva 45 anteriores; cero errores JavaScript |
| Recuperación | Reinicio real aprobado; dos sesiones SQL y regresión conservadas |
| Docker Compose | Configuración con perfil app válida; PostgreSQL real activo |
| Dependencias | npm: cero vulnerabilidades; NuGet: seis proyectos sin paquetes vulnerables en los orígenes consultados |
| Publicación | Revisión de secretos en archivos rastreados/historial y diff sin incidencias; .env ignorado |
| Maestro | SHA256 original conservado, sin modificación |

Fuentes locales: `artifacts/iteration-9-verify-final.log`,
`artifacts/http-identity/results.json` (191 entradas),
`artifacts/iteration-9-dependency-audit.log` y artefactos de métricas abajo.
No se reconstruyó una imagen Docker de aplicación ni se ejecutó una nueva
restauración de backup en 9; Compose config y PostgreSQL sí se comprobaron.
El respaldo durable continúa como bloqueante real, sin atribuirle el reinicio.

Limpieza comprobada: cero bases aulatutor_test/http/restore_GUID y ningún estado
privado de QA retenido. Principal conserva sus siete migraciones; no se crearon
cuentas/contenidos ficticios en ella. API Release y Angular HTTPS restaurados;
GET de salud a través de 4200 confirma Groq configurado, sin diagnóstico ni
generación real. Evidencia: `artifacts/iteration-9-restored.json`.

## Recorridos completos verificados

| Recorrido | Resultado técnico | Fuente |
|---|---|---|
| Docente manual | UI crea/edita/guarda; API carga referencia y publica/asigna | E2E 9 A + integración de tres vías |
| Docente integrado | Una propuesta Mock, aplicación, edición SQL explícita, simulación/publicación/asignación | E2E 9 A; sin afirmar calidad del guion generado |
| Docente externo | Prompt V2 e importación real del JSON local, revisión/publicación/asignación | E2E 9 A; ChatGPT personal no automatizado |
| Conversacional SQL | Dos alumnos, nueve intentos primarios, un refuerzo, pausa/reanudación, reportes y cierre | Integración AcademicSqlConversational; UI previa conservada |
| Precargada SQL | Misma versión, 4/8 intentos, perfiles distintos, progreso persistido y cero IA ordinaria | Integración AcademicSqlPilot y E2E 9 B/C |
| Evaluación | Referencias autorizadas, errores/retry, sin modificar evidencia ni NeedsReview | Integración y E2E 9 C/D |
| Supervisión | 3 elegibles, 2 finalizados, 1 sin abrir; 8/12 entregas; feedback independiente | E2E 9 D; integración usa cohorte distinta de 2 |
| Recuperación | Reinicio de API y respuesta perdida sin duplicación | Test-RestartRecovery y recorridos previos retenidos |

La rúbrica curricular permanece sin puntuar. No se realizaron llamadas a
Groq/ChatGPT reales, entrevistas ni despliegue institucional; no quedan autorizados.

## Metodología de métricas

Contadores IAiProvider.GenerateAsync separados de intentos de envío HTTP simulado.
Mock usa cero sockets de proveedor; HttpMessageHandler cuenta intentos de transporte
y reservas, no llamadas reales a Groq ni facturación. Error controlado de cuota y
reintento explícito preservan contexto y se contabilizan como otras invocaciones.

Lecturas de supervisión: una de calentamiento excluida y treinta medidas secuenciales
en TestServer HTTP del proceso de prueba, PostgreSQL real aislado, una versión/dos
alumnos. Percentiles empíricos nearest-rank, sin estimación de capacidad ni intervalos
de confianza; p95 con treinta observaciones tiene resolución de muestra limitada.
CPU/memoria corresponden al proceso de prueba/servidor, no al coste de una llamada
externa ni memoria de SQL Server. No extrapolar a grupos grandes.

E2E registra duración por recorrido y total del proceso; suite exterior incluye
preparación HTTP, arranque Angular y reinicio. Incluye esperas deliberadas de límites.
Métricas HTTP de helpers excluyen CSRF inicial y llamadas negativas fuera de ellos;
UI incluye clic hasta respuesta. Se agrupan por ruta sin IDs/query privados y solo
se calculan p50/p95 con n>=20; muestras menores conservan n/min/max.

### Navegador y recursos observados

Pase aprobado: 177411 ms de proceso Node/browser (177,41 s); suite exterior
467664 ms (467,66 s), incluyendo HTTP de preparación, Angular y reinicio/esperas.
Los 60 checks incluyen 42 capturas y 18 recorridos funcionales; no son 60 usuarios.
Helpers midieron 183 operaciones exitosas, cero errores en ese subconjunto. No
incluye todas las solicitudes del navegador ni los negativos fuera de helpers.
Los 429/503 y pérdidas de respuesta de los recorridos son fallos introducidos
explícitamente y verificados, no indisponibilidad real de Groq.

Ruta local /cards: n=34, p50=143,43 ms y p95=279,19 ms, 34 respuestas exitosas.
Combina operaciones locales del guion y tiempos UI/API descritos arriba, con
objetivos/ayudas distintos: no es una medición de una tarea uniforme ni de IA.
No se calculan percentiles para rutas con menos de veinte muestras.

Instantánea API tras reinicio y QA: 175955968 bytes de working set (167,80 MiB),
pico 185942016 bytes (177,33 MiB), CPU acumulada desde ese reinicio 10250 ms.
No representa consumo por solicitud ni CPU de PostgreSQL, navegador o proveedor.
La integración registra aparte memoria/CPU del proceso de prueba y lecturas n=30.

### Integración PostgreSQL y llamadas controladas

| Experimento independiente | Denominador y resultado |
|---|---|
| Tres vías de creación | 3 versiones publicadas/asignadas; 1 GenerateAsync de diseño y 0 de importación |
| Precargada SQL | 2 alumnos, 27 turnos locales (20 principales + 7 ayudas), más transiciones; 0 GenerateAsync durante todo el recorrido/cierre |
| Evaluaciones precargadas | 2 operaciones lógicas/2 reportes IA; 3 GenerateAsync: A=1, B=2 por fallo de cuota y retry explícito |
| Error controlado | 1 fallo de proveedor; otro retry prematuro rechazado antes de llamar; 0 duplicados de intentos/reportes |
| Conversacional SQL | 2 alumnos finalizados, 9 intentos primarios, 1 refuerzo generado; 21 GenerateAsync, sin HTTP externo |
| Retry de transporte | 1 SendAsync lógico, 2 intentos HttpMessageHandler, 2 reservas simuladas; 0 solicitudes de red |

Los experimentos usan fixtures/bases diferentes: no sumar sus invocaciones como
coste de una misma clase o comparar eficacia pedagógica. La regresión incluye
también la entrega de refuerzo y que nunca sustituya los obligatorios.

Lectura de supervisión individual: 30/30 respuestas HTTP TestServer exitosas,
0 errores, p50=42,23 ms y p95=49,05 ms; una lectura inicial excluida. Guion completo
de ese caso: 6498,67 ms, excluye preparación del fixture. CPU acumulada del proceso
durante el caso: 1359,38 ms. Working set/pico al muestrear: 1041006592 bytes
(992,78 MiB), del **proceso de pruebas ya calentado por la regresión completa**,
incluido servidor de pruebas, clientes y runtime. No es memoria atribuible a una
sesión ni a la API aislada de la instantánea anterior. No se midió consumo de un
modelo externo, primera respuesta/token ni capacidad máxima.

Artefactos privados reproducibles: iteration-9-e2e.json, e2e-suite.json,
e2e-suite-final.log, restart.json, navigation.json, api-resources.json,
academic.json, conversational.json, creation.json y http-retry.json, todos bajo
artifacts con prefijo iteration-9-. No contienen credenciales ni cuerpos en las
métricas de rutas. El estado de acceso de la demo solo existió en .cache privado.

Evidencia visual ficticia en [evidence-9](evidence-9/):
[SQL 320 Light](evidence-9/pilot-sql-320-light.png),
[SQL 1440 Dark](evidence-9/pilot-sql-1440-dark.png),
[resultado B](evidence-9/pilot-result-dark-390.png) y
[feedback docente](evidence-9/pilot-supervision-light-1440.png).

## Límites y decisión

La automatización verifica estructura, referencias, permisos, evidencia y recuperación.
No determina exactitud curricular integral, utilidad de pistas LLM, mejora educativa,
satisfacción o equivalencia de modelos. La plantilla real queda sin puntuar.
Groq/ChatGPT reales requieren autorización posterior explícita y procedimiento
aislado. MFA, cuentas, datos/consentimiento, retención/borrado, TLS/secretos, permisos
DB, respaldo durable y revisión institucional bloquean usuarios reales.
No alterar esas políticas para cerrar un ensayo ficticio.

Los ocho criterios de cierre del encargo quedan satisfechos para el piloto
interno ficticio: recorridos, compatibilidad, evidencias, supervisión, evaluación
preliminar, resultados reproducibles, requisitos reales y guías. No se requiere
un ADR nuevo: no cambian las decisiones académicas, permisos ni infraestructura.
Entrega mediante commit local de cierre; no se hace push ni se avanza a otra integración.
