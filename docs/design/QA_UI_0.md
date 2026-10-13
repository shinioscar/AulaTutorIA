# Cierre y QA — AulaTutor UI-0

Fecha: 2026-10-13. Estado: completado después de Iteración 4.

Light predeterminado, Dark explícito, tokens semánticos y documentación oficial.
Inicio es la única pantalla con nueva composición. Cabecera, navegación, botones,
formularios, tarjetas, avisos, conversación, código y reportes usan la misma base;
los demás flujos conservan su estructura. Sin nuevas rutas de negocio, paquetes,
fuentes externas, gráficos artificiales ni cambios de backend o autenticación.
Las seis referencias del usuario se conservan en references/ como inspiración.

## Entorno e inventario

Edge instalado + Playwright del runtime, HTTPS local y PostgreSQL real en una BD
temporal aislada. Cuentas @example.invalid y proveedor Mock, claves externas
retiradas del proceso de QA. Sin editar .env, sin llamadas reales ni datos reales.

| Escenario previsto | Resultado observado |
|---|---|
| Visita nueva con preferencia del SO Dark | Light por defecto; no guarda una selección implícita. |
| Activar Dark | Cambio inmediato; ninguna petición HTTP adicional. |
| Recargar | Recupera Dark; script del head precede estilos/Angular. |
| Teclado y foco | Space/Enter alternan; nombre estable «Tema oscuro» incluye etiqueta «Oscuro»; aria-pressed refleja estado; contorno sólido 3 px. |
| Salto al contenido | Primer Tab enfoca «Saltar al contenido»; Enter enfoca main#content. |
| Otra pestaña | Cambio mediante el botón se sincroniza por storage. |
| Valor almacenado desconocido | Light; no interpreta el valor como código. |
| localStorage denegado | Light inicial; cambio Dark operativo; recarga Light y cero errores de página. |
| Privacidad de almacenamiento | Solo aulatutor.theme.v1; sessionStorage vacío. Ningún dato de usuario/conversación/credencial. |
| Student | Login/logout con CSRF, tema conserva campos de creación, sesión y conversación existentes funcionan; ejemplo SQL exclusivamente Mock. |
| Teacher | Ambos temas en Inicio; Reportes compartidos vacío conserva explicación de autorización; navegación Grupos existente. |
| Admin | Ambos temas en Inicio; usuarios y consulta de configuración Mock; no se pulsa Ejecutar prueba breve. |
| Roles/autenticación | Preferencia persiste al cambiar de cuenta; menús mantienen restricciones de rol. Regresión HTTP verifica CSRF/401/403/404/privacidad. |
| Responsive | Inicio a 1440×1000, 768×1024, 390×844 y 320×844; Admin a 320 px; chat a 390 px; sin overflow horizontal de página. |
| Código/texto | Superficies y texto legibles en ambos temas; código con scroll horizontal propio, sin HTML/ejecución. |
| Movimiento reducido | transition-duration computada 0s con prefers-reduced-motion: reduce. |

Se ajustó el mínimo tipográfico del título de Inicio para que «acompañamiento»
conserve espacio a 320 px. Se mantuvo estable la etiqueta del toggle para que el
nombre accesible incluya el texto visible. Las capturas finales concluyen las
transiciones; no representan estados intermedios de mezcla entre paletas.

## Verificaciones

- `./scripts/Verify.ps1 -Smoke -Http`: aprobado; restore locked, compilación .NET,
  68 pruebas unitarias + 55 de integración = 123, consistencia EF, npm ci,
  TypeScript, tokens/contraste, Angular producción, Compose, diff y smoke HTTPS.
- Recorrido HTTP completo: 75 comprobaciones aprobadas en Mock/PostgreSQL real.
  El posterior SetupOnly prepara fixtures visuales y no sustituye ese recorrido.
- `npm run check:theme`: 70 pares aprobados, mínimos 4.5:1 de texto normal y 3:1
  de controles/foco/progreso; colores centralizados y referencias de tokens válidas.
- Tras los ajustes visuales finales: TypeScript, check:theme y build de producción
  repetidos y aprobados. Backend, contratos, dependencias y migraciones intactos.
- Revisión visual/funcional de los tres roles: cero errores de página observados.

Evidencia privada reproducible en artifacts/verify-ui-0.log y artifacts/ui-0/;
no se versionan logs, contraseñas, cookies ni certificados.

## Capturas finales

19 capturas en [evidence-ui-0](evidence-ui-0/), todas con datos ficticios:

| Pantalla | Light | Dark |
|---|---|---|
| Inicio Student escritorio | [Light](evidence-ui-0/home-light-desktop.png) | [Dark](evidence-ui-0/home-dark-desktop.png) |
| Inicio Student móvil | [Light](evidence-ui-0/home-light-mobile.png) | [Dark](evidence-ui-0/home-dark-mobile.png) |
| Inicio Student tablet | [Light](evidence-ui-0/home-light-tablet.png) | [Dark](evidence-ui-0/home-dark-tablet.png) |
| Inicio Teacher | [Light](evidence-ui-0/teacher-home-light.png) | [Dark](evidence-ui-0/teacher-home-dark.png) |
| Inicio Admin | [Light](evidence-ui-0/admin-home-light.png) | [Dark](evidence-ui-0/admin-home-dark.png) |
| Chat con código | [Light](evidence-ui-0/student-chat-light.png) | [Dark](evidence-ui-0/student-chat-dark.png) |

Además: Inicio a 320 px, formulario Student, chat móvil, reportes Teacher móvil,
Inicio Admin móvil, usuarios Admin y configuración IA móvil.

## Límites y continuidad

Estos controles no certifican una auditoría completa WCAG. La revisión visual de
reportes docentes usa el estado vacío; el flujo completo de evidencias/reportes y
privacidad está cubierto por la regresión de Iteración 4. No se rediseñan todos
los dashboards ni se implantan sidebar/drawer, seguimiento del SO o módulos futuros.

El documento maestro conserva su SHA256 original. El entorno temporal de QA se
elimina al cerrar; la API local vuelve a Groq sin generación adicional. UI-0 no
autoriza avanzar a Iteración 5.
