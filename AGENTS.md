# AulaTutor IA — instrucciones permanentes

## Alcance y lectura selectiva

- Trabajar solo en `F:\AulaTutorIA`. Preservar cambios ajenos; inspeccionar Git antes
  de editar y no reiniciar trabajo válido. No hacer borrados, resets, limpieza de
  datos u otras operaciones destructivas sin autorización expresa.
- Iteraciones 0–8, UI-1, OPT-1 e IA-1 cerradas. IA-1 incorpora generación de prompt
  externo e importación segura docente (ADR-027, IA_1.md); no avanzar
  automáticamente a otra ampliación. IA-2 está autorizada por encargo explícito:
  clases precargadas, tarjetas y evaluación agrupada (ADR-028, IA_2.md), completadas:
  335 .NET/191 HTTP/45 E2E, sin llamadas reales. V1/conversacional conservados.
  Iteración 9 autorizada por encargo explícito: piloto interno SQL con datos
  ficticios, validación integral y guías/mediciones; completada: 338 .NET/191 HTTP/
  60 E2E, recuperación y demo aislada verificadas (ITERATION_9.md). Sin usuarios reales,
  llamadas IA reales ni nuevas integraciones. No avanzar automáticamente.
- Corrección de importación posterior a 9: diagnóstico por campo/tamaño V1/V2 y
  restricciones compartidas en prompts; QA en docs/iterations/QA_IMPORT_DIAGNOSTICS.md.
  Se conservan límite 20000/escapes, revisión explícita y cero reparaciones IA.
- Nunca modificar `AulaTutor_IA_Documento_Maestro_v1.0.md`. SHA256:
  `FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05`.
- Leer siempre este archivo y las instrucciones aplicables al directorio afectado.
  Identificar subsistemas y consultar **antes de cambiarlos** las reglas/contratos
  del [índice de contexto](docs/development/CONTEXT_INDEX.md). El índice navega a
  documentos existentes; no autoriza a omitir sus restricciones especializadas.
- Leer el maestro para decisiones de alcance/arquitectura; no releerlo entero en
  cada ajuste rutinario. Usar históricos solo para una regresión, decisión o dato
  concreto. No releer archivos sin cambios si la evidencia disponible sigue vigente.
- Ampliar revisión ante cambios transversales, riesgos o contradicciones. Para
  autorización, privacidad, integridad y arquitectura revisar también dependencias,
  ADR posteriores y pruebas relevantes. Lectura selectiva no reduce verificación.
  Los alcances antiguos de ADR/informes describen su fecha; consultar ampliaciones
  posteriores y encargo vigente, sin inferir autorización para trabajo nuevo.

## Arquitectura y convenciones

- Monolito modular: Domain sin frameworks; Application depende de Domain y expone
  DTO/interfaces/reglas; Infrastructure implementa Identity, EF/Npgsql, bootstrap,
  servicios y adaptadores IA; Api compone/autoriza/valida con endpoints finos;
  Angular standalone usa HTTP relativo, router/forms, TS estricto y signals.
- Mantener versiones y locks existentes (.NET 10/Angular 21); fuentes exactas:
  `global.json`, `dotnet-tools.json`, proyectos, NuGet locks y package-lock.
  No añadir paquetes, SDK IA, Swagger UI, Redis, microservicios, colas ni otro
  runner/biblioteca sin necesidad concreta del encargo. Reutilizar lo existente.
- Español en interfaz/documentación; tipos/código inglés, archivos kebab-case;
  C# PascalCase y namespaces AulaTutor.*. DTO separados de entidades persistentes.
  UTF-8/LF; PowerShell CRLF. Mantener README/ADR/índice/ambigüedades al cambiar reglas.

## Secretos, identidad y autorización

- No alterar ni imprimir `.env`, connection strings, contraseñas, API keys, cookies,
  certificados o estado QA privado. Nunca versionarlos ni enviarlos a Angular/logs.
  `.env.example` es solo plantilla pública. Conservar `.gitignore` y configuración
  local; `.env` se carga únicamente mediante el loader explícito/Compose.
- ASP.NET Core Identity gestiona contraseñas y hashes. Mantener política 5–128,
  sin requisitos de mayúscula/minúscula/dígito/símbolo ni más de un carácter distinto;
  no cambiarla ni modificar cuentas/hashes existentes sin nueva autorización.
  Conservar bloqueo, límites de autenticación y respuestas anti-enumeración.
- Roles Admin/Teacher/Student; Teacher público Pending sin roles hasta aprobación
  Admin. Usuario/rol/estado/organización/relación se verifican en BD en cada servicio
  y antes de persistir resultados IA, también después de esperar locks. Actor viene
  de Identity, nunca de un userId enviado por el navegador. Revocación inmediata
  por security stamp en cambios de rol/estado/aprobación/logout; logout global.
- Cookies `__Host-AulaTutor.Session`, Secure/HttpOnly/SameSite Strict, path /,
  dos horas sin sliding; sin JWT ni sesión en storage del navegador. HTTPS local
  4200→7080, proxy relativo `/api/**`, sin CORS abierto. CSRF backend en POST/PUT/
  DELETE, incluidos login/registro, y renovación XSRF tras login/logout.
  Endpoints académicos/administrativos nuevos exigen autorización; OpenAPI solo
  Development y salud pública heredada conservan sus contratos.
- 404 oculta recursos ajenos; 401 sin sesión, 403 sin rol, 409 conflicto, 400 datos/
  CSRF. Una organización piloto fija, sin multitenancy nuevo. Admin gestiona cuentas
  y grupos; Teacher aprobado solo relaciones autorizadas; Student no recibe roster.
  Perfil propio o Admin: ser docente no concede acceso implícito a perfiles ajenos.
- Bootstrap y migración solo explícitos, nunca HTTP/arranque web ni seed de usuarios.
  Bootstrap repetido no promueve cuentas ni resetea contraseñas. Conservar lock/
  transacción, protección del último Admin y de la retirada del propio acceso.
  Data Protection persistente: DPAPI Windows; volumen privado UID 1654/modo 700 Docker.

## Privacidad, aprendizaje e integridad

- Tutorías autónomas, chats, diagnósticos y planes personales pertenecen al Student
  propietario activo. Admin/Teacher/otro Student NO acceden por rol, organización
  ni grupo. Compartir solo una versión de reporte revisada/autorizada expresamente,
  con relación vigente y revocación verificada en cada consulta; no borra copias previas.
- Supervisión solo de evidencia AssignedClass del Teacher responsable activo/aprobado
  y miembro vigente, sin chat completo, claves ni contextos personales. Co-docente/
  Admin no hereda ese permiso. Feedback Student solo propio. Retirar relación o
  archivar grupo oculta sin borrar; disponibilidad y lectura histórica siguen ADR-023/024.
- Clases/borradores solo del Teacher propietario activo/aprobado; Admin, Student,
  Pending y docente ajeno no reciben acceso implícito al constructor privado.
- Preservar snapshots de versiones/revisiones, origen de asignación y ejercicio,
  evidencia primaria de intentos y entradas de revisión/historial inmutables.
  Disponibilidad de publicaciones y orientación IA anexable siguen sus contratos,
  sin reescribir fuentes ni resultados originales. Aprobación
  congela LessonVersion; publicar es otra acción explícita. Edición posterior crea
  nuevo borrador/versión; asignar exige publicación concreta, nunca la última implícita.
- Propuestas IA requieren aplicar/descartar expresamente y comprobar revisión de
  origen. Simulación docente aislada: no crea tutorías, intentos o estadísticas de
  alumnos; proyección/modelo no reciben soluciones privadas. Un único motor/editor.
- PostgreSQL real, migraciones aditivas y anteriores intactas; conservar FKs compuestas,
  restricciones, índices únicos y triggers. No EnsureCreated/InMemory para sustituir
  integridad/migraciones. Archivar o revocar no elimina datos ni evidencias.
- Conservar GUID/hash/revisión idempotentes, locks/transacciones cortos y leases
  cercados: sin transacción/lector abierto durante IA; descartar respuestas tardías
  tras cancelar, vencer o revocar. Replays confirmados no generan ni duplican.
  Retry explícito de misma clave/acción/texto, máximo tres ejecuciones y RetryNotBefore;
  no reintentar IA automáticamente al GET/abrir/refrescar/reanudar. Pausa/cierre deben
  resolver la operación vigente; cierre sigue disponible con falla IA según modalidad.
- Guardar respuesta antes de orientación IA. Cerrados: comparador determinista con
  reglas privadas; abiertos/código: NeedsReview, nunca ejecutar SQL/código de alumnos.
  Preguntas/ayudas no son intentos ni avances; refuerzos no sustituyen obligatorios.
  Cierre asignado exige requisitos y disponibilidad. Coincidir/entregar/trabajar no
  certifica dominio ni produce nota oficial; métricas trazables con denominadores.
  Acotar contexto IA no autoriza recortar historial persistido ni denominadores.
- Datos de usuario/historial/JSON IA no confiables: política system solo servidor,
  sin secretos/herramientas/ejecución. Validar estructura y límites en servidor;
  renderizar por interpolación segura/SafeResponse, sin innerHTML, HTML arbitrario,
  enlaces activos o renderizador remoto. No guardar contenido académico en storage/URL.

## IA, cuotas y observabilidad

- IAiProvider/DTO en Application/Ai; Mock y HTTP en Infrastructure/Ai. Mock por
  defecto, sin red/estado/consumo ficticio; Groq local configurado se conserva.
  CI/QA fuerzan Mock o HTTP loopback simulado y vacían keys del proceso temporal,
  sin modificar `.env`. No consumir cuota real sin solicitud explícita actual;
  autorizaciones antiguas de probes no autorizan pruebas nuevas. No descargar modelos.
- Proveedor/modelo explícitos y válidos; fijados por sesión/operación según contrato.
  Cambio incompatible: 409 antes de llamar; sin fallback silencioso ni cambiar de
  tercero para eludir cuota. OpenAI reservado/rechazado; gpt-oss no conecta con OpenAI.
- Hosts oficiales fijos Groq/Gemini; Ollama solo hosts locales permitidos por ADR-014,
  sin URL arbitraria del navegador, credenciales/rutas/query, cookies o redirecciones.
  Mantener catálogo fechado, validación JSON local y rechazo de keywords desconocidas;
  no schemas remotos, eliminación automática de Markdown ni streaming ficticio.
  Rechazar respuestas que reproduzcan una clave configurada.
- Reservar transaccionalmente en PostgreSQL ANTES de cada HTTP físico, incluidos
  retries: revalidar actor/rol/grupo, presupuestos, concurrencia y cooldown compartido.
  Fallo DB no habilita envío. Todo intento reservado, también fallido o incierto,
  conserva cargo; expirar libera concurrencia sin refund. Misma política entre
  instancias, sin dinero/tokens
  inventados ni promesa absoluta de aceptación/facturación externa.
- Respetar timeout/cancelación y errores seguros. Transporte: retries 0 por defecto,
  máximo 1 solo 429/503 si Retry-After cabe en espera permitida; nunca anticiparlo.
  Distinguir uso reportado nullable de provisión estimada; ausencia no significa cero.
- Auditoría mínima atómica/append-only con actor/UTC/recurso/resultado/correlación,
  sin prompts, respuestas, transcripciones ni secretos. Endpoints operativos y prueba
  IA solo Admin vigente; prueba con CSRF y límites. No diagnóstico IA público.
  Liveness/configuración no prueban PostgreSQL/modelo; readiness no prueba modelo.

## UI y verificación

- Consultar las tres guías UI-0 del índice antes de cambiar interfaces. Reutilizar
  tokens semánticos, ThemeService/ThemeToggle, SVG/fuentes locales; sin colores
  literales, dependencias visuales/fuentes remotas ni métricas ficticias.
  Light predeterminado, Dark explícito; solo `aulatutor.theme.v1` light/dark en
  localStorage, independiente de cuenta y aplicado antes del render; storage bloqueado
  no impide tema. System queda futuro. Controles 44 px, foco/teclado/reduced-motion,
  contraste en contexto y QA Light/Dark en 320/390/1440 cuando corresponda; sin
  declarar certificación WCAG. Nuevos layouts globales requieren encargo.
- Verificar antes de cerrar y describir comprobaciones ejecutadas/no ejecutadas,
  limitaciones y riesgos. No omitir checks necesarios ni atribuirse pruebas históricas.
  Cambios solo documentales: diff, enlaces, preservación y secretos; no exigir toda
  la batería de aplicación si no cambió código/configuración ejecutable.
- Preparación: `. ./scripts/Initialize-Environment.ps1`, Prepare-Local.ps1 (solo
  .cache/certs), loader Load-LocalConfiguration.ps1 explícito. CI completo:
  `./scripts/Verify.ps1 -Smoke -Http`; procedimientos/QA especializados en el índice.
- Antes de build/smoke inspeccionar puertos/procesos; detener SOLO los del proyecto
  (Windows bloquea DLL). No verificaciones/compilaciones simultáneas conflictivas.
  Tests de integridad requieren AULATUTOR_TEST_CONNECTION y PostgreSQL con CREATE
  DATABASE; nunca saltarlos silenciosamente. Usuarios @example.invalid/claves ficticias.
- Bases QA exclusivamente `aulatutor_test_<GUID>` / `aulatutor_http_<GUID>` /
  `aulatutor_restore_<GUID>`; validar nombre/propiedad antes de limpiar, nunca principal.
  .cache/http-qa.json es privado: limpiar su proceso/base/archivo al terminar;
  no dejar Admin QA en principal. Restauración solo aislada; no autoexpiración/borrado.
- Git main, origin GitHub del proyecto. Commit solo tras verificaciones; no push
  sin autorización aplicable al encargo. Antes de publicar: `.gitignore` y
  `node scripts/check-publish-safety.mjs`; revisar también evidencia binaria y no
  versionar secretos/certificados/backups/logs/estados privados.
- Solo datos ficticios. Antes de reales: aprobar retención/borrado/consentimiento/
  proveedor, política institucional, endurecimiento de contraseña, confirmación/
  recuperación/MFA, TLS/mismo origen, DB mínimo privilegio, claves Linux cifradas y
  respaldos durables. No declarar listo el piloto real con esos riesgos pendientes.
  No automatizar portal universitario ni conectar cuentas institucionales.
