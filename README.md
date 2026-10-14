# AulaTutor IA

Iteración 5: constructor docente, simulaciones aisladas y versiones inmutables,
además de diagnóstico estructurado, planes personales ejecutables, ejercicios,
intentos, reportes orientativos, refuerzo y compartición voluntaria mínima,
además de proveedores IA, identidad, roles y grupos. Fuente de verdad:
`AulaTutor_IA_Documento_Maestro_v1.0.md`, intacto. La base de Iteración 0 conserva
Mock, salud/OpenAPI, arquitectura modular, Compose y CI local. Las clases todavía
no se asignan a grupos ni se ejecutan por estudiantes. No hay evaluaciones oficiales.
Groq, Gemini y Ollama tienen adaptadores REST; Mock es
predeterminado sin configuración externa y obligatorio en pruebas. En el entorno
local del usuario se conserva Groq, con diagnóstico real informado por él:
openai/gpt-oss-20b, 555 ms, 97 tokens de entrada/77 de salida. El agente no ejecutó
llamadas reales adicionales en Iteración 3 ni modificó claves privadas de .env.

## Sistema visual y temas (UI-0)

AulaTutor Light es el tema inicial; el botón «Oscuro» de la cabecera cambia
el tema para cualquier rol. La elección explícita persiste en el navegador bajo
`aulatutor.theme.v1`; es la única preferencia guardada en localStorage, sin datos
académicos, credenciales ni sesiones. Se aplica antes de arrancar Angular. Si el
almacenamiento está bloqueado, el cambio sigue funcionando en la página actual.

La guía oficial está en [DESIGN_SYSTEM](docs/design/DESIGN_SYSTEM.md),
[THEME_TOKENS](docs/design/THEME_TOKENS.md) y
[COMPONENT_GUIDELINES](docs/design/COMPONENT_GUIDELINES.md), con arquitectura en
[ADR-020](docs/decisions/ADR-020-temas-y-sistema-visual.md). Inicio demuestra la
identidad educativa: superficies claras/azul/verde y alternativa marino/turquesa/
violeta. Cabecera, navegación, tarjetas, controles, avisos, conversación, código
y reportes consumen tokens semánticos comunes. No se añadieron dependencias.

Los demás flujos conservan su distribución; los dashboards completos, sidebar/
drawer y opción de tema del sistema quedan para encargos posteriores. No hay
métricas inventadas ni cambios en autenticación o permisos. Las próximas pantallas
deben reutilizar esta base y funcionar en ambos temas. Evidencia y comprobaciones
de UI-0: [QA_UI_0](docs/design/QA_UI_0.md).

## Herramientas

.NET SDK 10.0.302, EF Core/Identity 10.0.10, Npgsql EF 10.0.3, PostgreSQL 18.6;
Angular 21.2.25 (CLI/build 21.2.26), Node 24.11.0 y npm 11.6.2 usados en validación.
PowerShell 7 y Docker Desktop Linux activo. Dependencias exactas y lockfiles
versionados; dotnet-ef local en dotnet-tools.json. Sin instalaciones globales.

## Preparación

En PowerShell desde `F:\AulaTutorIA`:

```powershell
. ./scripts/Initialize-Environment.ps1
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Elegir POSTGRES_PASSWORD privado en .env si aún es un placeholder.
./scripts/Prepare-Local.ps1
. ./scripts/Load-LocalConfiguration.ps1

docker compose --env-file .env -f deploy/compose.yaml up -d postgres
dotnet restore AulaTutorIA.slnx --locked-mode
dotnet tool restore
dotnet run --project src/AulaTutor.Api --no-launch-profile -- --migrate
```

PostgreSQL está en localhost:15432, con 5432 dentro de su contenedor. .env lo lee
Compose; Load-LocalConfiguration.ps1 carga explícitamente conexión, certificados
y claves de protección/configuración IA para comandos .NET. No imprime secretos. .cache y .env se
excluyen de Git. No copiar contraseñas en appsettings o Angular.

Prepare-Local exporta el certificado HTTPS de desarrollo a .cache/certs. Usa
localhost para autenticación; si el navegador no confía en el certificado,
configurar su confianza local con `dotnet dev-certs https --trust`. Este comando
solo se ejecuta conscientemente por el desarrollador. No omitir validación TLS
fuera de las verificaciones locales con certificado de desarrollo.

## Administrador inicial (explícito)

```powershell
./scripts/Bootstrap-Admin.ps1
```

Solicita email, nombre y contraseña de forma interactiva, sin contraseña en
argumentos ni logs. No hay usuario/contraseña predeterminados. Ejecutar después
de la migración. Repetir con el mismo administrador no cambia su contraseña;
una cuenta previamente registrada no puede promoverse por bootstrap. Después,
los administradores adicionales se gestionan mediante un Admin autenticado.
También se admite --bootstrap-admin con variables privadas BootstrapAdmin__Email,
BootstrapAdmin__DisplayName y BootstrapAdmin__Password para operación controlada.

## Ejecutar API y web

API local (terminal 1, raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
. ./scripts/Load-LocalConfiguration.ps1
dotnet run --project src/AulaTutor.Api --launch-profile https
```

Web (terminal 2, raíz):

```powershell
. ./scripts/Initialize-Environment.ps1
Set-Location src/AulaTutor.Web
npm.cmd ci
npm.cmd start
```

Abrir https://localhost:4200. Proxy HTTPS /api al mismo origen del navegador.
API HTTPS https://localhost:7080; salud heredada http://127.0.0.1:5080/api/health.
OpenAPI solo Development en /openapi/v1.json. Salud conserva iteration=1 por
compatibilidad y muestra el proveedor configurado, sin llamadas IA ni readiness DB.

Alternativa API en Docker (no ejecutar a la vez la API local):

```powershell
docker compose --env-file .env -f deploy/compose.yaml --profile app up -d --build
docker compose --env-file .env -f deploy/compose.yaml --profile app run --rm api --migrate
```

Puertos de API 5080 HTTP y 7080 HTTPS. Montaje local de certificados, claves de
Data Protection persistidas en volumen privado /keys; API sin root. Compose es
de desarrollo. `docker compose --env-file .env -f deploy/compose.yaml down`
conserva volúmenes; no usar -v para una parada habitual. Para la web, Ctrl+C.

## Funcionalidades y permisos

- Registro Student; solicitud Teacher pendiente sin rol docente, con login y
  consulta de su estado. Admin aprueba/rechaza y administra roles/estado de cuentas.
- Login, perfil de sesión /me y logout. Cookies Secure/HttpOnly/Strict, expiración
  de 2 horas; sin tokens en localStorage. Logout y cambios de permisos revocan
  sesiones anteriores, por lo que el usuario debe volver a entrar tras aprobación.
- Admin crea/renombra/archiva grupos y asocia docentes aprobados/estudiantes.
  Teacher ve grupos asignados y listado mínimo de miembros; Student solo sus
  grupos, sin listado de compañeros ni perfiles ajenos. Archivo conserva datos.
- Organización piloto única, preparada en el modelo sin interfaces multitenant.
- Contraseña nueva de 5–128 caracteres, sin composición obligatoria; longitud,
  mayúsculas, minúsculas, números y símbolos son recomendaciones (ADR-015).
- Selector administrativo de miembros con búsqueda parcial por correo en toda
  la organización; paginación conserva filtro. No limita la búsqueda a 50 cuentas.
- Diagnóstico IA solo Admin: configuración sin secretos, capacidades y prueba
  explícita breve con respuesta/duración/uso reportado/errores seguros.
- Tutorías privadas Student: tema/objetivo/nivel/repaso directo, conversación,
  pistas/simplificación, historial, pausa/reanudación y resumen descriptivo al cierre.
  Teacher/Admin/otros estudiantes no reciben acceso por grupo ni rol. El alumno puede
  compartir expresamente una versión de reporte con un docente relacionado (ADR-019).

CSRF validado en todas las escrituras, incluido registro/login. Angular obtiene
/api/auth/csrf y envía X-XSRF-TOKEN; renueva tras login/logout. 5 fallos bloquean
una cuenta 15 minutos; login/registro limitados a 30 peticiones/minuto por IP.
Errores genéricos para cuentas inexistentes, bloqueadas o desactivadas; registro
válido duplicado mantiene respuesta 202. DTO validados y permisos en backend.

Contratos completos: docs/architecture/IDENTITY_HTTP.md y OpenAPI con cookie,
cabecera CSRF y códigos de respuesta. Tutorías: docs/architecture/TUTORING_HTTP.md.
Decisiones: ADR-012 a ADR-019. Aprendizaje: docs/architecture/LEARNING_HTTP.md.

## Tutoría privada

Entrar como Student y abrir «Tutorías». Crear tema y objetivo conserva una sesión
sin llamar IA. «Comenzar conversación» realiza el primer turno explícito. «Enviar
mensaje», «Necesito una pista» y «Explícalo más sencillo» envían un turno cada uno
al proveedor/modelo fijados al crear. Groq permanece activo en el entorno local;
no se hace fallback ni se genera al abrir, actualizar o reanudar una sesión.

La IA recibe política del servidor, metadatos y una ventana reciente acotada. Las
observaciones son orientativas, no corrección objetiva ni calificaciones. Mock usa
guiones de SQL rotulados como simulación; no comprende cualquier tema como un LLM.
Código/listas/texto se representan sin HTML arbitrario y nunca se ejecutan.

Pausar y reanudar conservan historial. Tras cerrar la app o volver a entrar, abrir
la sesión desde el listado recupera el estado. Un turno pendiente tiene generación,
error o recuperación explícita: no escribir otro hasta resolverlo/cancelarlo. Retry
conserva el mismo intento y clave, con máximo 3 ejecuciones y espera del proveedor.
Cancelar conserva el intento y descarta respuestas tardías; no garantiza detener
la facturación externa. Una recuperación podría consumir cuota otra vez.

Finalizar es terminal y conserva fecha/resumen descriptivo local sin nueva llamada
IA; puedes leer la conversación posteriormente. El resumen no es un reporte
académico validado ni se envía a docentes. Límites: tema 160/objetivo 500, mensajes
de 2000 caracteres, 100 mensajes por sesión, 100 sesiones por alumno y 6 envíos/minuto
por cuenta/proceso. Revisiones y claves idempotentes protegen pestañas concurrentes.

Privacidad provisional: SOLO datos ficticios. Se conservan en PostgreSQL local
hasta limpieza explícita del piloto, sin eliminación automática. Compartición de
reportes limitada a consentimiento y relación docente vigente; el historial sigue privado.
Antes de usuarios reales hay que aprobar retención, consentimientos, políticas de
proveedores y solicitudes de eliminación. La contraseña mínima de 5 sigue vigente
por decisión del usuario, pero es un riesgo pendiente y no es apropiada para un
piloto con usuarios reales. No se modificó esa política en 3.

Aplicar PrivateTutoring de forma explícita con el comando --migrate de Preparación;
es una migración aditiva, sin modificar hashes/cuentas/grupos existentes. El arranque
web no migra. Detalles de integridad/recuperación en ADR-016/017.

## Diagnóstico, ruta personal y resultados

En una sesión nueva puedes «Comenzar diagnóstico estructurado»: las preguntas se
registran como consignas. Responde mediante «Registrar respuesta» en su tarjeta;
el chat sigue disponible para dudas libres. Finaliza el diagnóstico cuando resulte
razonable, con un máximo configurable de cinco preguntas. Si hay menos de dos
consignas respondidas, el análisis es preliminar. No produce notas oficiales.

«Generar plan personalizado» usa el diagnóstico vigente; la tarjeta muestra metas,
explicaciones, dificultad y tiempos orientativos. Puedes pedir ajustes (hasta tres
versiones). «Iniciar tutoría personalizada» acepta la ruta y empieza su primera
actividad en la misma conversación. Preguntas y ayudas mantienen la actividad;
«Continuar ruta» exige una respuesta registrada y controla el avance en backend.
Un resultado abierto pendiente de revisión permite continuar, sin certificar dominio.
Pausar/reanudar conserva posición. Puedes seguir en modo libre y pedir ejercicios
independientes sin generar un plan.

Ejercicios cerrados se contrastan con reglas privadas; código y respuestas abiertas
reciben orientación IA y quedan no verificados. No se ejecuta código/SQL. Los
intentos conservan versión, respuesta, ayudas y resultado, incluso si falla la IA.
El panel de evidencias permite revisarlos; ningún mensaje libre se convierte por sí
solo en un intento. Una coincidencia con una regla propuesta por IA no demuestra
dominio ni validez curricular: las reglas también requieren revisión pedagógica.

Finalizar conserva el resumen local. «Generar reporte orientativo con IA» añade
observaciones vinculadas a evidencias y estadísticas calculadas desde PostgreSQL.
La acción es explícita y no se repite al recargar. Reportes antiguos se marcan
desactualizados cuando cambian evidencias. Las recomendaciones permiten «Iniciar
tutoría de refuerzo» conservando su origen y sin mezclar conversaciones anteriores.

Puedes compartir únicamente una versión del reporte con un Teacher aprobado que
comparta un grupo vigente contigo. Revisa el contenido y autoriza expresamente;
el docente consulta «Reportes compartidos», sin acceso al historial ni respuestas
completas. Puedes revocar nuevas consultas; eso no elimina copias previas. Admin
no recibe acceso a reportes privados por su rol.

Aplicar LearningEvidence explícitamente con --migrate. Límites de diagnóstico en
configuración backend Learning:MaxDiagnosticQuestions (1–8) / MinimumEvidence
(1–máximo). Se conserva el límite de seis envíos por minuto: puede ser necesario
esperar durante un recorrido largo. Máximo diez intentos por ejercicio, diez
ejercicios libres y diez revisiones de reporte por sesión.

Mock permite recorrer estos controles con guiones rotulados. No evalúa semánticamente
una materia ni personaliza como un LLM. El usuario informó una tutoría pedagógica
real satisfactoria con Groq antes de 4; los nuevos prompts/planes/reportes con
Groq/Gemini/Ollama requieren validación manual autorizada posterior. No se realizaron
nuevas llamadas reales ni cambios a secretos durante esta iteración.

## Constructor docente, simulación y publicación

Entrar como Teacher aprobado y abrir «Mis clases». «Crear borrador» acepta datos
incompletos. En Contenido puedes editar metadatos, objetivos, secciones, orden,
actividades y criterios. Guarda antes de solicitar propuestas, simular o revisar.
Las actividades cerradas necesitan alternativas privadas válidas; las abiertas/
código conservan revisión humana y nunca ejecutan SQL. El editor muestra todas
las soluciones/rúbricas únicamente a su docente propietario.

«Diseñar con IA» admite plan completo, ajustes de contenidos, ejercicios o rúbricas.
Cada solicitud produce una propuesta estructurada. Revísala y pulsa «Aplicar
propuesta» o «Descartar». Un mensaje del asistente nunca cambia silenciosamente el
borrador. Puedes seguir editando durante generación; una propuesta de una revisión
anterior queda desactualizada y no puede sobrescribir esos cambios. Errores conservan
el borrador y permiten consultar/reintentar la misma solicitud o cancelarla.

En Revisión y versiones, «Simular como estudiante» guarda una copia de la revisión.
«Comenzar enseñanza simulada» genera el primer turno con el motor existente.
Prueba respuestas correctas/incorrectas, dudas, pistas, simplificación y avance.
Cada respuesta queda registrada antes de orientación IA. Las actividades obligatorias
exigen una respuesta para avanzar; el ensayo admite continuar tras errores y no
certifica al profesor. Detener, volver al editor y reabrir conservan el recorrido;
reanudar no llama IA. Cambiar el borrador no altera esa simulación.

La simulación usa almacenamiento separado de tutorías/ejercicios/reportes de alumnos.
No crea evaluaciones del docente, membresías, asignaciones ni estadísticas académicas,
ni accede a conversaciones privadas. Su proyección excluye reglas/soluciones privadas.
La futura experiencia estudiantil podrá compartir motor y versión, pero no se
promete equivalencia exacta entre recorridos ni proveedores.

«Pasar a revisión» muestra el resumen guardado y requisitos pendientes. «Confirmar
aprobación» crea una LessonVersion inmutable con el contenido completo, fecha,
responsable, número y SHA-256. «Confirmar publicación» es una acción independiente.
Para modificar contenido aprobado/publicado, «Crear nuevo borrador»; la próxima
aprobación/publicación crea otra versión sin cambiar las anteriores. Las versiones
pueden consultarse y la clase puede archivarse conservando contenido. No se requiere
completar una simulación por cada cambio. El docente conserva responsabilidad académica.

Aplicar la migración aditiva TeacherLessons explícitamente con --migrate; las tres
previas permanecen intactas. PostgreSQL protege versiones/revisiones frente a cambios
directos. Solo propietario Teacher activo/aprobado: otros docentes, Student, Pending
y Admin ordinario no reciben acceso. Contratos y límites en
[LESSONS_HTTP](docs/architecture/LESSONS_HTTP.md), decisiones en
[ADR-021](docs/decisions/ADR-021-clases-versiones-y-simulaciones.md), entrega en
[ITERATION_5](docs/iterations/ITERATION_5.md).

Todo QA usa Mock o proveedores controlados. Mock demuestra el flujo con guiones
rotulados, sin comprensión semántica. Los nuevos prompts con Groq/Gemini/Ollama y
la calidad curricular necesitan una prueba manual autorizada; no se consumió cuota
externa ni se modificaron claves/.env. La Iteración 6 requiere otro encargo.

## Configurar IA

En `.env` privado definir AI_PROVIDER=Mock, Groq, Gemini u Ollama. Para Groq:
GROQ_API_KEY y GROQ_MODEL; para Gemini: GEMINI_API_KEY y GEMINI_MODEL; para Ollama:
OLLAMA_MODEL y OLLAMA_BASE_URL. No incluir claves en comandos versionados, Angular
o appsettings. `.env.example` contiene solo nombres y valores locales sin claves.
Alternativa directa backend: Ai__Provider, Ai__Groq__ApiKey/Model,
Ai__Gemini__ApiKey/Model, Ai__Ollama__Model/BaseUrl.

Ejemplos de modelos documentados: Groq openai/gpt-oss-20b; Gemini gemini-3.8-flash;
Ollama gpt-oss:20b si YA está instalado. Ninguno se descarga automáticamente ni se
selecciona por defecto. Catálogo/capacidades y fuentes oficiales en ADR-014. Los
modelos Gemini 2.5 tienen acceso restringido a cuentas previas; modelos desconocidos
no declaran JSON Schema. OpenAI queda pendiente, sin SDK ni clave requerida.

Ollama local: http://localhost:11434 para API Windows; para API Docker usar
http://host.docker.internal:11434 (configurar expresamente si `.env` fija localhost).
Solo se aceptan hosts locales permitidos, sin URLs arbitrarias desde la web.
Groq/Gemini usan hosts oficiales fijos y no siguen redirecciones.

AI_TIMEOUT_SECONDS=30, AI_MAX_CONCURRENCY=2, AI_MAX_RETRIES=0 y
AI_MAX_RETRY_DELAY_SECONDS=5 son valores iniciales. Un proveedor seleccionado sin
configuración válida falla al iniciar, sin fallback. Tener claves sin seleccionar
un proveedor conserva Mock. Reiniciar la API para aplicar cambios; el loader
respeta variables ya definidas, por lo que usar terminal nueva al cambiar `.env`.

Prueba real opcional: configurar explícitamente proveedor/modelo/clave en backend,
iniciar API, entrar como Admin y abrir «Diagnóstico IA». Consultar configuración
no consume cuota; «Ejecutar prueba breve» envía texto fijo (máximo 128 tokens), sin
datos académicos. Puede consumir cuota y está limitada a 3 intentos/minuto. Ningún
script de verificación hace llamadas reales: todos fuerzan Mock o servidor simulado.

## Verificación reproducible

```powershell
# PostgreSQL del proyecto activo; API/web detenidas (puertos 5080, 7080, 4200 libres).
./scripts/Verify.ps1 -Smoke -Http
```

Restaura locks, compila, ejecuta 81 pruebas unitarias y 74 de integración, verifica
consistencia de migración, npm ci/TypeScript/contraste de temas/Angular, Compose, Git y dos recorridos
reales: smoke de fundaciones y HTTP de identidad/tutoría Mock sobre HTTPS/PostgreSQL.
La verificación de Iteración 5 añade recorridos docentes y conserva las 123 pruebas
previas; el HTTP ampliado comprende 107 comprobaciones. Resultados en ITERATION_5.md.
Cada prueba de identidad crea/elimina su propia BD temporal; no usa InMemory ni
omite pruebas sin PostgreSQL. Requiere credenciales locales con CREATE DATABASE.
HTTP usa cuentas @example.invalid y contraseña aleatoria, sin sembrar usuarios en
la BD principal. `.env` y certificados deben estar preparados para estos scripts.

La revisión visual funcional usa escritorio 1440×1000 y móvil 390×844/320 de ancho.
Capturas e informes históricos en docs/iterations/evidence-1 e ITERATION_1.md;
diagnóstico y nuevos ajustes en evidence-2 e ITERATION_2.md; tutorías en evidence-3
e ITERATION_3.md. Proveedores/tutorías reales adicionales no se ejecutan en CI/QA.
La Iteración 4 añade QA_4.md y evidence-4 para diagnóstico, ruta, ejercicios,
reportes, refuerzo y compartición. `Http-Identity.ps1 -KeepRunning -SetupOnly`
prepara una base temporal para revisión visual; no equivale al recorrido HTTP
completo. Limpiar su proceso/base/.cache/http-qa.json al terminar.
En Windows detener la API antes de compilar para evitar bloqueo de DLL.

La corrección posterior a 5 usa formatos IA menos anidados, `lesson-design-v2` y
`learning-v2`, sin cambiar clases/rutas existentes. Detalles en
[ADR-022](docs/decisions/ADR-022-formatos-groq-y-recuperacion.md) y
[QA Groq](docs/iterations/QA_GROQ_FIX.md). Las pruebas reales requieren autorización
explícita: `./scripts/Probe-Groq.ps1 -AllowRealCalls` usa cuentas ficticias y una
BD aislada, máximo 10 llamadas, separación mínima de 65 s, sin reintentos automáticos.
`-StudentOnly` comprueba solo Student. `-KeepRunning` conserva proceso/base/estado
privado para diagnóstico incluso al fallar; limpiar después con
`./scripts/Cleanup-GroqProbe.ps1`. No se integra en Verify y no modifica `.env`.
Groq limita tanto peticiones como tokens; los límites exactos son por organización.

## Continuar el proyecto

Consultar AGENTS.md, los ADR, docs/architecture/OPEN_QUESTIONS.md y el plan de
iteraciones y las guías de docs/design. Iteraciones 0–5 y UI-0 completadas. No avanzar
a 6 sin nuevo encargo: las asignaciones deberán referenciar LessonVersion concreta,
conservando tutorías privadas y planes personales independientes.

Pendientes: confirmación de email, recuperación/MFA, invitaciones autoservicio y
políticas de retención/compartición. Antes de producción: TLS confiable, mismo
origen/proxy definido, cifrado de claves Linux en reposo y usuario PostgreSQL de
mínimo privilegio. El piloto sigue usando datos ficticios.
