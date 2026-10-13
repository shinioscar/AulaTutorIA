# Inventario de QA — Iteración 3

QA en Mock con API/BD temporal aislada y Edge/Playwright existente, sin instalar
runner ni usar Groq. Capturas de datos ficticios, nunca contraseñas legibles.

| Requisito/control | Recorrido funcional | Revisión visual |
|---|---|---|
| Tutorías propias/estado vacío | Entrar Student B, listado vacío, actualizar | Escritorio 1440×1000, móvil 390×844 |
| Creación | Tema/objetivo/nivel/repaso directo, crear sin generación | Aviso de privacidad y formulario |
| Conversación | Comenzar, pregunta diagnóstica, intento, ejercicio, Hint/Simplify | Chat, consigna, código/listas y controles |
| Generación/cancelación | Retardar respuesta Mock en navegador, comprobar bloqueo/indicador, cancelar | Estado transitorio y error recuperable |
| Recuperación | Fallo HTTP controlado, refrescar estado sin duplicar | Mensaje y botón de recuperación |
| Pausa/reanudación | Pausar, recargar, reanudar; salir/login y abrir sesión | Estado y acciones correspondientes |
| Cierre | Confirmar finalización, resumen local, lectura posterior | Sin campo de envío; resumen privado |
| Privacidad | Student A/Teacher/Admin no pueden abrir sesión de B | Guard/navegación y respuesta HTTP 404 |
| Seguridad del contenido | Intento con HTML literal y código; ninguna ejecución DOM | Texto escapado y bloque pre, sin overflow de página |
| Accesibilidad | Labels, foco/teclado, estados role=status, textarea y botones | Controles legibles en móvil 320 px |

Exploración adicional: doble clic mientras generación pendiente y recarga/reentrada
sin nuevo envío. Verificar capturas y métricas de ajuste horizontal; el historial
tiene desplazamiento propio y la página móvil puede desplazarse verticalmente.
Estados de fallos simulados deben etiquetarse, sin atribuirlos a proveedor real.

## Resultado ejecutado (2026-10-13)

Flujo completo aprobado: login Student B, vacío, crear, diagnóstico, intento,
ejercicio, pista, simplificación, pausa, recarga/logout/login, reapertura, reanudación
y finalización con resumen legible. Repaso directo y selección Intermediate también
recorridos. Navegación/botones adecuados al estado; Teacher/Admin regresan a Inicio
sin enlace Tutorías y obtienen 404 en recurso privado; Student A obtiene 404.

Se inspeccionaron capturas de escritorio 1440×1000 y móvil 390×844, además de comprobar
ancho 320: sin desbordamiento horizontal, labels y botones legibles. Se ajustaron
altura del historial, desplazamiento a respuesta nueva y orden móvil de conversación.
No hubo errores JavaScript. El texto HTML de prueba quedó literal, sin imágenes
inyectadas ni ejecución. Código SQL y listas se mostraron sin ejecutarse.

Una respuesta HTTP se perdió intencionalmente después de que el backend Mock la
persistiera: GET recuperó un solo par de mensajes, sin reenvío automático. Se retrasó
un GET anterior durante otro envío: la revisión nueva permaneció visible. Se retrasó
la entrega de una respuesta Mock para inspeccionar generación y botones bloqueados.

Para capturas Pending/Timeout se prepararon esos estados en la base temporal aislada,
con intentos ficticios rotulados; cancelar y recuperar llamaron al backend real y Mock.
La recuperación dejó un intento y una respuesta, sin duplicados. Estas capturas son
QA visual de estados preparados, no fallos reales de Groq. Timeout/cancelación/red/cuota
y cercado de resultados están además cubiertos con proveedor controlado en integración.

Evidencias en evidence-3: sessions-empty-desktop, sessions-desktop/mobile,
tutoring-created-desktop, generating-desktop, chat-desktop/mobile, paused-desktop,
pending-staged-desktop, error-staged-mobile, network-error-mobile,
completed-desktop/mobile, direct-review-mobile y access-denied-desktop (PNG).
Solo datos ficticios y metadatos; ninguna contraseña, cookie o clave visible.
