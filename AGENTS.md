# AulaTutor IA — contexto para agentes

## Fuente y alcance autorizado

- Trabajar solo en F:\AulaTutorIA. Documento maestro v1.0 en raíz: leer completo,
  nunca modificarlo. SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
- Último encargo: **Iteración 5**, constructor docente, simulación y publicación;
  UI-0 conserva guía visual oficial, tokens y temas Light/Dark,
  con Groq activo local y Mock en pruebas. No ejecutar cuota real sin autorización.
  Iteraciones 0–5 y UI-0 completadas. No avanzar a 6 sin nuevo encargo.
- Registros: docs/iterations/ITERATION_0.md a ITERATION_5.md; ADR en docs/.
  Mantener README, AGENTS, ADR, ambigüedades y verificaciones actualizados.
- En 4 se autorizan compartición mínima de reportes y validadores formativos cerrados,
  sin clases docentes, asignaciones, notas oficiales ni ejecución SQL/código de alumnos.
  Mock, salud y estructura de proveedores permanecen funcionales.

## Arquitectura y convenciones

- Monolito modular: Domain sin frameworks; Application referencia Domain y expone
  DTO/interfaces/reglas de acceso; Infrastructure implementa Identity, EF/Npgsql,
  bootstrap, servicios y Mock; Api compone/autoriza/valida; Angular usa HTTP relativo.
- SDK .NET 10.0.302 / net10.0; EF Core e Identity 10.0.10, Npgsql EF 10.0.3.
  dotnet-ef 10.0.10 local en dotnet-tools.json. NuGet locks y package-lock versionados.
- Angular standalone 21.2.25 con router/forms, TS estricto, signals y HttpClient.
  Texto/documentación español; tipos/código inglés, nombres de archivos kebab-case.
- C# PascalCase, namespaces AulaTutor.*; endpoints finos, lógica en servicios.
  DTO distintos de entidades de persistencia. UTF-8/LF, PowerShell CRLF.
- No nuevos paquetes sin necesidad de la iteración; sin SDK IA, Swagger UI, Redis,
  microservicios, colas o runner E2E prematuros.
- IAiProvider/DTO en Application/Ai; Mock y adaptadores HTTP en Infrastructure/Ai.
  Mock conserva texto sin red/estado/uso ficticio; configuración explícita válida
  habilita Groq/Gemini/Ollama, nunca fallback silencioso. No SDK ni paquetes nuevos.

## Sistema visual y temas (UI-0 / ADR-020)

- Guías obligatorias para nuevas interfaces: docs/design/DESIGN_SYSTEM.md,
  THEME_TOKENS.md y COMPONENT_GUIDELINES.md. Reutilizar tokens semánticos de
  src/AulaTutor.Web/src/styles/theme-tokens.css; no colores literales en componentes.
- Light predeterminado; Dark explícito, data-theme en html, ThemeService/ThemeToggle
  compartidos. Solo aulatutor.theme.v1 (light/dark) puede guardarse en localStorage;
  excepción visual, nunca credenciales, sesión, roles o contenido académico.
  Preferencia por navegador/origen, independiente de login/rol. Head aplica antes
  del render; storage bloqueado no impide usar el tema. System queda futuro.
- UI-0 adapta únicamente Inicio y estilos compartidos. Layouts de tutorías/grupos/
  usuarios/diagnóstico se conservan con colores tokenizados. Dashboards completos,
  sidebar/drawer y nuevos módulos requieren otro encargo. Referencias en docs son
  inspiración, sin estadísticas, costos, notas o disponibilidad ficticios.
- Fuentes/iconos locales de sistema/SVG; sin dependencias visuales o fuentes remotas.
  Controles 44 px, foco visible, teclado, reduced-motion y contraste en contexto.
  npm run check:theme verifica 70 pares y centralización; integrado en Verify.ps1.
  Capturas/QA en docs/design/evidence-ui-0 y QA_UI_0.md. No certifica auditoría WCAG.

## Identidad y autorización (ADR-012/013)

- ASP.NET Core Identity: roles Admin/Teacher/Student, contraseñas solo Identity.
  Student público; Teacher público Pending sin roles hasta aprobación Admin.
- ADR-015: contraseñas nuevas de 5–128 caracteres para todos los roles; no exigir
  mayúscula/minúscula/dígito/símbolo ni más de 1 carácter distinto. Longitud/variedad
  recomendadas. No cambiar hashes/cuentas existentes; bloqueo y CSRF conservados.
  Riesgo pendiente: mínimo de 5 no apropiado para piloto con usuarios reales; no cambiar
  política sin nueva autorización, como pide el encargo de 3.
- GET /api/admin/users?email= filtra correo parcial normalizado ANTES de paginar,
  máximo 254 caracteres y 50 resultados/página. Admin activo de organización piloto;
  selector de grupo conserva filtro al paginar y reinicia selección al buscar.
- Cookies __Host-AulaTutor.Session: Secure siempre, HttpOnly, SameSite Strict,
  path /, 2 horas sin sliding. Sin JWT ni sesiones en localStorage.
- HTTPS local: Angular https://localhost:4200, API https://localhost:7080;
  proxy /api/** de mismo origen. HTTP 5080 conserva salud/OpenAPI de desarrollo.
- CSRF backend para POST/PUT/DELETE, también login/registro. GET /api/auth/csrf
  emite cookie antiforgery HttpOnly y XSRF-TOKEN legible (no autentica). Angular
  X-XSRF-TOKEN relativo y renovación tras login/logout. No CORS abierto.
- 5 fallos: bloqueo 15 min; 30 login/registro por IP/min/proceso. Registro duplicado
  válido recibe mismo 202; credenciales desconocidas/incorrectas/bloqueadas: mismo 401.
- Roles/estado/aprobación/logout actualizan security stamp; revocación inmediata,
  logout de todas las sesiones de la cuenta. Servicios verifican actor actual en BD,
  organización y relación; no confiar en userId de navegador para identificar actor.
- Admin gestiona cuentas, aprobación y ciclo/miembros de grupos. Teacher aprobado:
  grupos asignados y roster mínimo. Student: solo grupos propios, sin roster.
  Perfil /api/users/{id}: propio o Admin, sin permiso implícito docente. 404 oculta
  recursos ajenos, 401 sin sesión, 403 sin rol, 409 conflicto, 400 CSRF/datos.
- Las conversaciones privadas solo pertenecen al estudiante propietario.
  Ser Admin/Teacher o compartir organización/grupo NO concede acceso a tutorías.
- Una organización piloto fija; FKs compuestas de membresías evitan cruces entre
  organizaciones. No crear multitenancy completo. Archivar grupos no elimina datos.
- Bootstrap explícito (--bootstrap-admin o script interactivo), sin credenciales
  predeterminadas ni seed de usuarios. No promover registro existente ni resetear
  contraseña al repetir; primer admin serializado por advisory lock/transacción.
  Se protege último Admin y retirada del propio acceso. No habilitar bootstrap HTTP.
- Migración InitialIdentity con organización/roles, sin usuarios. --migrate explícito;
  no migrar/crear cuentas al arranque web. Data Protection persistente, DPAPI Windows,
  volumen privado UID 1654/permisos 700 en Docker. Producción Linux requiere cifrado definido.

## Proveedores IA (ADR-014)

- Mock predeterminado; Groq/Gemini requieren modelo y API key privados, Ollama
  modelo explícito/URL local permitida. OpenAI reservado y rechazado en 2. Los
  modelos openai/gpt-oss de Groq/Ollama NO conectan con OpenAI.
- IHttpClientFactory sin logging automático/cookies/redirecciones. Hosts oficiales
  fijos Groq/Gemini; Ollama solo localhost/127.0.0.1/::1/host.docker.internal sin
  ruta/query/userinfo. Nada de URLs ni claves desde Angular. No descargar modelos.
- Ai__Provider, Ai__Groq__ApiKey/Model, Ai__Gemini__ApiKey/Model,
  Ai__Ollama__BaseUrl/Model; mapeos .env en loader y Compose. Loader no sobreescribe
  variables existentes: terminal nueva tras cambiar .env. Docker Ollama requiere
  host.docker.internal; localhost apunta al contenedor, no a Windows.
- Catálogo conservador/fechado en ADR-014: Groq gpt-oss 20b/120b y qwen 3.8 strict;
  Gemini 3.8 y 2.5 JSON Schema sin prometer strict; Ollama gpt-oss local JSON/Schema.
  Modelos desconocidos Gemini/Ollama solo texto; ampliar catálogo con fuente oficial.
- Contrato text/json/json-schema (objeto raíz); subconjunto schema explícito,
  keywords desconocidas rechazadas, salida validada localmente. Sin schema remoto
  ni eliminación automática de Markdown. StreamingImplemented=false, chunk futuro.
- Timeout 30 s/concurrencia 2/reintentos 0 por proceso; máximos 120 s/8/1. Reintentar solo
  429/503 si Retry-After cabe en espera máxima 5 s (1–10), sin anticiparlo ni fallback.
  CancellationToken distingue cancelación externa y timeout; errores tipados seguros.
- GET /api/admin/ai/ sin red; POST /test Admin activo+CSRF, prompt fijo/128 tokens,
  3 intentos/min/cuenta/proceso (incluye CSRF fallido). Telemetría metadatos, nunca
  prompt/respuesta/keys. Rechazar respuesta que reproduzca clave configurada.
- No llamadas reales durante Verify/QA: Mock o HTTP loopback simulado. Prueba real
  opcional solo acción explícita Admin con proveedor configurado. No se ejecutó en 2.
- El usuario informó diagnóstico real Groq antes de 3: openai/gpt-oss-20b,
  555 ms/97 tokens entrada/77 salida. No atribuirlo al agente ni a tutoría real.
  Conservar AI_PROVIDER=Groq local y .env privado; NO hacer más llamadas reales
  sin autorización explícita. CI/TestHost/HTTP/browser QA fuerzan Mock y vacían
  keys externas del proceso temporal. .env nunca se sobreescribe para pruebas.

## Tutorías privadas (ADR-016/017)

- Domain: TutoringSession/Message/Turn, enums operativos. Application:
  TutoringOrchestrator, política tutoring-v1, DTO/ITutoringService. Infrastructure:
  persistencia, autorización, idempotencia y lease; API fina; Angular Student.
- Solo propietario Student activo de organización piloto. Teacher/Admin/Student
  ajeno: 404 aunque compartan grupo. Crear/listar sin Student: 403. Actor viene de
  Identity, nunca studentId enviado. Sin excepción admin ni compartición.
- Migración aditiva PrivateTutoring después de InitialIdentity. FKs restrictivas,
  propietario/organización y únicos en creación/turno/rol/secuencia. No EnsureCreated.
- Created→Active→Paused→Active o Completed terminal. Crear/leer/refrescar/reanudar
  NO genera IA. Start/Message/Hint/Simplify son turnos explícitos. Pausa/cierre
  requieren resolver/cancelar generación vigente; cierre funciona con falla IA.
- GUID requestId idempotente ligado a acción/texto; revisión de servidor. Lock
  corto de fila propia antes/después de generación, sin transacción prolongada.
  Replays completados no llaman IA; pendientes: 202, otra clave/revisión vieja 409.
- Lease de timeout+30 s, token cercado: descartar resultado tardío tras cancelar,
  recuperar o vencer. Cancelación HTTP/timeout conservan intento como fallo;
  lease vencido visible Interrupted. Retry explícito del mismo requestId/acción/texto,
  máximo 3 ejecuciones y RetryNotBefore. No reintentar al GET ni automáticamente.
- Revalidar Student/activo en BD antes de persistir respuesta: revocación descarta
  resultado. GET usa snapshot corto RepeatableRead. Cancel de MISMA clave admite
  revisión anterior por carrera con HTTPabort, sin cancelar otro turno.
- Orquestador sin secretos/herramientas/SQL. SystemPolicy solo servidor; metadatos
  e historial son datos user no confiables. Ventana hasta 11 mensajes en total/12k caracteres;
  conserva todo el historial en DB. Foco/activity sugeridos NO validan dominio.
- JSON Schema cuando hay capacidad, texto para Mock. Modelo/proveedor se fijan al
  crear; cambio de configuración: 409 antes de llamada, sin fallback. tutor texto de 6000,
  entrada de 2000, tema 160/objetivo 500/foco 200, 100 mensajes y 100 sesiones/alumno.
  Rate limit de 6 envíos/min/cuenta/proceso; presupuesto/auditoría completa siguen para 8.
- Mock conserva comportamiento/8 pruebas previas; añade guiones SQL explícitamente
  simulados de diagnóstico/repaso/ejercicio/pista/simplificación. No comprensión LLM.
- Resumen descriptivo local al cierre: fechas/tema/objetivo/ayudas/turnos, sin IA,
  sin calificar ni reportar a Teacher. El reporte IA de 4 es una acción separada.
- Angular renderiza párrafos/código/listas por interpolación segura. No innerHTML,
  HTML arbitrario, enlaces activos ni renderizador remoto. Sin local/sessionStorage
  para conversación/credenciales. Poll GET cada 2 s mientras genera; salir aborta POST.
- Retención provisional: SOLO ficticios en DB local hasta limpieza explícita,
  sin autoexpiración/borrado; reportes compartidos solo conforme ADR-019. Antes de reales aprobar políticas de
  retención, consentimiento, proveedor y solicitudes de eliminación.

## Aprendizaje formativo (ADR-018/019)

- LearningExercise/ExerciseAttempt identifican consigna, respuesta y ayudas; los mensajes
  libres no son intentos. Respuesta guardada antes de orientación IA. Cerrados: comparador
  determinista de reglas privadas, nunca claves en Angular ni chat contextual. Abiertos/Code
  NeedsReview, sin ejecutar código/SQL. Coincidir con una regla no certifica validez curricular.
- DiagnosticAssessment versionado: mínimo de evidencias distintas configurable; preliminar
  si insuficiente, sin fortalezas/dificultades categóricas. Learning:MaxDiagnosticQuestions
  1–8 (5), MinimumEvidence 1–máximo (2). Finalización temprana explícita permitida.
- PersonalizedLearningPlan independiente de LessonVersion: 3–5 objetivos con una actividad
  por objetivo; máximo tres propuestas ajustadas explícitamente. Aceptación inicia dentro
  de la misma sesión. Servidor exige intento antes de avanzar; preguntas libres no cambian
  posición. Trabajado no significa dominado. Pausa/reanudación conserva ruta.
- Nuevas acciones en /messages usan el mismo lock/lease/revisión/retry de 3. ContextJson
  durable privado para retries, ExerciseId y RetryText en TurnView. Hasta ocho evidencias,
  cuatro mensajes cortos, contexto 28k y salida 2048 tokens. Prompt learning-v1 y JSON
  validado incluso con proveedores de solo texto; fallo seguro sin fallback externo.
- LearningReport conserva estadísticas PostgreSQL y observaciones IA trazables; máximo
  diez revisiones. EvidenceRevision marca desactualizados; sin regeneración al GET ni
  con clave nueva sobre reporte vigente. FinalReport explícito admite Completed sin reabrir.
  Resumen local conservado. Correctos cuentan ejercicios distintos, errores cuentan intentos.
- LearningRecommendation crea refuerzo idempotente con SourceRecommendationId/SourceReportId,
  solo tema/meta/nivel, sin mezclar historiales. Crear refuerzo no llama IA.
- ReportShare solo versión revisada/autorizada expresamente: Teacher activo/aprobado,
  mismo grupo vigente Student/Teacher, verificado en cada consulta. Revocación o retirada
  de relación impide nuevas consultas; no elimina copias previas. Teacher/Admin no leen
  chat/intentos/diagnóstico/plan ajenos. Vista docente mínima de reportes, sin supervisión.
- Migración aditiva LearningEvidence después de PrivateTutoring. Las dos previas intactas.
  Retención real, consentimiento institucional, borrado/exportación y auditoría siguen pendientes.
- El usuario informó una tutoría Groq real satisfactoria antes de 4. El agente no repite
  llamadas reales. Mock nuevo usa guiones genéricos rotulados, sin personalización semántica.

## Clases docentes (Iteración 5 / ADR-021)

- Lesson propia Teacher activo/aprobado del piloto; Admin/Student/Pending no crean,
  otros actores reciben 404 en recursos. /api/lessons exige sesión y CSRF en escrituras.
- LessonDraft editable y LessonDraftRevision inmutable con contenido completo JSON.
  Objective/Section/Exercise/Rubric son valores validados; ExerciseProposal y comparador
  de 4 se reutilizan. IDs únicos y referencias a objetivos verificadas; orden por arrays.
- Draft→Simulation→InReview→Approved→Published→Archived; editar/aplicar vuelve a Draft.
  Aprobación exige objetivos/contenidos/actividades/criterios/pistas. Simulación completa
  no obligatoria. OpenResponse/Code requieren revisión, sin ejecutar SQL ni código.
- Approved crea LessonVersion inmutable (número, revisión, snapshot, actor/fecha/hash).
  Publish explícito crea LessonPublication separada; archive cambia disponibilidad.
  Nuevo borrador conserva versiones previas. Triggers PostgreSQL impiden reescribir
  versiones/revisiones/origen de simulación. Sin endpoints de edición de versiones.
- LessonOrchestrator/IAiProvider: Plan/Content/Exercises/Rubrics, lesson-design-v1,
  objeto completo validado en todos los proveedores. LessonProposal no cambia draft;
  apply/discard explícitos. DraftRevision antigua impide aplicar propuesta tardía.
- LessonOperation: contexto durable privado, requestId/hash, locks cortos de Lesson,
  lease timeout+30s/token cercado, máximo tres ejecuciones y RetryNotBefore. GET no IA;
  pending 202; cancel/stop descarta resultados tardíos; revalidar Teacher vigente en BD.
  Proveedor/modelo fijados por operación/simulación, cambio 409 sin fallback externo.
- LessonSimulation ligada a copia/revisión concreta, Active/Stopped y reapertura sin IA.
  Reutiliza TutoringOrchestrator/SystemPolicy; proyección temporal de tutoría en memoria,
  nunca filas de TutoringSession/ExerciseAttempt/LearningReport ni miembros/asignaciones.
  Answer se guarda/valida antes de IA; dudas/ayudas no avanzan; Next exige respuesta en
  obligatorio. Ensayo permite avanzar tras respuesta incorrecta, no certifica al docente.
- El constructor propietario recibe soluciones privadas; vista de simulación y modelo
  conversacional excluyen claves y referencias. No endpoints estudiantiles de clases.
- Límites: 100 clases/docente, 12 objetivos/secciones, 24 ejercicios, plan 20k caracteres,
  contexto 28k, 30 propuestas/20 versiones/20 simulaciones por clase, 50 turnos/10 respuestas
  por actividad/300 decisiones, seis solicitudes IA por minuto/docente/proceso.
- TeacherLessons aditiva después de las tres anteriores intactas. Retención ficticia
  local hasta limpieza expresa; futura eliminación requiere decisión sobre triggers.
  Contratos en docs/architecture/LESSONS_HTTP.md; QA_5.md y evidencia de 5.
  Groq local intacto; pruebas Mock/controladas, cero nuevas llamadas reales.

## Operación y pruebas

- Inicializar herramientas: . ./scripts/Initialize-Environment.ps1.
  Exportar certificado: ./scripts/Prepare-Local.ps1 (solo .cache/certs).
  Configurar entorno privado: . ./scripts/Load-LocalConfiguration.ps1.
- .env lo lee Compose; .NET lo carga únicamente mediante el script local explícito.
  No imprimir connection strings, cookies, contraseñas, .env ni archivos de QA privados.
- PostgreSQL localhost:15432 (interno 5432) en proyecto Compose aulatutor-ia.
  API/web pueden seguir activos al entregar: inspeccionar puertos/procesos y detener
  SOLO los del proyecto antes de build/smoke (Windows bloquea DLL en uso).
- CI local completo: ./scripts/Verify.ps1 -Smoke -Http. Restore locked, build, 155
  pruebas (81 unitarias/74 integración; conserva 123 previas), consistencia EF,
  npm ci/check/check:theme/build, Compose config, diff, smoke Mock y HTTP identidad/tutoría.
  UI-0 repitió 123 pruebas/75 HTTP y añadió 70 comprobaciones de contraste y
  19 capturas por rol/viewport; QA_UI_0.md registra teclado/storage/responsive.
  Iteración 4: 123 pruebas y 75 comprobaciones HTTP aprobadas; cierre visual en QA_4.md.
  Iteración 5: 155 pruebas/107 HTTP/70 contraste, 26 capturas en evidence-5 y QA_5.md;
  TeacherLessons aplicada localmente, Docker API compilada y QA temporal eliminado.
  Histórico:
  91 pruebas y 48 comprobaciones HTTP de 3; revisión visual
  desktop/móvil registrada en QA_3.md. PrivateTutoring/LearningEvidence aplicadas en BD local;
  entorno de QA eliminado y API local restaurada con Groq sin llamadas reales.
- Integración exige AULATUTOR_TEST_CONNECTION (el loader lo asigna si falta) y
  PostgreSQL real con CREATE DATABASE. Bases aulatutor_test_<GUID> / aulatutor_http_<GUID>
  aisladas y eliminadas con validación de nombre. Nunca usar EnsureCreated/InMemory
  para reemplazar comprobaciones de migraciones o saltarlas silenciosamente.
- HTTP QA -KeepRunning solo para revisión: .cache/http-qa.json es privado, contiene
  contraseña ficticia aleatoria/PID/base temporal. Limpiar proceso/base/archivo al
  terminar; no mantener el administrador de QA en la BD principal.
- Pruebas con @example.invalid, claves aleatorias ficticias. Capturas versionadas
  en docs/iterations/evidence-1/2/3/4, sin secretos. Caches/logs ignorados.
- /api/health conserva iteration=1 por contrato heredado; liveness/configuración IA,
  sin red ni readiness PostgreSQL. OpenAPI solo Development, cookie/CSRF y rutas 1–4.
  Los endpoints nuevos deben exigir autorización; nunca diagnóstico IA público.
- Git main, sin remoto. Commit solo al pasar verificaciones; no push sin autorización.

## Pendientes y continuidad

- Iteración 6: asignaciones de LessonVersion a grupos; requiere encargo nuevo. Preservar
  tutoría privada y planes personales como concepto diferente. No continuar automáticamente.
- Pendientes documentados: confirmación email, recuperación/MFA, invitaciones,
  auditoría integral (8), retención institucional y borrado. Frontera Approved/Published
  resuelta en ADR-021: inmutable al aprobar, publicación explícita independiente.
- Antes de producción/datos reales: aprobación/políticas institucionales, TLS/proxy
  de mismo origen, mínimo privilegio PostgreSQL, claves Linux cifradas y respaldos.
- No automatizar portal universitario ni conectar cuentas institucionales. No usar
  claves/cuotas IA externas sin solicitud explícita de prueba real del usuario.
