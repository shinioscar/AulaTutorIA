# ADR-020 — Temas globales y sistema visual AulaTutor UI-0

Fecha: 2026-10-13. Estado: implementado y verificado.
Encargo UI-0 después de Iteración 4. No modifica el maestro ni autoriza Iteración 5.

CSS custom properties semánticas en un archivo fuente. Light predeterminado;
Dark explícito mediante `data-theme` en html. Un servicio Angular singleton maneja
signal/estado y un componente nativo ThemeToggle independiente de AuthService.
El nombre accesible del toggle es estable e incluye la etiqueta visible «Oscuro»;
aria-pressed refleja Dark, con icono decorativo y superficie de selección.

Se autoriza únicamente `aulatutor.theme.v1` en localStorage (light/dark). No se
añaden credenciales, usuarios, conversación ni roles. La preferencia corresponde
al navegador/origen y persiste entre cuentas/logout. Valores desconocidos → Light;
storage denegado → cambio en memoria sin bloquear aplicación. Evento storage
sincroniza otras pestañas y al limpiar preferencias vuelve a Light. No seleccionar
Dark por el SO; una futura opción System necesita contrato/migración de preferencia
explícitos, listener matchMedia y pruebas. El tipo Theme/servicio concentran esa extensión.

Un script mínimo estático en head aplica la preferencia antes del primer render.
Comparte la clave/normalización con el servicio y no hace HTTP. CSS tiene fallback
Light y color-scheme por tema para controles nativos. Una política CSP futura deberá
permitir ese script con hash/nonce o trasladarlo a recurso local bloqueante; no
introducir unsafe-inline general. UI-0 no modifica middleware o permisos.

Los colores locales de conversación, código y reportes pasan a tokens; sus layouts
y lógica permanecen. Inicio demuestra la dirección visual con accesos por rol y
lecturas de salud existentes. Se revisaron seis referencias locales, conservadas
intactas solo en docs. Sin bibliotecas visuales/fuentes remotas/nuevas dependencias,
gráficos falsos, métricas inventadas o rutas nuevas. Solo Inicio recibe composición nueva.

Tipografía de sistema, escala rem/espacio, controles de 44 px, focus visible, contraste
AA objetivo y reduced-motion. Verde Light usa variante textual; bordes decorativos
y bordes esenciales se separan. Guard de 70 pares de contraste/tokens integrado en
Verify.ps1, complementado por QA visual/funcional con cuentas ficticias por rol en BD Mock aislada.
123 pruebas y 75 comprobaciones HTTP aprobadas; 19 capturas en evidence-ui-0.
Resultados y límites de revisión en docs/design/QA_UI_0.md.
La comprobación no certifica una auditoría completa WCAG.

Guías oficiales en docs/design/DESIGN_SYSTEM.md, THEME_TOKENS.md y
COMPONENT_GUIDELINES.md. Paneles completos, sidebar/drawer, opciones System,
analytics y módulos docentes quedan pendientes de autorización posterior.
