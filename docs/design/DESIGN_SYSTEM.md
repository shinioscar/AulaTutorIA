# AulaTutor IA — sistema de diseño oficial UI-0

Fecha: 2026-10-13. Base visual para Student, Teacher y Admin, después de Iteración 4.
Fuente funcional: maestro v1.0 intacto y ADR del producto. Decisión de temas: ADR-020.

## Identidad y dirección

AulaTutor IA comunica acompañamiento académico, organización y aprendizaje
personalizado. Educativo, confiable y profesional; moderno y cercano sin resultar
infantil. El contenido académico y la acción siguiente tienen prioridad sobre
decoración o métricas. El nombre se conserva igual en ambos temas.

Se revisaron las seis referencias aportadas en `references/`, sin modificarlas:
Student/Teacher/Admin en Light y Dark. Son inspiración visual, no requisitos de
funcionalidades o fuentes de datos. Sus porcentajes de dominio, calificaciones,
presupuestos, uptime, agenda, búsquedas globales y constructor no se trasladan a
la aplicación. Solo pueden mostrarse capacidades existentes con datos verificables.
Las referencias permanecen en documentación; no se incorporan como imágenes del producto.

| Tema | Identidad | Aplicación |
|---|---|---|
| AulaTutor Light | Fondo luminoso, superficies blancas, azul y verde, aire y jerarquía clara | Predeterminado para nuevas visitas, aunque el sistema operativo use Dark |
| AulaTutor Dark | Azul marino, superficies azul pizarra, turquesa y violeta, alto contraste | Elección explícita; degradado/glow discreto solo en un elemento destacado |

No negro puro como fondo, neón sobre texto, animación continua, gamificación
decorativa, ruido gráfico ni paneles cargados de indicadores. Las ilustraciones
aportan contexto educativo; se omiten cuando reducen espacio para leer o conversar.
UI-0 usa un libro/brote SVG decorativo local en Inicio y un marcador de libro en la
marca. No se descarga ninguna fuente ni se instala una librería de iconos.

## Base implementada y alcance

- CSS custom properties semánticas en `src/AulaTutor.Web/src/styles/theme-tokens.css`.
- ThemeService y ThemeToggle globales, atributo `data-theme` en `<html>`.
- Preferencia explícita `aulatutor.theme.v1`, únicamente `light`/`dark`, en localStorage.
- Aplicación temprana en el head, antes de estilos/Angular; CSS define también Light
  por defecto. Lectura/escritura bloqueada conserva una interfaz operativa.
- Cabecera, navegación, marca, botones, formularios, tarjetas, avisos, grupos,
  código, burbujas, ejercicio y reporte consumen tokens compartidos.
- Inicio es la pantalla representativa adaptada: bienvenida académica, accesos
  reales por rol, privacidad y lectura de salud/configuración existente.

No hay componentes duplicados por tema, rutas nuevas de negocio, módulos nuevos,
framework visual, fuentes remotas, métricas inventadas ni cambios de permisos.
Las distribuciones de Tutorías, Grupos, Usuarios y Diagnóstico IA se conservan;
se sustituyen sus colores locales para poder leerlas en ambos temas. El rediseño
de esos flujos y los dashboards completos queda para nuevos encargos.

## Jerarquía y navegación por rol

| Rol | Prioridad de información | Navegación existente en UI-0 | Patrones futuros, aún sin implementar |
|---|---|---|---|
| Student | Actividad actual → chat → objetivo/ejercicio → plan/progreso → resultados | Inicio, Tutorías, Grupos | Separar trabajo activo de historial; sidebar para secciones extensas si se autoriza |
| Teacher | Trabajo docente → grupos → evidencias autorizadas/revisión | Inicio, Reportes compartidos, Grupos | Constructor/simulación/clases/progreso según sus iteraciones, nunca chats privados implícitos |
| Admin | Operación → usuarios/aprobación → grupos → configuración/diagnóstico | Inicio, Administración, Diagnóstico IA, Grupos | Consumo/auditoría/presupuesto solo con contratos y datos existentes, sin uptime/costos ficticios |

En sesión Student, el chat conserva protagonismo y una sola consigna actual.
Progreso significa actividad trabajada, no dominio certificado. Los hechos del
validador y las interpretaciones IA deben seguir separados y rotulados. Estados
de error/cuota/recuperación deben conservar mensajes e intentos confirmados.

Teacher ve únicamente reportes autorizados y con relación vigente. No crear
enlaces de conversación privada ni confundir NeedsReview con una nota automática.
Admin ve metadatos operativos, sin permiso implícito sobre conversaciones/reportes.
«API disponible» describe liveness; no implica readiness PostgreSQL o cuota IA.

## Responsive y accesibilidad

UX-2 amplía la disposición UI-0/UI-1 por encargo explícito (ADR-029): sidebar
con rutas existentes y estado activo en escritorio; por debajo de 1024 px,
menú desplegable en flujo normal con aria-expanded, Escape y retorno de foco.
Marca, identidad y tema conservan orientación. Al elegir una ruta, el foco vuelve
al contenido. El menú filtra capacidades del actor y nunca concede autorización.


Tamaño táctil de botones/enlaces de navegación: mínimo 44 px. Foco visible de 3 px,
teclado nativo, labels de formulario y estados con texto; el color nunca sustituye
una etiqueta. Las tarjetas se reorganizan, las tablas futuras usan contenedor de
scroll etiquetado y el código conserva scroll propio. No ocultar evidencia para caber.

Objetivos: 4.5:1 para texto normal y 3:1 para límites esenciales de controles/foco.
Los bordes suaves de tarjetas son decorativos; formularios usan `--control-border`.
El verde Light de marca no es color de texto pequeño: usar `--secondary-text`.
Se respeta prefers-reduced-motion: transiciones/animaciones se desactivan. Ningún
efecto debe retrasar una respuesta, bloquear un control o dificultar lectura prolongada.

La comprobación de tokens/contraste y la revisión manual no equivalen a una auditoría
WCAG completa. Los componentes nuevos deben verificar su contraste en contexto,
incluidos hover, errores, gráficos reales y fondos compuestos.

Fuentes: [contraste de texto WCAG 2.2](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html),
[contraste de controles](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)
y [prefers-reduced-motion](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@media/prefers-reduced-motion).

## Continuidad

Usar esta guía y COMPONENT_GUIDELINES.md antes de nuevas pantallas. Añadir tokens
solo cuando el significado nuevo lo requiera; no copiar colores del mockup en un
componente. Cualquier dashboard posterior mantiene privacidad y datos verificables,
reutiliza ambos temas y registra excepciones visuales. UI-0 no autoriza Iteración 5.

Rediseño UX-2 implementado: navegación lateral, resúmenes y detalle por rol;
seguimiento y límites de verificación en [UX-2](../iterations/UX_2.md). Las cuatro
referencias `references/ux-2/` orientan composición y jerarquía, sin autorizar sus
funciones ni sus estadísticas ilustrativas. Este encargo amplía la disposición
visual histórica UI-0/UI-1; los contratos académicos y de privacidad se mantienen.
