# Iteración 6 — Asignación de clases y ejecución individual

Fecha: 2026-10-14. Estado: completada y verificada.
Encargo explícito de Iteración 6, sin avanzar a 7. Se revisaron maestro completo,
AGENTS/README, informes 0–5, ADR/preguntas, grupos/membresías, motor/evidencias,
versiones/simulaciones/inmutabilidad y guías UI-0 antes de implementar.
Maestro intacto, SHA256
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Resultado funcional

- Teacher aprobado asigna una publicación/versión propia concreta a un grupo con
  relación vigente. Selector identifica versiones anteriores; sin borradores ni
  selección implícita de última versión. Fechas opcionales UTC editadas en horario
  local; creación/replays, duplicados activos, revisión y cambios quedan protegidos.
- Active/Inactive/Withdrawn terminal, programación, desactivar/reactivar/retirar y
  consultar historial. Cambiar grupo/versión requiere otra asignación. Una nueva
  publicación no altera asignación, sesión ni ejercicios anteriores.
- Student dispone de Mis clases/lista/detalle con objetivos, contenido, docente,
  grupo, versión/hash, disponibilidad y progreso propio. Inicio único por alumno/
  asignación, incluso entre claves o pestañas. Crear/consultar/reanudar no IA;
  Comenzar conversación genera primer turno explícito en el chat existente.
- Un solo motor de tutoría/evidencias: núcleo aprobado común con LearningExercise
  identificado por versión/ejercicio/objetivo/Required; cada alumno tiene intentos,
  ayudas, estado y sesión propios. IA puede variar orientación y refuerzo
  IsComplementary; nunca reemplaza consignas, reglas, objetivos ni requisitos.
- Servidor controla avance y cierre. Cerrados obligatorios requieren coincidencia;
  abiertos/código permiten entrega NeedsReview, sin ejecución ni verificación de
  dominio. Cerrar exige todos los obligatorios; guarda reporte local trazable con
  hechos PostgreSQL, versión/hash y pendientes humanos, sin cuota ni nota oficial.
- Modos Private/Personalized/AssignedClass explícitos; simulación docente mantiene
  almacenamiento separado. Las clases no aparecen en listado privado. Teacher/Admin
  no reciben conversaciones por asignación; compartición expresa de reporte conservada.
- Relación/pertenencia y disponibilidad verificadas en todas las rutas relacionadas
  y antes de persistir respuesta IA. Retirar miembro/docente o archivar grupo cierra
  acceso sin borrar evidencia. Desactivar/retirar/vencer/archivar publicación deja
  historial en solo lectura con relación vigente. Reincorporar conserva sesión.
- Mismos GUID/hash/revisión/locks cortos/lease/token/retry/cancel, sin transacción
  durante IA. Contexto durable, modelo fijado sin fallback, material aprobado
  separado de respuestas no confiables. Presupuesto 32k incluye política/esquema;
  reduce contexto secundario cuando hace falta, sin recortar núcleo ni DB.

## Arquitectura y datos

ADR-023 define ciclo/privacidad/entrega; ASSIGNMENTS_HTTP.md documenta DTO/endpoints.
Domain añade LessonAssignment/AssignmentChange, modo y vínculos de sesión/ejercicio.
Application expone contratos y assigned-class-v1. Infrastructure concentra
AssignedClassAccess/AssignmentService/AssignedClassWorkflow y reutiliza
TutoringService/LearningWorkflow/validadores/IAiProvider. Minimal API fina y Angular
standalone con páginas mínimas sobre tokens Light/Dark existentes.

Migración aditiva `20261014050536_AssignedClasses`, quinta tras las cuatro anteriores
intactas. FKs compuestas, índices únicos de asignación activa y sesión alumno/
asignación; triggers sobre fuente de asignación/sesión/ejercicio e historial.
Mode identifica también planes personales previamente aceptados. No se migran
automáticamente al arrancar, ni se cambian usuarios, hashes, roles o grupos.

## Verificaciones y evidencia

Base inicial: Verify.ps1 -Smoke -Http aprobó las 174 pruebas previas (100/74),
107 HTTP y 70 comprobaciones de temas. Primer ciclo ampliado aprobó 211 pruebas
(100/111); recorrido HTTPS independiente aprobó 148 comprobaciones (107+41).
QA de navegador completada con Mock: 29 capturas, 22 mediciones de ajuste, cero
overflow horizontal y cero errores JavaScript observados. Inventario/resultados
en QA_6.md; archivos en FILES_6.md.

Se añadieron después dos pruebas unitarias de límites de contexto y una de reserva
de reporte local. El cierre ejecuta Verify completo sobre el código final (214:
102 unitarias y 112 integración), consistencia EF, TypeScript/Angular, tokens,
Compose, smoke y 149 HTTP. La consulta individual del recurso creado está incluida.
El ciclo final completo `Verify.ps1 -Smoke -Http` terminó satisfactoriamente sobre
el código de entrega, incluidas las 214 pruebas y las 149 comprobaciones HTTP.

| Comprobación final | Resultado |
|---|---|
| Restore locked / .NET Release | Correctos, cero errores/advertencias |
| Pruebas .NET | 214 aprobadas: 102 unitarias y 112 integración; cero omitidas |
| PostgreSQL / EF | Cinco migraciones reproducibles, sin cambios pendientes; bases reales aisladas |
| HTTPS / HTTP | 149 comprobaciones, incluidas las 107 anteriores |
| TypeScript / Angular | npm ci/check/build producción correctos, sin advertencias |
| Temas | 70 comprobaciones de contraste/tokens aprobadas |
| Smoke / Compose | API, OpenAPI, web/proxy Mock y configuración correctos |
| Docker Linux | Imagen final API compilada, sin iniciar otra API |
| QA visual | 29 capturas, Light/Dark, escritorio/móvil/320; cero errores JS observados |
| Fuente / secretos | Maestro original, cuatro migraciones previas intactas, cero coincidencias de secretos configurados |

Las 40 nuevas pruebas incluyen fuente publicada/no publicada/borrador, relación
docente/grupo, cruce de organización/FKs, duplicados/replay/revisión, disponibilidad,
miembro tardío/retirado, dos alumnos concurrentes, mismo núcleo y rutas distintas,
refuerzo separado, intentos/validación/persistencia/pausa, obligatorios/cierre/reporte,
privacidad/capacidades/CSRF/anónimo, cambio de modelo/respuesta inválida/429/timeout,
lease/replay pendiente/cancelación/respuesta tardía/revocación y presupuestos de
contexto. PostgreSQL real, bases aisladas y ninguna prueba omitida/InMemory.
El caso de límite conserva nueve reportes anteriores y crea el décimo local al
cerrar, sin otra llamada IA; la tutoría privada mantiene su límite previo.

## Incidencias, decisiones y límites

La QA corrigió selección de refuerzo al recargar: se restaura la actividad aprobada
actual y solo un refuerzo recién generado se selecciona automáticamente. Se repitió
pausa/recarga/reanudación/cierre. Un test de fechas adelantaba el reloj 30 horas y
caducaba cookies: renueva login después del salto, sin cambiar expiración real.
Un build coincidió con testhost y Windows bloqueó DLL; se repitió después de cerrar
el proceso propio. No se eludieron verificaciones ni se tocaron procesos ajenos.

Asignación individual queda como extensión de la política central de destinatarios,
sin UI/permisos incompletos. Hasta 300 asignaciones/docente, 100 cambios/asignación,
diez refuerzos y diez intentos/ejercicio, 100 mensajes/sesión y 100 sesiones/alumno.
Un cerrado con diez fallos requiere decisión académica posterior; no se inventa
aprobación ni reset. Cuotas por proceso no son presupuesto distribuido; cancelar/
recuperar no garantiza facturación exactly-once.

Solo datos ficticios. No nuevas llamadas reales Groq/Gemini/Ollama ni cambios a
.env/claves; el QA fuerza Mock o proveedor/HTTP controlado. Mock conserva guiones
rotulados, sin comprensión semántica ni calidad curricular demostrada. Los nuevos
prompts con proveedores reales requieren prueba manual autorizada posterior.
Las capturas staged son proyecciones controladas de navegador, no errores de Groq;
el 429 funcional observado corresponde al limitador local con Mock.

Se conserva contraseña mínima de cinco y su riesgo para usuarios reales. Retención/
consentimiento/borrado institucional, MFA/email, auditoría/budget integral, sandbox,
supervisión docente avanzada, notas e integración institucional siguen pendientes.
No se avanza a 7. No nuevos paquetes, SDK, microservicios ni fuentes remotas.

## Entorno y cierre

QA de navegador cerrada, cookies limpiadas, proceso/base/archivo privado HTTP
eliminados. Cero bases temporales test/http restantes. AssignedClasses aplicada
explícitamente a la BD principal sin bootstrap ni cuentas QA, cambios de hashes,
roles o grupos. Capturas versionadas; caches/logs/perfiles ignorados.

La revisión automática rechazó el comando combinado de arranque y comprobación
con «blocked by policy». La API se inició en una sesión de terminal controlada y
la salud se comprobó por separado: Groq/openai/gpt-oss-20b, solo configuración,
sin generación. No se modificó .env ni se consumió cuota externa.
Angular quedó activo en HTTPS 4200 y la API en HTTPS 7080/HTTP 5080; la salud a
través del proxy confirmó Groq. Identidades de procesos guardadas solo en caché
privada para la siguiente parada controlada antes de compilar.

Commit local al finalizar todas las comprobaciones, sin remoto ni push. No se avanza a 7.
