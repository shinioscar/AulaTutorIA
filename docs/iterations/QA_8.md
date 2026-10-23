# QA Iteración 8

Inventario elaborado antes del navegador; se reutilizó sin ampliar el producto.

| Requisito/control/afirmación | Comprobación funcional | Evidencia visual prevista |
|---|---|---|
| Registro docente, aprobación y grupo | Edge con formularios y controles Admin; HTTP conserva accesos cruzados | Recorrido funcional A |
| Diseñar → aplicar → revisar → aprobar → publicar → asignar | Edge real con controles; API Mock aislada | Clase publicada Light |
| Student iniciar → responder → pausa/reanudar → finalizar | Clase nueva, versión exacta y entregas por UI; historial tras recarga | Cierre Dark móvil |
| Cuota recuperable conserva texto | 429 HTTP deliberadamente simulado; reenviar explícitamente | Error Dark móvil |
| Revisión/feedback con historial | Teacher guarda comentario; Student propio lo consulta | Teacher Light/Dark320 y Student Light móvil |
| Tutoría autónoma conserva privacidad | Historial real de fixture personal, Teacher/Admin404; E2E HTTP diagnóstico/plan/reporte | Inspección funcional sin transcribir contenido a logs |
| Respuesta HTTP perdida | Respuesta confirmada en servidor y abortada en navegador, refresco conserva un turno | Verificación de sesión real por GET |
| Sin cambios visuales mayores | Foco, ajuste viewport, temas reversibles y texto seguro | Capturas 1440/390/320; inspección manual |

Exploratorios: cuota con texto sin confirmar y pérdida de respuesta después del
commit. La simulación de transporte se rotula; no representa un fallo real de Groq.
El harness espera las ventanas existentes de seis envíos/minuto; no cambia límites.
Se reutilizan Edge y Playwright del runtime instalado, sin descargar navegador ni
añadir dependencias al producto.

Reproducción y fuentes: tests/AulaTutor.E2E/run.mjs, Http-Identity.ps1 (A–E por
HTTPS/Kestrel/PostgreSQL), pruebas de proveedor/lease/cuota y Test-RestartRecovery.ps1.
El recorrido E completo de diagnóstico/plan/reporte se verifica por HTTP; el navegador
comprueba su privacidad e historial. No se afirma que todos los pasos A–F hayan sido
ejecutados mediante interfaz gráfica; la batería combina navegador, HTTP y pruebas
de integración del motor. El nombre heredado «Docente pendiente» del fixture está
aprobado y tiene rol Teacher; no representa permiso concedido a un pendiente.

## Resultados del navegador

El pase final completo del runner aprobó 16 comprobaciones agrupadas (nueve funcionales
y siete de ajuste/captura), cero excepciones JavaScript observadas y cero overflow
global en 1440, 390 y 320 px. Registro/aprobación por UI incluyó Pending sin acceso
y revocación de su cookie al aprobar; dos docentes consultaron/crearon asignaciones
concurrentes sin acceso a las del otro. La clase incluyó pista, intentos propios,
pausa/recarga/reanudación y cierre; revisión y feedback fueron visibles al propietario.
El snapshot de SQL es texto, sin ejecución. No se mide calidad curricular con Mock.

429 y pérdida de respuesta posterior a commit fueron deliberados. El primero
conservó el texto; GET tras la segunda mostró un solo turno/respuesta. Ciclo real
Light→Dark→Light o inverso, foco por Tab con outline al menos 2 px, reduced-motion
y storage limitado al tema (sessionStorage vacío) pasaron. Inspección visual de
las siete PNG: textos/controles legibles, sin solapamientos o recortes de controles
en el viewport capturado; desplazamiento vertical y scroll de conversación esperados.
No es certificación WCAG. No hay layouts nuevos ni UI-1.

Durante preparación se corrigieron dos esperas del harness, conservando aserciones:
recarga del selector de miembros posterior al POST y actualización de aria-pressed
después de cada cambio de tema. Promise.all maneja rechazo de click/respuesta sin
dejar promesas sin observar. Los fallos de preparación no se presentan como pases.
Las capturas esperan estado/animación final y enfocan resumen/feedback/texto conservado.

El reinicio real del backend de ese pase conservó cookie, historial/resumen y replay
idempotente de una sola solicitud. Se repitió todo sobre el binario final después
de corregir la auditoría de bajas y el permiso Admin tras lock. Las siete capturas
finales se volvieron a inspeccionar; ITERATION_8 registra resultados y límites.

## Capturas revisadas

- [Versión publicada Light](evidence-8/teacher-published-light.png)
- [429 simulado Dark móvil](evidence-8/student-quota-dark-mobile.png)
- [Texto conservado Dark móvil](evidence-8/student-quota-input-dark-mobile.png)
- [Clase cerrada Dark móvil](evidence-8/student-completed-dark-mobile.png)
- [Revisión e historial Light](evidence-8/teacher-review-light.png)
- [Revisión e historial Dark 320](evidence-8/teacher-review-dark-320.png)
- [Feedback propio Light móvil](evidence-8/student-feedback-light-mobile.png)
