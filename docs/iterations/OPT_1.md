# OPT-1 — Contexto permanente y lectura selectiva

Fecha: 2026-10-23. Estado: completada y verificada, exclusivamente documental.
Base: 1966caf. Git estaba limpio; no había modificaciones ajenas que preservar.
No se modificaron funcionalidades ni se avanzó a IA-1/Iteración 9.

## Clasificación y destino

A — Permanentes: AGENTS conserva alcance, arquitectura, convenciones, secretos,
Identity/CSRF/revocación, privacidad, inmutabilidad, transacciones/idempotencia,
proveedores/cuotas, UI segura, integridad PostgreSQL, pruebas y Git. La consulta
especializada es obligatoria antes de cambiar cada subsistema, no una sugerencia.

B — Especializadas: el índice dirige a ADR y contratos que ya existían. No se
crearon copias de contratos ni otro manual con el AGENTS antiguo. Parámetros,
DTO, máquinas de estados y límites numéricos de módulos se leen donde corresponden.

C — Históricas: resultados acumulados de 0–8/UI-1, mediciones 555 ms/97/77 comunicadas
por el usuario, antiguos alcances y capturas siguen en informes/ADR y en Git.
No se eliminó ningún informe. AGENTS previo se recupera con
`git show 1966caf:AGENTS.md`; no se inyecta otra copia como contexto permanente.

## Matriz de preservación frente a los 12 apartados anteriores

| Apartado anterior | Regla conservada en raíz | Detalle existente consultable desde el índice |
|---|---|---|
| Fuente/alcance | Workspace único, maestro/hash, cambios válidos, encargos explícitos, no cuota real | Plan, cierres y preguntas; autorizar una prueba antigua no habilita futuras |
| Arquitectura/convenciones | Dependencias por capa, DTO, español/inglés, UTF-8, paquetes mínimos y locks | Mapa, ADR-011 y archivos de versiones/locks como fuente exacta |
| Temas/UI-0 | Tokens/guías, Light/Dark, única clave visual, sin storage académico, accesibilidad | Tres guías y ADR-020/026; cifras de contraste históricas quedan en QA |
| Identidad | Identity/roles/aprobación, cookies, CSRF, revocación BD/locks, 404, bootstrap/último Admin, política sin cambios | ADR-012/013/015 e IDENTITY_HTTP: límites login, filtro antes de paginar y bootstrap detallado |
| Proveedores | Mock/QA, autorización actual, sin fallback/URLs arbitrarias, catálogo/JSON seguros | ADR-014/022: nombres de variables, modelos/capacidades, hosts locales, límites de transporte/probes |
| Tutorías | Propietario exclusivo, lease cercado, revisión/GUID/hash, retry explícito, sin IA al GET, cierre seguro | ADR-016/017/022 y TUTORING_HTTP: ciclo, contexto, límites, cancel de misma clave y resumen |
| Aprendizaje | Respuesta antes de IA, reglas privadas, sin SQL, dominio/nota no inferidos, reporte compartido opt-in | ADR-018/019/022 y LEARNING_HTTP: diagnóstico preliminar, mínimo de evidencia, planes/reportes/refuerzo e idempotencia |
| Clases | Propietario Teacher, propuesta expresa, snapshots inmutables, publicar separado, simulación aislada | ADR-021/022/026 y LESSONS_HTTP: estados, revisiones, límites y constructor progresivo |
| Asignadas | Versión explícita, origen/evidencia protegidos, refuerzo aparte, relación/disponibilidad y requisitos al cerrar | ADR-023 y ASSIGNMENTS_HTTP: rangos UTC, estados, unicidad, read-only, reincorporación e historial |
| Supervisión | Solo responsable, no chats/claves/planes, decisiones y evidencia primaria protegidas, métricas con denominador | ADR-024 y SUPERVISION_HTTP: referencias, cohortes, revisión concurrente, casos y límites; orientación IA anexable no se vuelve inmutable |
| Operación/pruebas | Cuota transaccional antes de HTTP, incierto sin refund, auditoría mínima, salud separada, QA aislado/limpieza/procesos/Git | ADR-025 y OPERATIONS: defaults/overrides, uso vs estimación, auditoría, backup/reinicio/E2E; scripts existentes |
| Pendientes | Solo ficticios; autenticación/políticas/TLS/DB/claves/backups bloquean declarar piloto real; sin portal institucional | OPEN_QUESTIONS, cierres/ADR posteriores y plan; no se implementan extensiones diferidas |

La comparación conserva la diferencia entre evidencia primaria inmutable y campos
anexables de orientación IA, y entre snapshots inmutables y disponibilidad editable
de publicaciones. Se mantuvo expresamente al propietario Teacher del constructor.
No se redujo privacidad a una frase genérica de «respetar permisos».

## Duplicación retirada y lectura

- Las prohibiciones repetidas de cuota real, cambios a .env, SQL de alumnos y
  accesos implícitos ahora tienen reglas comunes explícitas en raíz.
- Se retiraron recapitulaciones de cada cierre, listas de capturas y progresiones
  históricas 91/123/155/174/214/255/290 y HTTP. No son verificaciones de OPT-1.
- Versiones exactas se consultan en manifests/locks; límites de diagnóstico,
  constructor, sesiones/asignaciones, tokens y presupuestos en sus documentos.
- El maestro ya no se exige completo en cada tarea: se consulta por alcance o
  arquitectura; el índice amplía lectura ante riesgos/contradicciones. Este cambio
  de procedimiento está autorizado por OPT-1; el maestro y las restricciones del producto no cambian.
- No se crearon AGENTS anidados/overrides que repitan o oculten seguridad. La
  búsqueda del repositorio encontró únicamente AGENTS.md en raíz, sin overrides.

No se simplificaron más las restricciones de cookies/CSRF, revocación durante IA,
privacidad por modalidad, invariantes académicas, reservas y datos inciertos,
limpieza aislada y autorización de pruebas reales: conservar su precisión importa
más que alcanzar una cifra de líneas.

El índice es referencia por instrucción de raíz, no un archivo que se supone cargado
automáticamente. La documentación oficial describe la cadena AGENTS y la prioridad
de instrucciones por directorio; no se cambió configuración global de Codex.
Fuente consultada: [AGENTS.md oficial](https://learn.chatgpt.com/docs/agent-configuration/agents-md).

## Medidas

| Medida | Antes | Después | Reducción |
|---|---:|---:|---:|
| Líneas | 419 | 172 | 58.95 % |
| Bytes UTF-8 | 32430 | 12854 | 60.36 % |
| Caracteres Unicode | 31971 | 12679 | 60.34 % |
| Tokens aproximados (caracteres/4) | 7993 | 3170 | 60.34 % |

Procedimiento idéntico: texto UTF-8 antes obtenido con `git show 1966caf:AGENTS.md`
y después desde archivo; líneas LF, bytes UTF-8 y puntos de código Unicode.
Tokens aproximados = ceil(caracteres Unicode / 4), heurística sin tokenizer/modelo.
No mide el resto de instrucciones, herramientas, historia ni documentación cargada
según cada tarea; no equivale a ahorro real de cuota ni uso facturado de Codex.
El índice se consulta cuando hace falta, por lo que tampoco se considera gratis.

## Verificación y alcance de entrega

Se comprobaron diff completo, matriz de preservación, 73 enlaces Markdown locales,
rutas históricas y hash original. git diff --check y revisión de secretos aprobados.
El índice referencia documentos existentes; no hay instrucciones anidadas nuevas.
No se ejecutan pruebas de aplicación en esta tarea
exclusivamente documental; no se presentan las 290 anteriores como recién ejecutadas.
No se reinician servicios, no se accede a bases ni se cambian C#/Angular, migraciones,
contratos API, scripts, configuración, autenticación, .env o credenciales.

Archivos de entrega: AGENTS.md y README.md modificados; CONTEXT_INDEX.md y este
informe nuevos. README enlaza la navegación selectiva y corrige la prohibición
histórica de UI-1, ya cerrada. Sin publicación remota automática por este encargo.
