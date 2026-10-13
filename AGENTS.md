# AulaTutor IA — contexto para agentes

## Fuente y alcance autorizado

- Trabajar solo en F:\AulaTutorIA. Documento maestro v 1.0 en raíz: leer completo,
  nunca modificarlo. SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
- Último encargo: **exclusivamente Iteración 2**, proveedores IA y diagnóstico
  Admin, con ajustes explícitos de contraseña y búsqueda de miembros por correo.
  Iteraciones 0, 1 y 2 completadas; no avanzar a 3 sin nuevo encargo del usuario.
- Registros: docs/iterations/ITERATION_0.md, ITERATION_1.md, ITERATION_2.md; ADR en docs/.
  Mantener README, AGENTS, ADR, ambigüedades y verificaciones actualizados.
- No implementar tutorías, clases, motor pedagógico, evaluación ni reportes en 2.
  Mock, salud y estructura de proveedores permanecen funcionales.

## Arquitectura y convenciones

- Monolito modular: Domain sin frameworks; Application referencia Domain y expone
  DTO/interfaces/reglas de acceso; Infrastructure implementa Identity, EF/Npgsql,
  bootstrap, servicios y Mock; Api compone/autoriza/valida; Angular usa HTTP relativo.
- SDK .NET 10.0.302 / net 10.0; EF Core e Identity 10.0.10, Npgsql EF 10.0.3.
  dotnet-ef 10.0.10 local en dotnet-tools.json. NuGet locks y package-lock versionados.
- Angular standalone 21.2.25 con router/forms, TS estricto, signals y HttpClient.
  Texto/documentación español; tipos/código inglés, nombres de archivos kebab-case.
- C# PascalCase, namespaces AulaTutor.*; endpoints finos, lógica en servicios.
  DTO distintos de entidades de persistencia. UTF-8/LF, PowerShell CRLF.
- No nuevos paquetes sin necesidad de la iteración; sin SDK IA, Swagger UI, Redis,
  microservicios, colas o runner E 2E prematuros.
- IAiProvider/DTO en Application/Ai; Mock y adaptadores HTTP en Infrastructure/Ai.
  Mock conserva texto sin red/estado/uso ficticio; configuración explícita válida
  habilita Groq/Gemini/Ollama, nunca fallback silencioso. No SDK ni paquetes nuevos.

## Identidad y autorización (ADR-012/013)

- ASP.NET Core Identity: roles Admin/Teacher/Student, contraseñas solo Identity.
  Student público; Teacher público Pending sin roles hasta aprobación Admin.
- ADR-015: contraseñas nuevas de 5–128 caracteres para todos los roles; no exigir
  mayúscula/minúscula/dígito/símbolo ni más de 1 carácter distinto. Longitud/variedad
  recomendadas. No cambiar hashes/cuentas existentes; bloqueo y CSRF conservados.
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
- Las futuras conversaciones privadas solo pertenecen al estudiante por defecto.
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

## Operación y pruebas

- Inicializar herramientas: . ./scripts/Initialize-Environment.ps 1.
  Exportar certificado: ./scripts/Prepare-Local.ps 1 (solo .cache/certs).
  Configurar entorno privado: . ./scripts/Load-LocalConfiguration.ps 1.
- .env lo lee Compose; .NET lo carga únicamente mediante el script local explícito.
  No imprimir connection strings, cookies, contraseñas, .env ni archivos de QA privados.
- PostgreSQL localhost:15432 (interno 5432) en proyecto Compose aulatutor-ia.
  API/web pueden seguir activos al entregar: inspeccionar puertos/procesos y detener
  SOLO los del proyecto antes de build/smoke (Windows bloquea DLL en uso).
- CI local completo: ./scripts/Verify.ps 1 -Smoke -Http. Restore locked, build, 68
  pruebas (46 unitarias/22 integración; conserva las 24 originales), consistencia
  EF, npm ci/check/build, Compose config, diff, smoke Mock y HTTP identidad/diagnóstico.
- Integración exige AULATUTOR_TEST_CONNECTION (el loader lo asigna si falta) y
  PostgreSQL real con CREATE DATABASE. Bases aulatutor_test_<GUID> / aulatutor_http_<GUID>
  aisladas y eliminadas con validación de nombre. Nunca usar EnsureCreated/InMemory
  para reemplazar comprobaciones de migraciones o saltarlas silenciosamente.
- HTTP QA -KeepRunning solo para revisión: .cache/http-qa.json es privado, contiene
  contraseña ficticia aleatoria/PID/base temporal. Limpiar proceso/base/archivo al
  terminar; no mantener el administrador de QA en la BD principal.
- Pruebas con @example.invalid, claves aleatorias ficticias. Capturas versionadas
  en docs/iterations/evidence-1 y evidence-2, sin secretos. Caches/logs ignorados.
- /api/health conserva iteration=1 por contrato heredado; liveness/configuración IA,
  sin red ni readiness PostgreSQL. OpenAPI solo Development, cookie/CSRF y rutas 1/2.
  Los endpoints nuevos deben exigir autorización; nunca diagnóstico IA público.
- Git main, sin remoto. Commit solo al pasar verificaciones; no push sin autorización.

## Pendientes y continuidad

- Iteración 3: tutoría privada, orquestación/ciclo de sesión sobre contrato IA,
  conservación de identidad/pertenencia. No continuar automáticamente.
- Pendientes documentados: confirmación email, recuperación/MFA, invitaciones,
  auditoría integral (8), retención/compartición (4), frontera Approved/Published (5).
- Antes de producción/datos reales: aprobación/políticas institucionales, TLS/proxy
  de mismo origen, mínimo privilegio PostgreSQL, claves Linux cifradas y respaldos.
- No automatizar portal universitario ni conectar cuentas institucionales. No usar
  claves/cuotas IA externas sin solicitud explícita de prueba real del usuario.
