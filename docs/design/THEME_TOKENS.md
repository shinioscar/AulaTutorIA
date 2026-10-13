# Tokens de AulaTutor Light / Dark

Fuente ejecutable: `src/AulaTutor.Web/src/styles/theme-tokens.css`. Los valores
literales de color se definen únicamente allí. Componentes usan `var(--token)`.
Nombres semánticos estables, no nombres de color como blue-500 ni valores por rol.

## Paleta y controles

| Token CSS | Light | Dark | Uso |
|---|---|---|---|
| --background | #F6F9FC | #081426 | Fondo de aplicación |
| --surface | #FFFFFF | #101F35 | Tarjeta/cabecera/formulario |
| --surface-secondary | #EDF4FA | #172940 | Regiones secundarias |
| --surface-hover | #F0F5FD | #203650 | Hover de control secundario/tarjeta |
| --text-primary | #10243A | #EAF3FF | Lectura principal |
| --text-secondary | #52667A | #A9BED5 | Notas, metadatos, placeholder |
| --primary | #1769E0 | #73ABFF | Acción/enlace principal |
| --primary-hover | #1259C4 | #9FC5FF | Hover de botón principal |
| --primary-active | #0E4FAA | #5597EF | Botón presionado |
| --on-primary | #FFFFFF | #081426 | Texto sobre botón principal |
| --secondary | #0E9387 | #4BD7C3 | Acento educativo/progreso |
| --secondary-text | #08786F | #4BD7C3 | Variante apta para texto pequeño |
| --accent | #6F5CE8 | #AD91FF | Acento violeta ocasional |
| --border | #DCE6EF | #31445E | Separación decorativa |
| --control-border | #6B8196 | #708BA6 | Límite esencial de campos/botones secundarios |
| --focus | #1769E0 | #73ABFF | Contorno de foco, 3 px/offset 3 px |
| --disabled-background | #EDF4FA | #172940 | Control inactivo |
| --disabled-text | #52667A | #A9BED5 | Etiqueta inactiva legible |

La superficie hover Light se aclaró respecto al primer borrador: la combinación
azul/enlace sobre #E7EFF8 quedaba en 4.379:1. #F0F5FD supera 4.5:1. Los bordes
decorativos no se usan como único identificador de controles; el borde de campo
se oscurece expresamente. El verde de marca se conserva y tiene variante textual.

## Semántica

| Familia | Foreground Light / Dark | Fondo Light / Dark | Regla |
|---|---|---|---|
| --success / --success-surface | #08786F / #4BD7C3 | #E8F7F2 / #112F33 | Confirmación, resultado rotulado; no implica dominio |
| --warning / --warning-surface | #8C4B00 / #F3C36D | #FFF5E5 / #312818 | Cuota, pendiente, espera o revisión |
| --danger / --danger-surface | #B4233D / #FF8FA3 | #FFF0F3 / #351F32 | Error/acción destructiva |
| --info / --info-surface | #145BC0 / #9AC2FF | #EBF3FE / #172D4A | Información/alcance orientativo |

`--danger-hover` y `--on-danger` completan botón destructivo. Usar texto/icono junto
al estado, nunca rojo/verde aislado. Disabled conserva etiqueta, no opacidad sobre
toda una tarjeta que podría reducir contraste de información importante.

## Conversaciones, código y visualizaciones

- --chat-background, --chat-tutor, --chat-student y --chat-student-border: distinguen
  conversación, tutor y alumno con superficies tranquilas. Sin glow en el texto.
- --code-background / --code-text: bloque mono; --code-inline-background /
  --code-inline-text: fragmento dentro de párrafo/lista. Renderizado seguro existente.
- --progress-track / --progress-fill: alias a superficie secundaria/acento verde.
  Mostrar denominador y significado del avance; ningún porcentaje de dominio ficticio.
- --chart-primary / --chart-secondary / --chart-accent: colores futuros para datos
  reales. Añadir etiquetas/patrones y verificar contraste antes de implementar gráficos.
- --gradient-hero: degradado discreto en bienvenida; --shadow-featured limita el
  halo Dark a ese elemento. --shadow-card es la sombra normal, sin animación.

## Escalas comunes

| Familia | Valores / criterio |
|---|---|
| --space-1/2/3/4/5/6/8/10/12/16 | 4, 8, 12, 16, 20, 24, 32, 40, 48, 64 px (rem) |
| --font-sans | Segoe UI Variable, Segoe UI, system-ui, fallbacks; sin descarga |
| --font-mono | ui-monospace, Cascadia Code, Consolas, monospace |
| --font-xs/small/body/large/heading/display | Metadatos → lectura → título, escalas rem/clamp |
| --weight-medium/semibold/bold/display | 500 / 600 / 700 / 800 |
| --line-body / --line-heading | 1.65 / 1.15 |
| --radius-small/medium/large/feature/pill | 6 / 10 / 16 / 24 px / cápsula |
| --icon-small/medium/large | 16 / 20 / 24 px |
| --target-min | 44 px; mínimo de área de controles compartidos |
| --layout-max / --layout-form | 72rem / 34rem |
| --duration-fast / --duration-standard | 120 / 180 ms; solo respuesta visual breve |
| --ease-standard | cubic-bezier(.2,0,0,1) |

Las dimensiones históricas de pantallas específicas se conservan cuando cambiarlas
supondría rediseño fuera de UI-0. Los componentes nuevos usan estas escalas.

## Comprobación

`npm run check:theme` ejecuta `scripts/verify-theme.mjs`: 35 pares en cada tema,
comparación sin redondear con 4.5:1/3:1, colores centralizados y tokens referenciados
existentes. `node scripts/verify-theme.mjs --report` guarda resultados privados
reproducibles en artifacts/ui-0/contrast.json. Integrado en Verify.ps1.

| Ejemplo foreground/background | Light | Dark |
|---|---|---|
| text-primary / surface | 15.72:1 | 14.78:1 |
| text-secondary / surface-secondary | 5.34:1 | 7.71:1 |
| on-primary / primary | 5.08:1 | 7.90:1 |
| secondary-text / surface-secondary | 4.82:1 | 8.27:1 |
| control-border / surface-secondary | 3.63:1 | 4.16:1 |
| focus / surface-secondary | 4.58:1 | 6.30:1 |

Estas mediciones de tokens no sustituyen pruebas de contenido real, teclado,
contraste compuesto ni una auditoría completa de accesibilidad.
