# Usabilidad — revisión técnica del agente

Método: recorrido guionado con Edge y revisión de capturas, etiquetas y estructura
de navegación. **Cero entrevistas o participantes humanos.** Duraciones E2E incluyen
automatización, red local y esperas de límites: no son tiempos de aprendizaje ni
tiempos de una persona creando una clase.

## Pasos y comprensión

Ruta elegida en el piloto: desde Mis clases, abrir título/detalle (1), Iniciar clase
(2), Comenzar clase precargada (3). Desde Inicio se añade Mis clases (4 en total).
Existe un atajo Iniciar en la lista que omite el detalle: dos acciones desde lista,
tres desde Inicio, observado en la interfaz; el guion SQL usa la ruta de detalle.
No confundir abrir una sesión con el primer turno explícito de aprendizaje.

La creación tiene etapas separadas: crear borrador, completar/revisar contenido,
simular, pasar a revisión, aprobar, publicar y asignar. Se cuentan etapas, no todos
los clics/campos ni duración humana. En el piloto el cuerpo SQL se prepara mediante
API para reutilizar la referencia; la UI verifica creación, edición y publicación
en los recorridos actuales. La importación evita editar JSON para revisar ayudas,
pero la calidad del documento de entrada sigue requiriendo juicio docente.

Pedir una pista requiere una acción y conserva el registro. En tarjetas las ayudas
no solicitan IA; el reporte IA al cerrar es otra acción. La retroalimentación
distingue coincidencia de regla, entrega NeedsReview y revisión humana.

## Hallazgos concretos

| Hallazgo | Consecuencia observada | Acción |
|---|---|---|
| Detalle de clase decía que todo inicio posterior enviaba al proveedor | Podía confundir una clase precargada con el chat | Corregido el texto para explicar ambas modalidades; sin cambiar permisos/motor |
| Sección de introducción y contenido pueden repetir explicación | Más lectura/desplazamiento antes de responder, especialmente en 320 px | P2: optimizar presentación, sin bloquear el piloto |
| Ayudas móviles debajo del ejercicio | Requieren desplazamiento vertical en consignas largas | P1: estudiar acceso contextual/atajo con usuario; no ocultar contenido ni cambiar datos |
| Navegación activa heredada de tutorías en sesión asignada | El rótulo global puede orientar menos que el enlace Mis clases | P1: futura navegación global, fuera de rediseño en 9 |
| Necesidad de sincronizar ayudas al editar actividades | Puede bloquear guardado/validación si no se sincronizan | Guía docente incluye botón explícito; estudiar automatización visible futura |
| Límite de diez intentos en un cerrado | Un caso agotado necesita intervención/política académica | Mantener evidencia y seguimiento; no resetear ni aprobar para completar demo |
| Resultados Mock rotulados pero guiones genéricos | No permiten valorar utilidad semántica SQL | Rúbrica manual pendiente con proveedor real autorizado |

Comprobación visual: 42 capturas/mediciones Light/Dark en 320/390/1440, controles,
legibilidad y ausencia de overflow de página; resultados en ITERATION_9.md. Teclado/foco y reduced-motion
son pruebas técnicas parciales. No afirmar comprensión, satisfacción, accesibilidad
certificada ni mejora de rendimiento académico de personas reales.
