# ADR-029 — Navegación y supervisión visual UX-2

Estado: aceptada por encargo explícito UX-2; verificación de implementación en
[UX_2.md](../iterations/UX_2.md). Amplía la disposición visual de UI-0/UI-1,
sin cambiar el maestro, contratos académicos, permisos ni disponibilidad.

## Navegación y detalles

Un shell común consume las rutas existentes según roles; sidebar de escritorio
y navegación desplegable en flujo normal por debajo de 1024 px. Ruta activa
con fondo, borde, peso y aria-current. Identidad del usuario y ThemeToggle en
cabecera. Sin buscador global, notificaciones ni rutas conceptuales del mockup.
La preferencia almacenada sigue siendo exclusivamente el tema de ADR-020.

Dialog nativo para el drawer de lectura: fondo inerte, foco contenido, Escape,
cierre visible y restauración del foco. En móvil ocupa la pantalla. La consulta
individual se repite en cada apertura y las respuestas tardías se invalidan al
cerrar, recargar o destruir el componente. No contiene un editor paralelo: revisar
y enviar feedback sigue en la página completa con el contrato de ADR-024.
El editor y la revisión advierten antes de abandonar cambios locales.

## Datos y gráficos

No se amplía el backend. El resumen global cuenta asignaciones, versiones,
actividades pendientes y casos, cada uno con su unidad. No suma estudiantes de
asignaciones distintas como personas únicas. Los gráficos se presentan dentro
de los resultados de una asignación concreta, con cohorte y versión identificadas.

SVG nativo para la distribución disjunta NotOpened/Created/Active/Paused/Completed,
acompañado de tabla textual; no requiere librería. Created se deriva de opened
menos active, paused y completed conforme a los estados existentes. Total:
eligibleStudents. Barras por objetivo muestran deliveredRequired contra
eligibleRequiredSlots. NeedsReview es entrega abierta pendiente de criterio humano,
no acierto. Vacíos y denominadores cero se describen, sin porcentajes inventados.

Sin gráficas temporales, nota promedio, prioridad psicológica, nuevas inferencias,
conversación privada, claves de corrección ni datos externos. ADR-023/024/028
continúan determinando todos los accesos y el significado de las evidencias.

## Referencias y comprobación

Las cuatro PNG de references/ux-2 son conceptos visuales, nunca assets de la
aplicación ni fuentes académicas. Iconos y portada de libro son SVG propios
decorativos locales; sin nuevas licencias, fuentes remotas o dependencias.
Light/Dark comparten plantillas y tokens existentes. QA de cierre exige capturas
reales inspeccionadas y comparación explícita con los cuatro conceptos, además
de regresión técnica; compilar no acredita fidelidad visual ni certificación WCAG.

## Afinado editorial y sección de proveedor

Por encargo posterior a UX-2, Inicio se centra en accesos por rol y se retira la
tarjeta de entorno local. Proveedor de IA agrupa el estado existente: Teacher
aprobado accede por /ai-provider con teacherGuard y consulta solo /api/health;
Admin conserva /admin/ai con adminGuard, configuración saneada y prueba explícita.
Student y cuentas pendientes no reciben el enlace ni acceso a estas vistas.
La salud de AulaTutor se rotula como servicio, sin atribuir disponibilidad al modelo.
La pantalla queda como ubicación de futuras opciones; este cambio no implementa
selección de proveedor, edición de claves ni nuevas integraciones.

Se eliminan descargos repetidos y menciones de desarrollo de la interfaz. Etiquetas
precisas conservan origen IA, revisiones y denominadores. Referencias técnicas y
detalle de métricas se pliegan; consentimiento, cuota, recuperación y cambios
pendientes siguen junto a sus acciones. Los límites de piloto real se mantienen
en documentación y operación.
