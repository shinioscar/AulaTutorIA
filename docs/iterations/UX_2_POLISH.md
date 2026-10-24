# UX-2 — Afinado editorial y navegación de proveedor

Encargo posterior al cierre UX-2, sobre `cb90a9f`. Estado: completado y verificado.
Solo datos ficticios y proveedor Mock durante QA. Maestro v1.0 intacto.

## Cambios

| Área | Resultado |
|---|---|
| Inicio | Saludo y accesos a tareas por rol. Se retiran el bloque de entorno local, la tarjeta de aclaraciones y la bienvenida duplicada de usuarios autenticados. |
| Proveedor de IA | Nueva entrada lateral para Teacher y Admin. El docente consulta estado del servicio y proveedor; Administración conserva configuración saneada y prueba explícita. |
| Supervisión | Se elimina «El avance no es una calificación» y otros descargos repetidos. Las barras conservan cantidades y denominadores. Reporte por objetivo con métricas plegables; referencias técnicas fuera de la lectura principal. |
| Clases y constructor | Descripciones breves, modalidad y tipos de actividad legibles, revisión/publicación y acciones de propuesta conservadas. |
| Tutorías y tarjetas | Se simplifican avisos, diagnósticos, planes, simulación y resultados. Se evita repetir el resumen local y se pliegan sus detalles técnicos. |
| Reportes compartidos | Descripción breve y consentimiento por destinatario/versión. Se conserva el alcance de revocación junto a su acción. |
| Cuentas y grupos | Etiquetas en español y descripciones centradas en la tarea. Se retiran menciones de piloto y aclaraciones de permisos repetidas. |
| Mensajes fijos del servidor | Confirmaciones y resúmenes nuevos más breves; aviso de revisión humana se conserva donde corresponde. Las evidencias e historiales existentes permanecen intactos. |

Las aclaraciones académicas y operativas se conservan en los contratos, ADR y
guías del piloto. Se mantienen las etiquetas de origen IA, tipo de validación,
revisión humana y evidencia insuficiente. Se conserva la información que permite
decidir al compartir, generar, reintentar o abandonar cambios locales.

## Permisos y límites

`/ai-provider` reutiliza teacherGuard; `/admin/ai` conserva adminGuard. El menú
elige la ruta administrativa para Admin y la docente para Teacher. La vista
docente solo llama a GET /api/health. GET /api/admin/ai/ y POST /api/admin/ai/test
siguen siendo administrativos y la prueba exige CSRF. No hay endpoint nuevo ni
configuración editable de proveedores, claves o modelos en esta entrega.

«Servicio AulaTutor: Disponible» representa liveness de la API. «Proveedor
configurado» identifica la configuración; no se presenta como prueba de conexión
del modelo. La consulta de pantalla y Actualizar estado no disparan IA.

No cambian comparadores, estados, denominadores, disponibilidad, versiones,
snapshots ni política del tutor. No se reescribe texto académico almacenado ni
se filtran respuestas por frases. No hay migraciones, paquetes o llamadas reales.
El aspecto de producto terminado no modifica los requisitos pendientes para
[personas y datos reales](../pilot/READINESS.md).

## Verificación

| Comprobación ejecutada | Resultado |
|---|---|
| `Verify.ps1 -Smoke` | 356 pruebas .NET: 157 unitarias y 199 de integración con PostgreSQL; ninguna omitida. Release sin advertencias/errores y sin cambios de modelo EF pendientes. |
| HTTP HTTPS y autorización | 191 comprobaciones satisfactorias, incluidas CSRF y roles. |
| E2E con Edge/Playwright y Mock | 184 comprobaciones: 27 recorridos funcionales y 157 capturas/mediciones responsive. Cero errores JavaScript. Reinicio real de API conserva cookie, historial e idempotencia. |
| Temas y producción Angular | 80 comprobaciones de tema/contraste, TypeScript estricto y build satisfactorios. Bundle inicial: 299,81 kB; transferencia estimada: 83,31 kB. |
| Smoke | API/OpenAPI, Angular y proxy HTTPS satisfactorios; sin generación externa. |
| Dependencias | Sin cambios de paquetes/locks. Auditoría de producción: cero vulnerabilidades. `npm ci` informa dos avisos existentes en desarrollo; no se declara saneamiento de esas dependencias. |
| Último ajuste de resumen | Tras plegar el detalle de entrega se repitieron TypeScript, las 80 comprobaciones de tema y build; QA focal con seis capturas, teclado y comparación del DTO académico antes/después, sin alteraciones ni errores JavaScript. |

Se inspeccionaron visualmente las 157 capturas del recorrido completo, agrupadas
en 14 láminas, y las seis capturas focales del resumen terminado. Se revisaron
Inicio, proveedor, constructor, clases/tarjetas, tutorías, reportes, grupos,
Administración y Supervisión en Light/Dark. Los recorridos incluyen 320, 390,
768, 1024 y 1440 px según pantalla. No se detectó desbordamiento horizontal global.
La revisión focal comprobó apertura/cierre del detalle mediante Enter y conservó
el texto original del resumen. El pase completo precede únicamente a ese último
plegado de presentación; se registra su verificación focal por separado.

Evidencia representativa: [33 capturas](evidence-ux-2-polish/), entre ellas
[Inicio docente](evidence-ux-2-polish/polish-home-teacher-1440-light.png),
[proveedor administrativo](evidence-ux-2-polish/polish-provider-admin-1440-dark.png),
[Supervisión](evidence-ux-2-polish/polish-supervision-1440-light.png),
[métricas plegables](evidence-ux-2-polish/polish-supervision-report-1440-light.png)
y [resumen móvil](evidence-ux-2-polish/polish-completed-summary-390-dark.png).
Los registros completos y las 163 capturas quedan en `artifacts/`, ignorado por Git.

Dos ejecuciones previas detectaron expectativas E2E de presentación obsoletas:
esperar la actualización Angular antes de comprobar el resultado de diagnóstico
y abrir «Origen del reporte» antes de consultar metadatos plegados. Se corrigieron
los recorridos y se ejecutó de nuevo la suite completa; esos intentos no se cuentan
como pases. Las comprobaciones académicas y de permisos se conservaron.

La base ficticia aislada, sus procesos y los estados privados QA se retiraron con
`Stop-PilotDemo.ps1`, validando propiedad. API y Angular locales se restauraron con
la configuración Groq anterior y sin llamadas de inferencia. El maestro conserva
su SHA256 y la revisión de publicación no incorpora secretos ni estado privado.

No se ejecutaron pruebas con personas/datos reales ni proveedores externos.
La verificación visual no constituye certificación WCAG ni validación pedagógica.
Los requisitos de [preparación para reales](../pilot/READINESS.md) siguen pendientes.
