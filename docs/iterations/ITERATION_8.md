# Iteración 8 — Resiliencia, seguridad, cuotas y auditoría

Fecha local: 2026-10-22. Estado: **completada y verificada**. No se inicia Iteración 9 ni UI-1.
Base 9a7c1b7 en main. Se conservaron las modificaciones válidas del primer bloque;
no se reinició el trabajo. El pase inicial terminó con 258 pruebas (112/146),
181 HTTP, smoke, Angular/temas/EF/Compose correctos. No quedaron servicios propios
activos al recuperar el estado. Maestro SHA256 original preservado:
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Alcance implementado

- Cuotas PostgreSQL por intento HTTP físico, incluyendo retries: usuario/proveedor,
  ventana móvil, día UTC, organización/grupo y concurrencia global/individual.
  Reserva transaccional con advisory lock breve; actor/rol/grupo revalidados.
  Mock continúa sin red ni consumo ficticio. AllowExternalCalls detiene nuevos
  envíos mediante configuración de backend, sin cambiar proveedor ni credenciales.
- Reservas y enfriamiento sobreviven al backend: consumo incierto no se devuelve
  automáticamente; lease vencido libera concurrencia y conserva cargo diario.
  Uso reportado nullable separado de provisión estimada; no se inventa dinero ni
  se garantiza aceptación/facturación externa con una estimación local.
- Auditoría mínima atómica de cuentas/aprobación/roles/revocación, grupos/membresías,
  publicación/asignación, compartición/revocación y revisiones. Actor/UTC/recurso/
  resultado/correlación, sin contenido privado. Triggers protegen historial/origen.
  GET operativos Admin paginados/agrupados, sin acceso académico implícito.
- Dashboard docente carga cohortes/snapshots autorizados en lote. Se eliminan las
  consultas por grupo/asignación; se conservan versiones, denominadores y privacidad.
- Correlación del servidor en respuestas/ProblemDetails, readiness DB Admin,
  503 Npgsql saneado, cuerpos máximos 1 MiB y cabeceras nosniff/no-store/no-referrer/
  DENY. Cookies, CSRF, permisos y mínimo de contraseña cinco permanecen.
- Runner E2E local reutiliza Edge/Playwright existentes, sin paquetes o navegadores
  nuevos. Mock, base aislada, registro/aprobación/grupos, diseño/publicación/asignación,
  clase/feedback y recuperación. Diagnóstico/plan/reporte privados se consolidan con
  el recorrido HTTPS previo. Script específico comprueba reinicio real y replay.
- Ensayo de pg_dump/pg_restore en base aislada, archivo privado 600 y limpieza en
  finally; documentación de recuperación, protección de claves y respaldo durable.

Una migración nueva: 20261023015145_OperationalResilience (séptima), aditiva.
Las seis previas y el maestro no cambian. No crea usuarios al arranque ni modifica
versiones/evidencias originales. No hay dependencias directas o infraestructura nuevas.
ADR-025 y OPERATIONS.md contienen políticas, contratos, parámetros y límites.

## Hallazgos de seguridad y resiliencia

| Severidad inicial | Hallazgo confirmado | Corrección / evidencia |
|---|---|---|
| Alta | Cuotas/concurrencia reiniciables por proceso | Reservas SQL serializadas y durables; presupuestos/cooldown/lease probados entre contextos y con admisiones concurrentes |
| Alta | Sin auditoría central de cambios sensibles | Eventos en la misma transacción; rollback, inmutabilidad, campos mínimos y autorización Admin probados |
| Media | N+1 docente, carga completa repetida por asignación | Lectura agrupada; comparación de consultas con una y dos asignaciones/dos grupos |
| Media | Liveness se podía confundir con conectividad/IA | Readiness separada, acceso Admin; liveness sin DB e IA sin red; DB ausente devuelve error seguro |
| Media | Sin correlación uniforme y cabeceras de referencia/embebido | GUID servidor, ProblemDetails, no-referrer/DENY; sin confiar en encabezados del cliente |
| Media | Tamaño HTTP predeterminado mayor que necesidades de la aplicación | Límite 1 MiB y 413 previo a deserialización; límites académicos existentes conservados |
| Media | Recuperación no consolidada en procedimiento reproducible | Backup/restauración aislada y reinicio real con cookie/historial/replay sin duplicados |
| Alta antes de personas reales | Contraseña mínima cinco; confirmación/recuperación/MFA pendientes | Se conserva por restricción expresa; bloquea declarar preparado el piloto real |

La revisión contrastó Identity/cookies/stamps/CSRF, autorización de recursos,
separación tutorías privadas y clases, JSON no confiable, renderizado por interpolación,
destinos HTTP permitidos, reintentos/timeout/cancelación/leases, FKs/triggers,
transacciones e índices. Se conservan las pruebas previas; no equivale a una
certificación de seguridad ni a un estudio de rendimiento masivo.

## Verificación y métricas

El pase completo Verify.ps1 -Smoke -Http aprobó **290 pruebas: 116 unitarias y
174 integración, cero omitidas**. Conserva las 255 previas y añade 35 (cuatro
unitarias y treinta y una de integración). Restore locked, Release cero advertencias/
errores, siete migraciones reproducibles y modelo sin cambios pendientes,
Angular/TypeScript, 70 pares de temas, Compose, diff y smoke correctos. El recorrido
HTTPS aprobó **191 comprobaciones**, incluidas las 181 anteriores y diez nuevas
operativas/de auditoría. Log: artifacts/iteration-8-verify-final.log.

Después del pase completo se repitieron aisladamente BoundedMockLoad y
DashboardQueryCount: dos aprobadas, sin recompilar ni otros tests en paralelo.
HTTP ASP.NET TestServer in-process más PostgreSQL real aislado, no Groq/red pública:

| Alumnos concurrentes | Solicitudes | Éxito/error | p50 ms | p95 ms | Duplicados |
|---|---|---|---|---|---|
| 1 | 2 | 2 / 0 | 460.41 | 2160.63 | 0 |
| 3 | 6 | 6 / 0 | 2149.07 | 2216.86 | 0 |
| 5 | 10 | 10 / 0 | 182.36 | 2236.44 | 0 |

La misma cohorte crece por etapas: las posteriores mezclan aperturas nuevas y
replays de alumnos ya abiertos, con dos claves/pestañas por alumno. Por eso no se
interpreta el p50 menor como una mejora de capacidad al aumentar usuarios. Muestra
pequeña, cachés y locks incluidos. CPU acumulada de esas etapas: 1843.75 / 3828.13 /
5718.75 ms; memoria de trabajo y pico observados 215465984 bytes (205.484375 MiB).
Incluye test host y servidor in-process; no es memoria exclusiva de producción.
Waiters PostgreSQL observados al final de cada etapa: 0; no se afirma ausencia de
contención durante la etapa. La prueba existente de lock comprueba espera efectiva.
Tras los inicios, cinco alumnos contestaron independientemente la misma versión,
con un intento propio cada uno y sin duplicados. Los inicios no llamaron IA; las
respuestas posteriores usaron exclusivamente Mock. No se mide semántica de IA.
Datos: artifacts/iteration-8-load.json / iteration-8-dashboard-queries.json y log
iteration-8-load-isolated.log. El escenario es reproducible y acotado, sin saturar
con grupos grandes o afirmar capacidad para 30+ usuarios.
DashboardQueryCount midió 15 consultas con una asignación y 15 con dos asignaciones
en dos grupos, manteniendo cohorte y autorización. No se inventa una medida anterior
al cambio: el crecimiento previo se identificó por los bucles del código.

## Concurrencia, errores y privacidad

Cobertura nueva: doble pestaña/contextos independientes, presupuesto diario/móvil/
estimado, cupo global/usuario, overrides de grupo/organización, cooldown 429 compartido,
kill switch, origen/terminal inmutable, lease expirado sin refund, revocación antes de
admitir, rollover UTC y opciones inválidas. Transporte simulado prueba reserva por
retry, cero HTTP al rechazar admisión, 401/403/500/429, cancelación/timeout/desconexión/
JSON incompleto con uso desconocido. Auditoría confirma rollback e inmutabilidad,
contenido mínimo y exclusión Teacher/Student.

Las 255 previas conservan versión aprobada y evidencia primaria, duplicados/replays,
independencia de alumnos, estado tras pausa, revisión con evidencias concurrentes,
revocación durante generación/espera de lock, respuesta tardía, timeout y recuperación,
privacidad por modalidad, consentimiento/revocación y permisos cruzados. No se omiten
ni sustituyen migraciones por InMemory/EnsureCreated.

El runner final pasó 16 comprobaciones agrupadas (nueve funcionales y siete de
ajuste/captura), cero excepciones JavaScript y cero overflow global observado en
1440/390/320 px. Las siete capturas Light/Dark se inspeccionaron visualmente.
El reinicio real posterior conservó cookie, resumen/historial y un solo turno al
repetir la solicitud. Ambos se ejecutaron sobre el binario final, después de la
corrección de auditoría y revalidación Admin. Log: artifacts/iteration-8-e2e-suite.log;
resultados: iteration-8-e2e.json y iteration-8-restart.json. Incluye una segunda
ejecución completa del recorrido HTTPS de 191 comprobaciones antes del navegador.
429 y respuesta perdida se provocan explícitamente en el transporte de QA; no se
presentan como incidentes reales de Groq. QA_8 detalla qué recorre cada capa.

## Incidencias corregidas durante esta iteración

El pase completo detectó tres fallos de DI en pruebas que construyen Infrastructure
sin Api: faltaba registrar IHttpContextAccessor para admisión. Se interrumpió ese
pase, se registró la dependencia en Infrastructure y pasaron las 116 unitarias.
Se reinició Verify sin compilaciones paralelas. Log del fallo preservado en
artifacts/iteration-8-verify-composition-failure.log. No se ocultó ni saltó la prueba.

Identity puede marcar todas las propiedades modificadas al actualizar una cuenta;
la auditoría compara valor original/actual para evitar confundir revocación de sesión
con aprobación docente o registrar fallos de contraseña como cambios de permiso.

La revisión de escrituras detectó que la retirada de miembros usaba ExecuteDelete,
que evita SaveChanges y no generaba auditoría. Se reemplazó por borrado seguido
transaccional/idempotente. También se revalida el permiso Admin después del lock;
una prueba retira su rol durante la espera real y exige 403 sin eliminar miembros.
Esta corrección posterior quedó incluida en el pase completo del binario final;
el pase anterior de 289 se conserva como evidencia intermedia, no como cierre.

En la preparación del navegador se ajustaron dos esperas del harness: recarga del
selector de miembros tras guardar y actualización de aria-pressed tras cambiar
tema. Se conservaron las aserciones y se repitió el runner completo satisfactoriamente.
Las cuatro bases test aisladas del pase interrumpido se limpiaron al terminar,
con nombre GUID validado y sin procesos de pruebas activos; ninguna era la principal.

Al reanudar el cierre Docker Desktop estaba detenido. Se inició su motor y solo
el servicio PostgreSQL del proyecto; la comprobación posterior confirmó siete
migraciones persistidas y cero bases temporales. API/web quedaron activas y la
salud del proxy confirmó Groq sin generación. No se repitió la batería por este
arranque del entorno: el código verificado no cambió y la conectividad SQL se
comprobó expresamente. Estado final en artifacts/iteration-8-restored.json.

## Comprobaciones operativas finales

- Auditorías NuGet de los seis proyectos y npm: cero vulnerabilidades conocidas
  en los orígenes consultados; sin añadir paquetes. Logs privados en artifacts/
  iteration-8-nuget-audit-final.log e iteration-8-npm-audit-final.json.
- Imagen Linux API construida sobre el código final; Compose config aprobado.
- Séptima migración aplicada explícitamente en la base local mediante --migrate,
  sin bootstrap. Conteos de cuentas, versiones, intentos, mensajes, revisiones y
  membresías idénticos antes/después; no se eliminaron datos principales.
- pg_dump/pg_restore final: siete migraciones y conteos de esos registros iguales
  entre fuente y copia aislada. Archivo temporal modo 600, copia/archivo eliminados.
  Resultado en iteration-8-backup-final.log e iteration-8-main-maintenance.json.
- Hash del maestro original y seis migraciones históricas intactos. Escaneo de
  secretos configurados en los archivos de entrega: cero coincidencias; .env,
  certificados, estados privados, logs y dumps fuera de Git.
- Bases QA/procesos propios y .cache/http-qa.json limpiados. No quedaron tests,
  compilaciones ni bases test/http/restore GUID activas al restaurar el entorno.

## Requisitos previos al piloto y límites explícitos

| Prioridad | Requisito |
|---|---|
| Bloqueante para personas reales | Aprobar endurecimiento de contraseña (recomendación concreta: mínimo doce), confirmación/recuperación de cuenta y MFA Admin; no se cambia sin autorización |
| Bloqueante institucional | Consentimiento, tratamiento de datos/proveedores, retención/borrado y base de compartición; revisión curricular de reglas/rúbricas |
| Bloqueante operativo para despliegue | TLS/proxy del mismo origen y cabeceras del documento Angular, usuario DB mínimo privilegio, cifrado de claves Linux y respaldos cifrados fuera del volumen con restauración/RPO/RTO aprobados |
| Antes de ampliar tamaño | Medir grupos mayores y política real del proveedor; política de cuota idéntica entre instancias; paginar evidencia sin truncar estadísticas si se necesita |

No se probaron proveedores reales ni equivalencia semántica/curricular, 30+ alumnos,
varias máquinas backend reales, fallos de disco/HA/PITR, un despliegue público ni
certificación WCAG/pentest. El respaldo de ensayo se elimina: no es una política de
copias durables. No se garantiza facturación exactly-once ni tokenización exacta.
Las pruebas SQL entre contextos no se presentan como despliegue multiinstancia.

Extensiones preparadas por el punto común de admisión, sin implementación: clases
precargadas, menos llamadas/híbrido, evaluación agrupada, planes desde diagnósticos
precargados, claves individuales, constructor simplificado e importación JSON externa.
Sin nuevos proveedores, roles, calificaciones, integraciones ni rediseño UI-1.

## Entorno y entrega

Cero nuevas llamadas reales a Groq/Gemini/Ollama/OpenAI. Sin cambios a .env,
credenciales, contraseña mínima, conversaciones o evidencia principal. Datos QA
@example.invalid exclusivamente en bases temporales controladas. Migración principal
y restauración aislada verificadas; API y Angular locales activos en HTTPS con Groq
configurado, comprobado solo mediante salud del proxy (sin generación IA). Estado
privado de procesos actualizado y QA limpio. Cierre local en main, sin remoto ni
push; inventario en FILES_8.md. No avanzar a 9 ni a UI-1.
