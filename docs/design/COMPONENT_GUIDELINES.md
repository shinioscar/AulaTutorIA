# Patrones de componentes — AulaTutor UI-0

Una misma estructura Angular y tokens semánticos para Light/Dark. No duplicar
plantillas, no elegir colores por rol, no usar CSS hex/rgb/hsl en componentes.

## Estructura y navegación

Marca y botón de tema visibles antes y después de autenticación. Enlaces solo a
rutas existentes y capacidades del actor; backend sigue autorizando cada recurso.
La ruta activa tiene subrayado y peso, además de color. En móvil, enlaces ajustan
líneas con áreas táctiles de 44 px. La futura sidebar/drawer debe ser etiquetada,
mostrar ruta activa y gestionar foco/Escape sin ocultar acciones esenciales.

ThemeToggle es un botón nativo: nombre accesible estable «Tema oscuro», aria-pressed
indica si está activo, icono decorativo y etiqueta visible estable «Oscuro», incluida
en el nombre accesible. Enter/Space cambian
el tema. No requiere login ni llama HTTP. La elección persiste por navegador/origen,
no por cuenta: al cambiar de usuario se mantiene la preferencia del dispositivo.

## Tarjetas, botones y formularios

Tarjeta: `--surface`, `--border`, `--radius-large`, `--shadow-card`, espacio 6.
Una acción principal por tarea; secundarias con borde de control y superficie.
La clase `.destructive` se reserva para operaciones destructivas ya autorizadas:
texto específico y confirmación según el flujo, nunca cambiar permisos por diseño.
Hover/active/disabled/focus usan tokens, sin mover el layout ni animar escalas.

```html
<section aria-labelledby="practice-title">
  <h2 id="practice-title">Continuar práctica</h2>
  <p class="note">Describe el objetivo y la siguiente acción con datos existentes.</p>
  <button type="button">Continuar</button>
  <button type="button" class="secondary">Volver al historial</button>
</section>
```

Cada campo tiene label, límites y mensajes comprensibles. Placeholder no sustituye
label. Errores se describen con texto/aria-describedby y pueden usar aria-invalid;
no borrar respuestas pendientes por un 429. Mantener controles de cancelación y
recuperación de la misma solicitud. No almacenar contenido académico o credenciales
en storage por conveniencia visual. Solo la preferencia de tema está autorizada.

Avisos: `.notice`, con variantes info/success/warning/danger. No transformar cualquier
estado de revisión en alarma ni dar falso éxito con verde. Mensajes de backend
conservan su significado; roles, estado de sesión y resultado son conceptos distintos.

## Student: actividad, chat y evidencia

Jerarquía: título/objetivo → actividad actual y chat → ayudas/respuesta → ruta plegable
→ historial/evidencias/reportes. Una consigna actual, conversación legible y campos
cómodos en teléfono. Evitar banners que consuman la altura del chat durante el trabajo.
Usar superficies de tutor/alumno y metadatos discretos, con atribución textual.

El código usa fuente mono y scroll horizontal propio; texto IA por interpolación
segura. No innerHTML, enlaces arbitrarios o resaltador remoto. Pistas/simplificación
conservan la actividad. Resultado Deterministic identifica coincidencia con una regla;
NeedsReview/NotEvaluable y observaciones IA se rotulan expresamente. No inventar
«dominio», una nota o un porcentaje para llenar una barra visual.

Progreso de ruta: objetivos trabajados/total y etiquetas Actual/Pendiente/Trabajado.
Un reporte es distinto del chat; compartir requiere vista previa, destinatario
autorizado y consentimiento. Revocación no se presenta como borrado de copias.

## Teacher: trabajo y revisión autorizada

Prioridades futuras: clases, simulación, grupos, publicación, progreso y evidencias
autorizadas. Separar creación/simulación de revisión de estudiantes. Borrador,
aprobado y publicado necesitan etiquetas y acciones según estados reales; no
construir esos módulos en UI-0. En el producto actual solo Grupos y Reportes compartidos.

Tablas futuras: encabezados, filtros conservados, lectura por objetivo, contenedor
adaptable y datos completos. Una revisión pendiente necesita contexto y evidencias
autorizadas, no una nota IA automática. Ningún acceso implícito al historial privado.

## Admin: operación verificable

Prioridad: usuarios/aprobación, grupos, proveedor/configuración, diagnóstico,
seguridad y, cuando exista backend, consumo/límites/auditoría. Formularios muestran
qué cambia y estados efectivos. Configuración sin secretos; prueba IA solo acción
explícita autorizada con aviso de consumo, nunca una animación/tema que la ejecute.

Liveness API, readiness PostgreSQL, disponibilidad IA y cuota son mediciones distintas.
Sin datos del backend, mostrar «No disponible» o explicar el alcance; no 99.9%,
presupuestos, costos ni contadores inventados. No añadir estadísticas de mockups.

## Estados mínimos y comprobaciones

- Vacío: explica qué falta y una acción existente; no datos de ejemplo presentados como reales.
- Carga: mensaje breve role=status y bloqueo de envío duplicado; mantener contenido confirmado.
- Error/cuota: explicación segura, respuesta pendiente conservada y recuperación explícita.
- Permiso insuficiente: conservar 401/403/404 y sus límites; no eludir por un enlace nuevo.
- Correcto/completado: describir exactamente qué se guardó, sin certificar aprendizaje.

Verificar Light/Dark, teclado/foco, 320/390 px, tablet y escritorio, prefers-reduced-motion,
texto largo/código, storage bloqueado y recarga. Medir contraste en el estado real.
Antes de una pantalla nueva, revisar DESIGN_SYSTEM.md y THEME_TOKENS.md.
