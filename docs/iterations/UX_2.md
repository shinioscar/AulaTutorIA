# UX-2 — Rediseño profesional del frontend

Estado: **completada y verificada**, 23 de octubre de 2026. Base `main` en `2d18b64`.
Encargo explícito UX-2 y recomendación de comparación visual obligatoria con las
cuatro referencias de `docs/design/references/ux-2`. Sin push ni despliegue.

## Auditoría inicial y dirección

Git inicial: únicamente las cuatro PNG y LEEME aportados, sin código modificado.
Maestro SHA-256 comprobado: FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
Se consultaron AGENTS, índice, las tres guías UI-0, ADR-020/021/023/024/026/027,
contrato de supervisión, clases precargadas, cierres UI-1/IA-1/IA-2/9 y QA de
importación. Las cifras históricas 356 .NET/191 HTTP y 60 E2E no constituyen
verificación de UX-2.

Las cuatro referencias se abrieron e inspeccionaron visualmente. Dirección:
sidebar clara, cabecera compacta, espacio central amplio, tarjetas resumidas,
acentos semánticos azules/turquesa, gráficos acompañados de texto y detalle lateral.
Sin buscador global, notificaciones, planes comerciales, imágenes académicas,
calificaciones ni tendencias inventadas. Light predeterminado y Dark explícito.

| Prioridad | Pantalla | Problema observado | Solución | Riesgo / control |
|---|---|---|---|---|
| P1 | Shell de todos los roles | Menú horizontal extenso, varias líneas | Sidebar desktop y menú desplegable móvil | Mantener rutas, roles, teclado y foco |
| P1 | Supervisión | Conteos dispersos entre párrafos | KPIs con unidades, dona de estados y barras por objetivo | No sumar personas entre asignaciones |
| P1 | Seguimiento | Evidencia extensa obliga a navegar | Lista paginada, filtros de cohorte y drawer de lectura | GET autorizado por apertura, sin chat privado |
| P1 | Revisión | Formulario e historial extensos | Detalle consultable y acceso a revisión completa | Conservar revisión/evidencia/idempotencia |
| P2 | Mis clases | Poca jerarquía, biblioteca de dos columnas | Tarjetas compactas y accesos de creación horizontales | Búsqueda rotulada solo de página actual |
| P2 | Crear con IA | Formulario aislado sin orientación | Resumen reactivo de configuración y tres etapas | Resumen de indicaciones, nunca contenido generado |
| P2 | Editor | Resumen de actividad solo numérico | Tipo y consigna resumida, detalle nativo desplegable | No destruir controles ni estado al plegar |
| P2 | Clase precargada | Opciones sin selección destacada | Tarjetas seleccionables, progreso con denominador | Backend conserva avance y ayudas progresivas |
| P2 | Editor / navegación | Riesgo de perder cambios al salir | Guard y aviso antes de recargar/descartar | Confirmación nativa; nada en storage |
| P2 | Admin / tutoría / reportes | Coherencia global | Shell, controles, superficies y tokens compartidos | Conservar permisos y renderizado seguro |

## Implementación

- Shell por rol: rutas existentes, iconos SVG locales, identidad del actor, tema,
  navegación activa y menú móvil en flujo normal con Escape.
- `AppIcon`: trazos propios locales, sin recursos de terceros ni paquetes nuevos.
- `DetailDrawer`: dialog nativo modal, cierre explícito/Escape, scroll interno,
  fondo inerte y restauración nativa del foco y ciclo explícito de Tab/Shift+Tab. Solo lectura en Supervisión.
- `SupervisionCharts`: dona SVG y tabla de las mismas cantidades; barras con
  progress y valores textuales. Vacíos explícitos. No depende de un servicio IA.
- Supervisión global: asignaciones, versiones publicadas propias, actividades
  pendientes y casos. No es un contador de estudiantes únicos globales.
- Seguimiento individual: búsqueda dentro de la cohorte completa autorizada,
  filtro de estado y páginas locales de 12; drawer Resumen/Evidencias/Feedback
  e historial. La escritura permanece en la revisión completa existente.
- Biblioteca: SVG genérico decorativo de libro, sin fingir contenido de la clase;
  estado y fecha reales. Resumen de configuración IA con campos existentes.
- Tarjetas Student: barra de entregas obligatorias y opciones seleccionables.
- Editor: tipo/consigna en resumen y protección de cambios locales al salir.

## Fuentes y denominadores de gráficos

`GET /api/teacher/supervision/assignments/{id}`, contrato SUPERVISION_HTTP/ADR-024.
Cohorte actual de esa asignación, nunca suma de estudiantes de varias clases:

- Estados disjuntos: Completed, Active, Paused, abiertas Created, sin sesión.
  Created = opened − active − paused − completed; NotOpened = notOpened.
  Total = eligibleStudents. Abrir Created no equivale a comenzar.
- Objetivo: deliveredRequired / eligibleRequiredSlots. Unidad: pares estudiante /
  ejercicio obligatorio. Incluye entrega abierta NeedsReview, sin declararla correcta.
- Revisiones pendientes y casos son conteos separados; no se apilan como si fueran
  estudiantes. Sin promedios, prioridad psicológica, evolución temporal ni notas.

Las consultas y escrituras mantienen los endpoints y verificaciones de relación.
El drawer vuelve a consultar por apertura y descarta respuestas tardías al cerrar,
recargar o destruir el componente. No expone claves ni conversación autónoma.

## Comparación visual real con las cuatro referencias

Se inspeccionaron las cuatro PNG originales y las capturas Angular, no solo su
existencia. La revisión incluyó conjuntos de todos los tamaños/temas y ampliación
de las vistas principales, gráficos y paneles. Las referencias permanecen intactas.

| Referencia | Coincidencias implementadas | Diferencias justificadas |
|---|---|---|
| 01 Mis clases | Sidebar, cabecera compacta, dos accesos de creación, biblioteca en grid de cuatro columnas a 1440, filtros y estados | Portada de libro decorativa, sin fotografías académicas inventadas; sin totales globales ni estudiantes por clase que el listado no ofrece. Búsqueda rotulada de página actual. |
| 02 Crear con IA | Formulario principal y resumen lateral reactivo, nivel/cantidad agrupados, opciones avanzadas plegadas, recorrido de tres etapas | El lateral resume indicaciones, no finge contenido generado. Se conservan generación integrada y prompt/importación V1/V2; sin documentos, drag-and-drop ni promesas temporales. |
| 03 Tarjetas | Una actividad principal, progreso con denominador, opciones seleccionables, ayudas laterales y contenido bajo demanda | Menú Student real, no el menú Teacher conceptual. Material e introducción aprobados siguen accesibles; sin imágenes didácticas, notas o soluciones anticipadas. Feedback autorizado conserva su texto y origen. |
| 04 Supervisión | Cuatro indicadores, dona con tabla, barras por objetivo, casos plegables, listado filtrable/paginado y drawer con secciones | Gráficos por asignación para no mezclar cohortes. No hay notas, tendencias, prioridades inferidas ni mensajería. Feedback se consulta en el drawer y se registra en la revisión completa existente. |

Durante QA se corrigieron: ciclo de foco del drawer, lectura prematura de
aria-expanded en el test del menú, exceso de altura antes de la pregunta,
formulario IA demasiado largo, barras nativas sin color de tema y cuadrícula de
indicadores desequilibrada en tablet. El material académico no se elimina ni
se recorta para compactar la vista.

## Capturas finales

Directorio [evidence-ux-2](evidence-ux-2/): 114 capturas/mediciones del E2E completo,
ocho capturas focales SQL y once hojas de contacto. Las 122 capturas originales
se revisaron en conjuntos; las vistas principales se ampliaron. Ninguna imagen
se presenta como prueba de una funcionalidad inexistente o como dato productivo.

| Pantalla | Escritorio Light | Escritorio Dark | Móvil Light | Móvil Dark |
|---|---|---|---|---|
| Mis clases | [1440](evidence-ux-2/ux2-library-1440-light.png) | [1440](evidence-ux-2/ux2-library-1440-dark.png) | [390](evidence-ux-2/ux2-library-390-light.png) | [390](evidence-ux-2/ux2-library-390-dark.png) |
| Crear con IA | [1440](evidence-ux-2/ux2-create-1440-light.png) | [1440](evidence-ux-2/ux2-create-1440-dark.png) | [390](evidence-ux-2/ux2-create-390-light.png) | [390](evidence-ux-2/ux2-create-390-dark.png) |
| Tarjetas | [1440](evidence-ux-2/cards-1440-light.png) | [1440](evidence-ux-2/cards-1440-dark.png) | [390](evidence-ux-2/cards-390-light.png) | [390](evidence-ux-2/cards-390-dark.png) |
| Supervisión SQL | [1440](evidence-ux-2/ux2-sql-supervision-1440-light.png) | [1440](evidence-ux-2/ux2-sql-supervision-1440-dark.png) | [390](evidence-ux-2/ux2-sql-supervision-390-light.png) | [390](evidence-ux-2/ux2-sql-supervision-390-dark.png) |
| Drawer SQL | [1440](evidence-ux-2/ux2-sql-drawer-1440-light.png) | [1440](evidence-ux-2/ux2-sql-drawer-1440-dark.png) | [390](evidence-ux-2/ux2-sql-drawer-390-light.png) | [390](evidence-ux-2/ux2-sql-drawer-390-dark.png) |

Biblioteca, creación, tarjetas, gráficos y drawer se comprobaron a
320/390/768/1024/1440 en Light/Dark. Las capturas focales SQL usan 390/1440 y
una cohorte diferente de tres estudiantes; no sustituyen esa matriz completa.
Administración, grupos, asignaciones, reportes compartidos y lista de tutorías
se revisaron a 390/1440 en ambos temas. Algunas capturas enfocan una sección
mediante scroll; no son composiciones ni capturas de página completa.

## Verificación ejecutada

| Área | Resultado de UX-2 |
|---|---|
| .NET y PostgreSQL | **356**: 157 unitarias + 199 integración real, cero fallos/omitidas. Integración: 18 min 11 s. |
| EF / configuración | Modelo sin cambios pendientes; restore locked y Release sin advertencias. Compose válido. |
| HTTP HTTPS | **191** comprobaciones aprobadas en Verify y en la preparación final del navegador. |
| Angular | npm ci, TypeScript y build final de producción aprobados. Bundle inicial 299,90 kB, 83,41 kB estimados transferidos, dentro del presupuesto. |
| Tema / contraste | **80** controles aprobados: colores centralizados y tokens válidos, incluidos gráficos. |
| Smoke | API, OpenAPI, HTTPS, Angular y proxy relativo con Mock aprobados. |
| E2E completo | **137** comprobaciones agrupadas: 23 de comportamiento + 114 capturas/mediciones. Cero errores JavaScript. Conserva los recorridos anteriores y añade UX-2. |
| Focal SQL | **8 capturas adicionales**, separadas del E2E. Cohorte autorizada sin cambios: 3 elegibles, 2 finalizados, 1 sin abrir; 8/12 entregas obligatorias. Cero errores JavaScript. |
| Recuperación | Reinicio real de API: cookie, historial, evidencias y replay sin duplicados conservados. |
| Responsive / teclado | Sin overflow horizontal global observado en las mediciones. Escape, Tab/Shift+Tab y retorno de foco del drawer; menú por rol y reduced-motion; cambio de tema y storage limitado al tema. |
| Integridad UX | Propuestas explícitas, versiones exactas, importación, simulación, publicación, asignación, ayudas, intentos, pausas, revisión y feedback conservados. Cancelar salida mantiene el borrador local; no guarda silenciosamente. |
| Preservación | Maestro con SHA-256 original; sin cambios a backend, migraciones, versiones académicas, .env, permisos o locks. Diff/enlaces y seguridad de publicación revisados. |

Logs privados: artifacts/ux2-verify.log, ux2-e2e-complete.log,
ux2-final-build.log y ux2-sql-visual.json. El reporte original del runner mantiene
su nombre artifacts/iteration-9-e2e.json por compatibilidad con recuperación;
el directorio visual predeterminado pasa a evidence-ux-2 para preservar evidence-9.
Los pases fallidos intermedios no se contabilizan como aprobados. La corrección
de foco se volvió a verificar en el pase completo; la espera del menú conserva
las aserciones de estado y foco, esperando el render de Angular.

## Recursos, límites y operación

SVG de iconos y portada dibujados localmente, fuente de sistema y tokens existentes.
Sin dependencias visuales ni imágenes/fuentes remotas. Los cuatro mockups fueron
aportados como referencias conceptuales; no son assets del producto. Los PNG de QA
contienen únicamente el fixture ficticio; no se publican contraseñas, cookies,
claves, respaldos o estados privados.

npm audit detectó dos paquetes señalados en la cadena de desarrollo conservada:
@angular/build (moderado) por undici (alto). Avisos GHSA-4qv8-2p9j-2685,
GHSA-f72v-6w45-q472 y GHSA-xjj6-f33q-929c. package.json y package-lock son idénticos
a la base. La auditoría sin dependencias de desarrollo informa cero. No se aplica
la degradación a Angular 20.3.41 sugerida por npm. Se registra el riesgo de la
herramienta, sin declarar una auditoría de dependencias sin avisos.

La revisión no constituye certificación WCAG ni auditoría exhaustiva de zoom
nativo/tecnologías de asistencia. No se realizaron estudios con usuarios reales
ni se evaluó calidad curricular de un proveedor real. Las pruebas usan Mock o
simulaciones controladas; no consumen cuota externa. Persisten los bloqueantes
institucionales para datos reales documentados en el piloto.

Stop-PilotDemo limpió únicamente la base QA validada, sus procesos y archivos
privados. API y Angular locales se restauraron con la configuración previa Groq;
se comprobó salud y carga de Angular, sin solicitar generación real. Sin push,
despliegue ni autorización implícita para otra iteración.
