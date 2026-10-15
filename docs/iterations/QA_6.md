# QA de Iteración 6

Inventario previo a implementación y QA. Solo cuentas ficticias, PostgreSQL aislado,
Mock/proveedores controlados; ninguna llamada a cuotas externas.

| Requisito / control | Comprobación funcional prevista | Estado visual / evidencia |
|---|---|---|
| Docente: versiones publicadas | Selección explícita de versión y grupo autorizado; fechas; confirmar | Formulario Light/Dark, escritorio/móvil |
| Historial de asignaciones | Consultar, programar, desactivar, reactivar, retirar; revisión/idempotencia | Activa, programada, retirada; campos conservados ante error |
| Alumno: Mis clases | Lista limitada por membresía; detalle con versión/objetivos y disponibilidad | Vacía, disponible, programada, completada |
| Inicio / continuidad | Inicio doble crea una sesión, comenzar genera una vez; recarga conserva progreso | Clase recién creada y tutor activo |
| Núcleo reproducible | Dos alumnos, mismos ejercicios/objetivos/version; distintas ayudas y refuerzo | Actividad obligatoria y complementaria identificadas |
| Respuestas y avance | Cerrado incorrecto bloquea avance, abierto NeedsReview, evidencia durable | Feedback, progreso real por objetivos, ayudas |
| Pausa / reanudación / cierre | Pausar, recargar, continuar; pendientes impiden cierre; reporte traceable | Pausada y completada con pendientes de revisión |
| Resiliencia | Cuota/timeout/cancel/retry/lease y respuesta tardía; ningún GET genera | Error recuperable y espera (simulados) |
| Privacidad | Student A/B, Teacher X/Y, Admin, CSRF/anónimo, retirada de relación | Aviso de acceso revocado; tutoría privada intacta |
| Temas / accesibilidad | Toggle, teclado, foco, contraste/tokens, controles 44 px | Light/Dark a 1440/390/320, sin recortes laterales |

Escenarios exploratorios: cambiar de disponibilidad mientras el alumno genera;
recargar o pulsar inicio desde dos pestañas; retirar membresía durante generación;
publicar versión 2 mientras se cursa versión 1; texto/código largo en móvil.

## Resultados funcionales

QA real de interfaz con Edge existente y Playwright del runtime, sin nuevas
dependencias. API/Kestrel HTTPS, Angular y PostgreSQL aislado, proveedor Mock;
las claves externas se retiraron del proceso de prueba y .env no se modificó.

- Teacher seleccionó versión 1 anterior a versión 2 y SQL-01; asignó, comprobó
  rechazo de duplicado, programó versión 2 y corrigió fechas inválidas sin perderlas.
  Editó disponibilidad, desactivó/reactivó y retiró definitivamente la asignación.
  La versión, el grupo y el historial permanecieron identificables.
- Student B consultó lista/detalle, comenzó sesión sin mensajes y generó su primer
  turno mediante Comenzar conversación. Preguntó, pidió pista/simplificación y
  registró un incorrecto; no pudo avanzar ni finalizar. Al alcanzar seis turnos por
  minuto recibió 429 real del limitador LOCAL con Mock; Falso permaneció seleccionado.
  Después de la espera, el mismo formulario registró CorrectWithHelp.
- B generó/respondió refuerzo complementario, volvió al obligatorio aprobado,
  avanzó a la actividad opcional y presentó una respuesta abierta NeedsReview.
  Pausó/recargó/reanudó conservando posición; cerró con reporte local de cuatro
  intentos y una respuesta pendiente de revisión, sin llamada IA de cierre.
- Student A, inicialmente sin membresía (retirada por el HTTP), vio lista vacía.
  Su reincorporación se preparó con API administrativa únicamente en la BD ficticia;
  luego recorrió la interfaz real: misma versión/hash/ejercicio aprobado, otra sesión,
  respuesta CorrectWithoutHelp y cero complementarios frente al refuerzo y ayudas de B.
- Durante la sesión A, el docente desactivó la asignación: actualizar bloqueó envíos
  y cierre, conservando la lectura. Reactivar permitió finalizar la misma sesión.
  Abrir por URL la sesión de B devolvió 404 sin conversación visible. Las clases
  programadas conservaron inicio deshabilitado. Backend también cubre retirada de
  relación durante generación, archivo, privacidad y recuperación con proveedor controlado.
- Cierre/recarga conservaron el reporte local Application/deterministic, trazable a
  versión/hash/estadísticas. La UI distingue entrega, coincidencia y revisión humana.

## Correcciones durante QA

Recargar con un refuerzo guardado seleccionaba ese refuerzo sobre la actividad
aprobada actual. Se corrigió: solo se selecciona automáticamente un refuerzo recién
generado en la misma sesión montada. Se repitió pausa/recarga/reanudación; regresó a
e2, conservó intento abierto y permitió cerrar. Actualizar asignaciones también
limpia el mensaje de error anterior. Angular/check/build se repiten en el cierre.

El ensayo inicial de programación adelantaba 30 horas el reloj de prueba y también
caducaba las cookies de dos horas. Se corrigió el test renovando login de alumno y
docente después del salto; no se alteró la duración real de las sesiones.
Un recompilado coincidió con testhost y Windows bloqueó sus DLL; se repitió al
terminar el proceso de pruebas, sin detener procesos ajenos. Las comprobaciones
finales no dependen de ese intento fallido.

## Revisión visual y límites

29 capturas en evidence-6, Light/Dark, 1440×1000, 390×844 y ancho 320. Se revisaron
formulario docente, selección de versión anterior, errores, historial, listado,
detalle, actividad, refuerzo, pausa, revisión abierta, reporte, disponibilidad y acceso
denegado. 22 mediciones guardadas en caché: cero desbordamientos horizontales; inputs,
selects y botones visibles de al menos 44 px. Teclado: Tab desde selector de versión
lleva a grupo; tema nativo conserva foco visible. Textos/huellas ajustan líneas,
los selects nativos se acompañan del título completo y la versión. Las páginas son
desplazables; en móvil el contenido y la conversación preceden a metadatos laterales.
No se observaron errores JavaScript. No equivale a auditoría WCAG completa.

Solo aulatutor.theme.v1 en localStorage de Teacher/Student; sessionStorage vacío.
Cookies de los contextos limpiadas antes de cerrar navegadores. Proceso/base/estado
privado HTTP eliminados; perfiles y logs ignorados. Ninguna cuenta QA en BD principal.

Las dos capturas student-provider-*-staged-dark-320 son una proyección GET
controlada exclusivamente en navegador: muestran espera sin retry y retry habilitado,
con aviso visible de simulación. No generaron IA ni alteraron DB, y no prueban el
proveedor real. Retry/cancel/timeout/lease/revocación están cubiertos por integración
con proveedor controlado. El 429 del recorrido B fue del limitador local, no de Groq.

Mock demuestra estados/consignas con guiones rotulados, sin comprensión semántica.
No se afirma calidad pedagógica real de Groq/Gemini/Ollama ni se consumió cuota en 6.

El cierre repitió Verify completo: 214 pruebas (102/112), 149 HTTP, 70 comprobaciones
de temas, consistencia EF, Angular/TypeScript, smoke/Compose y build Linux correctos.
Incluye consulta del recurso docente recién creado y reserva de la décima revisión
para el reporte local, sin alterar el límite privado anterior.

## Evidencia representativa

- teacher-version-light-desktop.png, teacher-version-dark-320.png:
  versión anterior explícita y grupo autorizado.
- teacher-error-dark-mobile.png, teacher-history-dark-mobile.png,
  teacher-withdrawal-dark-desktop.png: duplicado, fechas y cambios.
- student-list-light-desktop.png, student-list-dark-mobile.png,
  student-scheduled-dark-mobile.png: estados y fechas reales.
- student-activity-light-desktop.png, student-rate-limit-dark-mobile.png,
  student-complement-light-desktop.png: núcleo fijo, respuesta conservada y refuerzo.
- student-open-review-dark-320.png, student-a-without-help-light-mobile.png:
  entregas y recorridos distintos.
- student-completed-dark-320.png, student-report-light-desktop.png:
  cierre/reporte local sin cuota ni nota.
- student-no-membership-light-mobile.png,
  student-cross-access-denied-dark-mobile.png: pertenencia y acceso cruzado.
