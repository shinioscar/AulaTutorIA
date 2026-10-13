# Inventario de QA — Iteración 4

Estado: completada. Edge instalado + Playwright del runtime, API HTTPS/Kestrel y
PostgreSQL en base `aulatutor_http_<GUID>` aislada, Mock únicamente. Sin paquetes
ni navegadores instalados. Se cerraron contextos/navegador y se eliminaron proceso,
base y archivo privado http-qa.json al terminar. Groq principal restaurado sin generación.

| Recorrido / control | Comprobación funcional / visual prevista |
|---|---|
| Diagnóstico | Inicio, pregunta/respuesta explícita, siguiente pregunta, terminar pronto/preliminar, resumen y referencias. |
| Plan | Propuesta de 3–5 metas, ajuste versionado, Iniciar tutoría personalizada en la misma conversación. |
| Actividad | Consigna, selector/respuesta, resultado/retroalimentación, pista, error/corrección, avance sin certificar dominio. |
| Flexibilidad | Pregunta libre conserva actividad; modo libre y ejercicio independiente; historia antigua compatible. |
| Persistencia | Pausa/reanudación, recarga/reapertura y posición durable. |
| Recuperación | Fallo/429/timeout/JSON inválido, retry misma clave, cancelación y respuesta tardía. |
| Reporte | Cierre local, generación explícita, estadísticas/evidencias, recomendaciones generales/fundamentadas, recarga sin IA. |
| Continuidad | Refuerzo idempotente desde recomendación sin mezclar historias. |
| Privacidad | Student A/B, Teacher X/Y y Admin; soluciones privadas excluidas de respuestas HTTP. |
| Compartición | Vista previa, relación, consentimiento, permiso versionado, consulta Teacher, revocación y retiro de relación. |
| Responsiva | Escritorio 1440×1000, móvil 390×844 y 320; teclado, foco, errores y ausencia de overflow/HTML activo. |

Exploraciones adicionales: solicitud de avance sin intento, respuesta desconocida,
recarga durante generación y permiso revocado. Datos ficticios y Mock únicamente.

## Resultados funcionales y visuales

El navegador recorrió diagnóstico con dos respuestas → análisis → plan de tres
objetivos → aceptación en la misma conversación → duda libre → pista → error →
simplificación → corrección → avance → pausa/recarga/reanudación → actividades
restantes → cierre → reporte → nueva tutoría de refuerzo. El chat no fuerza intentos;
la actividad no cambió al preguntar. Se registraron seis intentos: dos diagnósticos
NeedsReview, uno Incorrect, uno CorrectWithHelp con una pista/una simplificación y
dos CorrectWithoutHelp. Reporte: cinco ejercicios, seis intentos y tres objetivos
trabajados; ningún porcentaje de dominio. Refuerzo sin arrastrar mensajes previos.

Se probó vista previa de compartición, ausencia de docentes relacionados, selección
de docente aprobado relacionado, consentimiento, autorización, consulta Teacher y
revocación mediante controles Angular. Tras actualizar, Teacher quedó sin el reporte.
El acceso cruzado Student A/B, Teacher X/Y y Admin, así como el retiro de relación,
está además verificado por integración/HTTP con permisos reales y PostgreSQL.

Se observó un 429 real durante QA. Detectó pérdida de respuesta no enviada en el
formulario de ejercicio; se corrigió ngOnChanges para conservarla hasta confirmar
persistencia o cambiar consigna. Se reprodujo después un **429 interceptado en el
navegador**, sin llamada IA, para comprobar expresamente que «Falso» permanecía
seleccionado. La captura rate-limit-simulated-desktop corresponde a esa reproducción,
no a Groq ni a un error externo real. Reintento posterior registró una única respuesta.

Escritorio 1440×1000, móvil 390×844 y ancho 320: textos/consignas/resultados legibles,
sin desbordamiento horizontal observado. Pausa conserva actividad 2 de 3; su captura
de 320 corresponde a la sesión pausada, no a una afirmación de reanudación ya completada.
La reanudación se comprobó después al continuar sus ejercicios. Se revisaron tarjeta
de ruta, acción de inicio, actividad, reporte y controles de permisos. La página
permite desplazamiento vertical; los detalles secundarios pueden plegarse.

Exploración adicional con teclado (foco/Enter en Actualizar estado) y mensaje HTML
literal: cero nodos img/script dentro de la burbuja, ninguna ejecución, sin errores
JavaScript. localStorage y sessionStorage vacíos. No se presenta como auditoría WCAG
completa ni prueba de calidad pedagógica de un proveedor real. El timeout de una
automatización reinició el runtime; la sesión se recuperó sin pérdida de progreso.

## Capturas revisadas

- diagnosis-start-desktop.png: inicio y privacidad.
- diagnostic-summary-desktop.png / diagnostic-summary-mobile.png: análisis orientativo.
- plan-proposed-desktop.png / plan-mobile.png: propuesta e inicio directo.
- exercise-desktop.png / exercise-mobile.png: consigna, respuesta y ayudas.
- pause-320.png: actividad conservada en pausa.
- rate-limit-simulated-desktop.png: respuesta retenida ante 429 simulado.
- report-desktop.png / report-mobile.png: estadísticas y referencias.
- reinforcement-mobile.png: nueva sesión de refuerzo en la misma aplicación.
- sharing-no-relationship.png / sharing-authorized-desktop.png /
  sharing-revoked-desktop.png: vista previa, permiso y revocación.
- teacher-report-desktop.png / teacher-revoked-mobile.png: acceso docente mínimo.

Todas en evidence-4, con datos ficticios, sin secretos ni cookies/contraseñas visibles.
