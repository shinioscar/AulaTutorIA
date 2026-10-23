# QA de Iteración 7

Fecha: 2026-10-22. Inventario preparado antes de la revisión de navegador.
Solo cuentas ficticias en PostgreSQL aislado, API Mock; sin cuota externa.

## Inventario funcional y visual

| Afirmación o control | Comprobación funcional prevista | Estado visual |
|---|---|---|
| Inicio docente / Mis grupos | Login, enlace de navegación, filtro de grupo y actualización | Datos y vacío, Light/Dark |
| Resultados de clase | Seleccionar asignación concreta, participación con denominadores | Escritorio y móvil |
| Reporte académico | Tab de objetivos/ejercicios, hechos separados y versión | Light/Dark y 320 px |
| Pendientes de revisión | Tab de casos, razones, última respuesta y acceso individual | Código/texto largo |
| Evidencias individuales | Consigna, intentos, ayudas, resultado y orientación IA separados | Scroll normal, foco y lectura |
| Revisión humana | Seleccionar actividad/referencias, guardar y actualizar revisión | Formulario, validación e historial |
| Feedback Student | Login propietario, detalle de clase, consultar autor/fecha/historial | Light/Dark móvil |
| Privacidad | Otro docente/Student/Admin, membresía retirada, chat privado | Error comprensible y sin datos previos |
| Recuperación | Reenvío idempotente, revisión vieja, fallo de persistencia | Error conserva comentario |
| Tema y teclado | Toggle, navegación/formularios con teclado, recarga | 1440/390/320, foco visible |

Exploración prevista: cambio de evidencias mientras el docente redacta; consulta
después de perder membresía; respuesta larga/SQL literal sin ejecutar ni desbordar;
grupo sin alumnos/asignaciones y guardado cuya respuesta HTTP se pierde.

Las mediciones de ajuste acompañarán a capturas inspeccionadas: ningún scroll
horizontal global, controles accesibles y contexto inicial reconocible. Las páginas
son documentos desplazables; no se exige que todo el reporte quepa en una pantalla.
No se certifica auditoría completa WCAG ni calidad semántica de un LLM con Mock.

## Resultados funcionales y visuales

Recorrido HTTPS/Kestrel aislado aprobado: 181 comprobaciones (149 previas y 32
nuevas). Navegador Edge existente con Playwright incluido, sin instalar paquetes.
26 capturas finales y 35 mediciones de ajuste; anchos 1440, 768, 390 y 320 px,
Light/Dark. Cero desbordamientos horizontales globales y cero errores JavaScript
observados. Botones/selects/summaries medidos de al menos 44 px; etiquetas de
checkbox proporcionan el área táctil. Teclado Tab deja foco sólido de 3 px.
La preferencia de tema persiste al recargar, Light inicial y reduced-motion sin
transición. Solo aulatutor.theme.v1 en localStorage; sessionStorage vacío.

Se recorrieron con controles reales: login, enlace docente, filtro de grupo ida/
vuelta, actualización, detalle, tabs Estudiantes/Pendientes/Reporte, apertura de
estudiante, selección de actividad, estados y referencias, marca docente, envío,
comentario general, historial plegable y referencias, conflicto, recuperación,
lectura Student y actualización después de restitución. Se inspeccionaron por
separado los estados visuales, incluidas zonas de formulario, reporte y feedback.

La cohorte ficticia tiene dos alumnos. A registra un acierto de regla y dos
respuestas abiertas; B registra tres errores en el mismo cerrado. La vista final
cuenta dos estudiantes que trabajaron el objetivo, tres pares alumno/ejercicio,
seis intentos, tres incorrectos, dos NeedsReview y un ejercicio coincidente sin
ayuda. Obligatorios respondidos 2/2; entregas para avanzar 1/2; finalizaron 1/2.
La marca explícita docente sobre un acierto permanece diferenciada del resultado.
NeedsReview histórico no desaparece al revisar. A finaliza mediante el cierre
existente; B permanece activo. No se afirma dominio ni equivalencia pedagógica.

Una nueva respuesta abierta después de la revisión reabre la cobertura del caso
sin borrar decisiones anteriores. Se registró SQL fenced y HTML literal: no creó
nodos img ni ejecutó el marcador JavaScript, y siguió NeedsReview. Se revisaron
consigna, opciones públicas y orientación Mock separada del comparador.

El alumno A ve su feedback específico y general con autor/fecha/historial; B ve
su estado vacío propio. Co-docente Y aprobado y miembro del mismo grupo recibe
404 sobre la asignación del responsable. Admin ordinario y Student B reciben
404 sobre las evidencias de A; co-docente tampoco obtiene conversación. Retirar
la membresía oculta contenido tras actualizar; restituirla recupera el historial.
El nombre «Docente pendiente» pertenece al fixture histórico: su cuenta está
aprobada y tiene rol Teacher; no indica un acceso con aprobación pendiente.

## Preparación y estados controlados

- Los datos iniciales y dos decisiones provienen del recorrido HTTP real Mock.
  Se prepararon por API autorizada los tres intentos incorrectos de B y una nueva
  respuesta SQL de A, sin acceder a datos principales ni consumir cuota externa.
- El reporte individual Mock se generó explícitamente por la API existente antes
  del cierre, para inspeccionar observaciones/recomendaciones referenciadas. El
  cierre se hizo con el botón existente y confirmación nativa. La proyección
  docente muestra proveedor/modelo/revisión/referencias y desactualización tras
  cambiar evidencia de progreso. No se incorporó resumen IA grupal.
- teacher-save-response-lost-staged-light-desktop simula pérdida de respuesta
  después de un commit real. La recuperación de la misma solicitud conservó
  exactamente cuatro entradas en esa actividad, sin duplicar ni alterar NeedsReview.
- teacher-concurrent-review-dark-mobile usa una decisión simultánea preparada por
  API. El formulario recibe 409, conserva comentario, exige actualización y permite
  otra decisión tras examinar el historial nuevo. No es un fallo de proveedor.
- Las retiradas/restituciones y el alta/aprobación del docente Y son operaciones
  reales sobre la BD ficticia. No constituyen cambios a usuarios/grupos principales.

Se corrigieron en QA: opciones públicas ausentes en evidencia cerrada; labels de
select explícitos (evitan incluir opciones en el nombre accesible); encabezado de
resultados sin afirmar errores inexistentes; estado de detalle Student denegado
sin mensaje falso de lista vacía; limpieza de datos visibles al fallar autorización.
La automatización sincronizó navegación asíncrona y aceptó el diálogo nativo de
cierre; esperas sobre pantallas anteriores no eran fallos de persistencia.

## Evidencia y cierre técnico

Capturas en evidence-7; el inventario exacto figura abajo. Mediciones privadas en
.cache/iteration-7-browser-checks.json. Navegadores cerrados y cookies limpiadas;
API/base/archivo privado de QA eliminados. No se borraron perfiles recursivamente.
El ciclo completo de CI aprobó 255 pruebas/181 HTTP. Después del parche compatible
de CLI/build se repitieron instalación locked, check, temas, build, audit (cero) y
smoke. El código de UI y runtime Angular no cambiaron por ese parche. La sexta
migración se aplicó a la BD principal sin usuarios QA; API/web restauradas con Groq,
salud de configuración sin generación. Detalle en ITERATION_7.md.
Este QA no certifica WCAG ni comprensión de un LLM.

## Capturas conservadas

- [student-b-own-feedback-empty-light-mobile.png](evidence-7/student-b-own-feedback-empty-light-mobile.png)
- [student-class-visibility-dark-320.png](evidence-7/student-class-visibility-dark-320.png)
- [student-feedback-dark-320.png](evidence-7/student-feedback-dark-320.png)
- [student-feedback-light-desktop.png](evidence-7/student-feedback-light-desktop.png)
- [student-feedback-light-mobile.png](evidence-7/student-feedback-light-mobile.png)
- [student-relationship-revoked-light-mobile.png](evidence-7/student-relationship-revoked-light-mobile.png)
- [teacher-ai-observations-mock-light-desktop.png](evidence-7/teacher-ai-observations-mock-light-desktop.png)
- [teacher-class-light-desktop.png](evidence-7/teacher-class-light-desktop.png)
- [teacher-concurrent-review-dark-mobile.png](evidence-7/teacher-concurrent-review-dark-mobile.png)
- [teacher-empty-light-mobile.png](evidence-7/teacher-empty-light-mobile.png)
- [teacher-home-dark-mobile.png](evidence-7/teacher-home-dark-mobile.png)
- [teacher-home-light-desktop.png](evidence-7/teacher-home-light-desktop.png)
- [teacher-individual-dark-desktop.png](evidence-7/teacher-individual-dark-desktop.png)
- [teacher-individual-dark-mobile.png](evidence-7/teacher-individual-dark-mobile.png)
- [teacher-individual-light-desktop.png](evidence-7/teacher-individual-light-desktop.png)
- [teacher-objectives-dark-mobile.png](evidence-7/teacher-objectives-dark-mobile.png)
- [teacher-objectives-dark-tablet.png](evidence-7/teacher-objectives-dark-tablet.png)
- [teacher-objectives-light-desktop.png](evidence-7/teacher-objectives-light-desktop.png)
- [teacher-queue-dark-mobile.png](evidence-7/teacher-queue-dark-mobile.png)
- [teacher-queue-history-light-desktop.png](evidence-7/teacher-queue-history-light-desktop.png)
- [teacher-relationship-revoked-dark-mobile.png](evidence-7/teacher-relationship-revoked-dark-mobile.png)
- [teacher-review-dark-320.png](evidence-7/teacher-review-dark-320.png)
- [teacher-review-form-light-desktop.png](evidence-7/teacher-review-form-light-desktop.png)
- [teacher-save-response-lost-staged-light-desktop.png](evidence-7/teacher-save-response-lost-staged-light-desktop.png)
- [teacher-sql-evidence-dark-320.png](evidence-7/teacher-sql-evidence-dark-320.png)
- [teacher-y-denied-light-mobile.png](evidence-7/teacher-y-denied-light-mobile.png)
