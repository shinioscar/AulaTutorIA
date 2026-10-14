# QA de Iteración 5

Fecha: 2026-10-13. Entorno: PostgreSQL aislado, API Mock, Angular HTTPS.
Nunca usar el proveedor Groq principal durante esta revisión.

## Inventario previo de comprobaciones

| Requisito / control | Comprobación funcional | Estado visual / evidencia prevista |
|---|---|---|
| Mis clases / crear / paginación | Crear borrador incompleto; abrir y recargar | Listado Light/Dark escritorio/móvil |
| Datos / objetivos / secciones | Guardar cambios, añadir/eliminar y subir/bajar secciones | Editor y actividades, 1440/390/320 |
| Ejercicios / rúbricas | Cambiar tipo, regla privada, criterios y pistas | Editor docente, sin ejecución SQL |
| Diseñar con IA | Propuesta estructurada, conversación, aceptar/descartar | Diseño y propuesta revisable |
| Edición concurrente | Cambio manual durante generación; propuesta antigua rechazada | Desactualizada y error conservando contenido |
| Simulación | Iniciar; respuesta incorrecta/correcta; pista/duda/simplificar; continuar | Tutor y actividad actual en ambos temas |
| Detener/reabrir | Detener, regresar, recargar y reanudar sin IA | Simulación detenida/activa ligada a revisión |
| Revisión | Resumen, requisitos incompletos, aviso orientativo | Resumen y aprobación explícita |
| Versiones | Aprobar/publicar; nuevo borrador; conservar versión anterior | Vista de versión congelada |
| Permisos / CSRF | Student, Pending, Admin y otro Teacher rechazados | Error seguro, ningún contenido ajeno |
| Fallo/recuperación | Fallo simulado, retry misma clave, cancelación | Campos conservados y controles de recuperación |
| Temas / accesibilidad | Toggle, teclado/foco, storage bloqueado, reduced-motion | Light/Dark, sin overflow ni solapamientos |

Exploración adicional: texto/HTML literal en contenido; navegación con cambios
sin guardar y recuperación tras respuesta HTTP incierta. Automatizadas cubren
timeout, lease, revocación, carreras e inmutabilidad directa en PostgreSQL.

## Resultados

QA realizada con Edge instalado y Playwright del runtime, sin paquetes/navegador
nuevos. Cuenta Teacher ficticia aprobada, Student de la misma base temporal y
contraseña privada aleatoria; ningún usuario QA en la base principal. 26 capturas
en `evidence-5/`, sin credenciales ni claves. Se cerraron contextos/perfil, API/base/
archivo privado de QA y servidor Angular antes de verificar la entrega.

- **A:** listado vacío → borrador incompleto → guardar metadatos → propuesta Mock →
  refinar propuesta sin aplicar → descartar primera/aplicar segunda → edición manual.
  Se ejercitaron añadir/eliminar objetivos, secciones y actividades, subir/bajar,
  los cinco tipos, opciones/reglas privadas, criterios, referencia y pistas.
- **B:** snapshot de revisión 5 → Start → Verdadero (Incorrect) → Hint → Falso
  (CorrectWithHelp) → duda/simplificación sin avanzar → Next → código NeedsReview.
  Stop → recarga → Resume conserva conversación/posición, sin generación al abrir.
  Las soluciones/rúbricas internas no aparecen en la simulación. Resultado y
  orientación también se muestran junto a la actividad para poder leerlos en móvil.
- **C:** resumen y validaciones → InReview → aprobación explícita → publicación 1 →
  nuevo borrador/título distinto → aprobación/publicación 2. Vista de versión 1
  conserva título/contenido/hash anteriores. Archivo y nuevo borrador conservan
  ambas versiones. Texto `<script>` se muestra literalmente; no ejecución ni diálogos.
- **D:** Student no tiene enlace docente, navegación directa vuelve a Inicio y GET
  de la clase propia del profesor recibe 404. Anónimos, Pending, Admin ordinario,
  Teacher ajeno, CSRF y revocación durante generación cubiertos por HTTP/integración.
- Fallo HTTP 429 **simulado mediante intercepción** conserva la petición. Otra
  intercepción pierde la respuesta después de persistir: Consultar estado recupera
  la propuesta. Una edición posterior la marca desactualizada y deshabilita aplicar.
- Timeout **preparado en PostgreSQL de QA**, no un fallo real del proveedor: el
  botón de retry usa backend/Mock real y misma clave, termina Succeeded con attempts=2.
  Pendiente preparado después se cancela con backend real. Un GET anterior retenido
  y entregado tras cancelar no restaura Pending: la UI descarta lecturas anteriores
  a una mutación. Timeout/lease/cancelación genuinos también se prueban con proveedor
  controlado en la suite .NET, sin cuota externa.
- Light/Dark en listado/editor/diseño/ejercicios/simulación/revisión/versiones;
  escritorio 1440×1000, móvil 390×844 y 320×844. Scroll vertical deliberado para
  contenido largo y log; sin desbordamiento horizontal, solapamientos ni errores
  JavaScript observados. Campos de texto ampliados; objetivo completo bajo selector
  nativo para evitar información truncada. Capturas de regiones y viewport revisadas.
- Toggle por Space/Enter en ambos sentidos; foco solid/3px. Storage bloqueado
  conserva cambio en memoria Light↔Dark. Reduced-motion computado 0s. Única clave
  localStorage `aulatutor.theme.v1`; sessionStorage vacío, sin contenido académico.
  70 pares de contraste/tokenización pasan; no equivale a auditoría WCAG completa.

La paginación del listado se comprobó en página 1 con controles de límite deshabilitados;
no se prepararon 51 clases solo para una captura. Las carreras, límites y permisos
adicionales se comprueban automatizadamente. Las capturas de revisión/publicación
incluyen estados previos al ajuste menor del formato de fecha numérico español y
del aviso que deja de indicar generación cuando una petición falla.
No se afirma probar calidad pedagógica con Groq, Gemini u Ollama reales.

## Evidencia principal

| Área | Archivos |
|---|---|
| Listado | list-empty-light-desktop, list-dark-desktop, list-dark-mobile |
| Editor y actividades | editor-light/dark-desktop, editor-light/dark-mobile, editor-light-320, exercises-light/dark-desktop, exercises-dark-mobile |
| Propuestas y recuperación | design-light/dark-desktop, proposal-outdated-dark-320, error-simulated-dark-320, recovery-staged-dark-mobile |
| Simulación | simulation-light/dark-desktop, simulation-light/dark-mobile, simulation-stopped-light-mobile |
| Revisión y versiones | review-light-desktop, version-light-desktop, version-dark-desktop/mobile/320 |

Todos son PNG en evidence-5. Mock está rotulado y solo demuestra flujo; las capturas
de error/recuperación se identifican expresamente como simuladas/preparadas.
