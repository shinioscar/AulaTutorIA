# Iteración 7 — Supervisión docente, evidencias y revisión humana

Fecha: 2026-10-22. Estado: completada y verificada.
Encargo explícito de Iteración 7. No autoriza Iteración 8 ni nuevas llamadas reales
a proveedores. Se leyeron maestro completo, AGENTS/README, informes 0–6, ADR,
ambigüedades y guías UI-0 antes de implementar. Base Git limpia: 12ea1a6.
Maestro original preservado, SHA256
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Resultado funcional

- Inicio docente con grupos/cohorte vigente, publicaciones propias y asignaciones
  autorizadas. Resultados por clase/versión, participación y finalización separadas,
  métricas por objetivo/ejercicio y denominadores explícitos. Sin estadísticas falsas.
- Seguimiento individual de consignas, respuestas, intentos, ayudas y resultados.
  Refuerzos complementarios aparte del núcleo. AutomaticFeedback y orientación IA
  separados; observaciones de reportes solo con referencias válidas a esa clase.
  Sin acceso a conversación completa, diagnósticos o planes personales.
- Pendientes de revisión y casos explicables: NeedsReview/no evaluable, tres errores
  sin coincidencia, diez intentos agotados o marca docente. Filtro de casos atendidos,
  cobertura del último intento y reapertura ante nueva evidencia. Sin inferir interés
  o capacidad por inactividad, tiempo conectado o cantidad de mensajes.
- Revisión/feedback con comentario, observación, recomendación, resultado humano
  fundamentado y estados de revisión/seguimiento. Autor/fecha/referencias e historial
  inmutable. Decisión humana no cambia respuesta, comparador, ayudas, intentos ni
  avance; atender un caso agotado no lo aprueba ni reinicia.
- Student consulta únicamente sus observaciones docentes en el detalle de clase y
  conoce la visibilidad de sus actividades. La tutoría autónoma permanece privada;
  compartir/revocar reporte personal conserva ADR-019.
- UI funcional standalone sobre tokens Light/Dark, sin rediseño completo, nuevas
  dependencias directas, gráficos decorativos, fuentes remotas ni mensajería general.

## Modelo, permisos y operación

ADR-024 y SUPERVISION_HTTP.md describen contratos y límites. TeacherReview identifica
sesión/actividad; TeacherReviewEntry y ReviewEvidence almacenan decisiones y sus
referencias, sin duplicar respuestas, versiones o estadísticas derivables. La
relación con versión/asignación se obtiene de la sesión inmutable AssignedClass.
Los casos son proyecciones; GET no crea filas ni genera IA.

Solo responsable Teacher activo/aprobado y miembro vigente del grupo no archivado.
Estudiantes activos con rol y membresía actual forman la cohorte. Co-docente/Admin
no tienen permiso implícito. Withdrawn termina supervisión docente; Inactive/plazos/
publicación archivada permiten revisar historial con relación vigente. Retirar relación
oculta registros sin borrarlos; reintegración legítima recupera historial conforme
a ADR-024. Feedback propio conserva la política Student de ADR-023.

Escrituras CSRF con GUID/hash/revisión y EvidenceRevision; lock asignación→sesión,
revalidación después de esperar y persistencia atómica. Reenvío no duplica; revisión
vieja exige examinar de nuevo. Resultado humano requiere observación y evidencias;
cerrar seguimiento requiere Reviewed y referencia al último intento de la actividad.
Recuperación explícita de respuesta HTTP incierta conserva el mismo guardado.

Sexta migración aditiva `20261022224718_TeacherSupervision`, después de las cinco
previas intactas. FKs compuestas, únicos, estados e índices; triggers protegen origen
de revisión e historial/referencias. Otro trigger impide alterar evidencia primaria
de intentos o borrarla, permitiendo al motor anexar orientación/metadatos IA.
Sin migración al arranque, seeds de usuarios ni cambios de contraseña/roles/grupos.

## Trazabilidad de pruebas anteriores

La diferencia solicitada no es una regresión: Iteración 5 cerró con 155 pruebas
(81 unitarias/74 integración). El commit intermedio f336578 de corrección Groq añadió
19 casos unitarios: doce de transporte/errores/presupuesto/razonamiento y siete de
formatos de clases/plan. Cambió también aserciones existentes, sin aumentar las
74 de integración. QA_GROQ_FIX.md registra 174 (100/74), conservando el cierre de 5.
Iteración 6 añadió dos unitarias de contexto y 38 de integración de asignaciones:
214 (102/112). La línea base de 7 repitió estas 214, cero omitidas, y las 149 HTTP.
Log privado: artifacts/iteration-7-baseline.log.

## Verificación de 7

El ciclo completo `Verify.ps1 -Smoke -Http` aprobó 255 pruebas (112 unitarias y
143 integración), cero omitidas, y 181 comprobaciones HTTP sobre el C# final.
Restore locked, compilación Release sin errores/advertencias, consistencia EF,
TypeScript/Angular, temas, Compose, diff y smoke satisfactorios. QA de navegador
aprobado con 26 capturas y 35 mediciones; detalle en QA_7.md.

Se corrigió una expectativa de prueba (500 frente al 409 seguro habitual ante
DbUpdateException), sin cambiar el manejo del servidor. La comprobación concurrente
revalida permisos después de esperar un lock; su observador PostgreSQL limpia la
instantánea estadística para comprobar el bloqueo real, sin esperas a ciegas.

La auditoría npm de la línea base y del pase completo informó cuatro paquetes
afectados. Se parchearon únicamente CLI/build de 21.2.26 a 21.2.28 y su lock;
runtime/compiler-cli 21.2.25 y backend sin cambios. Después se repitieron npm ci,
TypeScript, 70 pares de temas, Angular producción, npm audit y smoke/proxy HTTPS.
Todo pasó; audit final cero vulnerabilidades. No se añadieron dependencias directas.
Justificación y fuentes primarias en ADR-024.

| Comprobación de entrega | Resultado |
|---|---|
| Restore locked / .NET Release | Correctos, cero errores/advertencias |
| Unitarias / integración | 112 / 143, total 255; cero omitidas, conserva 214 previas |
| EF / PostgreSQL real | Seis migraciones reproducibles, modelo sin cambios pendientes |
| HTTPS/Kestrel | 181 comprobaciones, incluidas las 149 previas |
| Web con lock final | npm ci/check/check:theme/build/audit y smoke aprobados |
| Dependencias | CLI/build 21.2.28; npm audit final cero vulnerabilidades |
| Docker Linux | Imagen final API compilada; configuración Compose correcta |
| QA Light/Dark | 26 PNG, 35 mediciones; cero overflow global y errores JS observados |
| Fuente y secretos | Maestro con hash original, cinco migraciones previas intactas, cero coincidencias de secretos configurados |
| Operación | TeacherSupervision aplicada explícitamente a BD principal; cero bases temporales test/http restantes |

Logs privados: artifacts/iteration-7-verify-final.log, iteration-7-web-final.log,
iteration-7-npm-audit-final.json, iteration-7-smoke-patched.log,
iteration-7-docker-final.log e iteration-7-main-migrate.log.

La ampliación contiene diez unitarias y 31 casos de integración (41 nuevos, suite
de 255). Las 214 anteriores se conservan; solo dos expectativas de migraciones
cambian de cinco a seis. La cobertura se organiza así:

| Requisitos del encargo | Evidencia automatizada |
|---|---|
| Grupos/clases autorizados, cohorte y participación (1–5) | DashboardUsesCurrentTeacherGroups, CoTeacherAdminAndOtherStudent, OpeningWithoutConversation y ParticipationAndObjectiveMetrics. |
| Objetivos, ayuda y NeedsReview (6–8, 16) | Tabla unitaria de entrega/verificación y agregación PostgreSQL de dos alumnos, varios intentos y un abierto; sin duplicar alumno/ejercicio. |
| Evidencias, revisión, feedback e integridad (9–12) | HumanReviewsAppendHistory parametrizada OpenResponse/Code, referencias ajenas, grounding/DTO y triggers de evidencia primaria/historial. |
| Errores, agotamiento y seguimiento (13–15) | RecurrentErrorsReopenAfterNewEvidenceAndExhaustionNeverApproves; TeacherCanFlagSpecificEvidenceAndLeaveGeneralClassFeedback; reglas puras de umbral/cobertura. |
| Versiones y privacidad (17–21) | NewPublishedVersionDoesNotMixEvidence, SupervisionNeverLeaksChat, consentimiento/revocación de reporte personal y restricciones entre roles. |
| Relaciones, CSRF y concurrencia (22–24) | Retiro/reingreso/archivo/revocación de rol/estado/aprobación, Anonymous/Csrf, replay simultáneo, claves diferentes y revalidación tras lock. |
| Persistencia y recuperación (25) | Fallo provocado antes de commit: rollback total y repetición de la misma solicitud, más rechazo de reescritura SQL. |
| Proveedor y regresión (26–27) | Panel/revisión funcionan con proveedor indisponible sin llamar IA; resumen grupal opcional no incorporado. Se mantienen los contratos y 214 casos previos. |

El HTTP HTTPS añade 32 comprobaciones a las 149 anteriores, sobre Kestrel y una
BD aislada; incluye opciones públicas, métricas, referencias, historial, idempotencia,
CSRF, Student correcto/ajeno, Admin, retiro de asignación y membresía. No InMemory,
EnsureCreated ni saltos de pruebas por PostgreSQL ausente.

## Proveedores, límites y continuidad

Supervisión/reportes deterministas y revisión no llaman IA y funcionan sin proveedor
disponible. Se reutilizan las observaciones existentes, identificadas como orientativas.
La generación opcional de resumen cualitativo grupal se difiere conforme al encargo,
para definir anonimización, referencias agregadas y límites/idempotencia antes de
enviar datos externos. No se implementa GroupLearningSummary innecesario.

Pruebas solo Mock/HTTP controlado, PostgreSQL real aislado y datos @example.invalid.
Mock es un guion rotulado; no certifica calidad semántica ni curricular. Cero nuevas
llamadas reales Groq/Gemini/Ollama, sin cambios a .env o secretos. Groq local se conserva.

Solo datos ficticios. Remediación/reapertura de intentos agotados, políticas
institucionales de retención/borrado, paginación y medición con grupos grandes,
email/MFA, auditoría/presupuesto integral y sandbox siguen pendientes. Mínimo de
contraseña cinco no cambia y conserva su riesgo. Sin notas oficiales, integración
universitaria, notificaciones externas ni ejecución SQL. No avanzar a 8.

## Entorno y cierre

TeacherSupervision aplicada explícitamente a la BD principal, sin bootstrap ni
usuarios QA, cambios de hashes/roles/grupos. Se conservan las cinco migraciones
previas y el maestro. Navegadores/cookies/proceso/base/archivo HTTP de QA limpiados;
caches/logs/perfiles ignorados. Capturas versionadas e inventario en FILES_7.md.

API local restaurada con Groq/openai/gpt-oss-20b en HTTPS 7080/HTTP 5080 y Angular
en HTTPS 4200. La salud a través del proxy confirmó solo configuración, sin llamada
IA. Estado de procesos guardado en caché privada para la siguiente parada controlada.
Commit local de cierre tras verificaciones; Git main, sin remoto ni push.
No se avanza automáticamente a Iteración 8.
