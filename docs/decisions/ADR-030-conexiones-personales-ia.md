# ADR-030 — Conexiones personales, custodia y OAuth PKCE

Fecha: 2026-10-23. Estado: aceptada; implementación y QA simulada verificadas
el 2026-10-24 por BYOK-1. Autorización externa real pendiente.
Amplía ADR-014/017/021/025, sin modificar el maestro ni autorizar datos reales.

Teacher aprobado y Student activos administran únicamente sus conexiones. Admin
no obtiene acceso a secretos ajenos. Conexión personal es opcional; usuarios sin
selección conservan el proveedor institucional. Desconectar exige elegir después
otra conexión o el institucional, sin fallback implícito.

Credenciales cifradas mediante un anillo independiente Data Protection, propósito
ligado a propietario/conexión/revisión. DPAPI en Windows; Linux exige protector
de certificado externo y almacenamiento privado. Sin esa custodia, BYOK falla
cerrado y el resto continúa. Lecturas devuelven estado, modelo y huella parcial;
nunca secreto, ciphertext ni datos OAuth. Auditoría mínima append-only.

Un vínculo inmutable identifica propietario, conexión/revisión, proveedor, modelo,
capacidades y aceptación de pago. Sesiones, operaciones docentes y simulaciones
guardan el vínculo al crearse; el institucional heredado conserva vínculo nulo.
Reintentos y evaluación agrupada usan ese vínculo. Revocación/rotación impiden
admisión y confirmación tardía, serializadas con el lock de reservas. Ninguna
transacción permanece abierta durante HTTP.

OAuth PKCE S256 usa state impredecible, vencimiento y consumo único, backend con
verifier cifrado y retorno fijo configurado por operador. Cookie principal Strict
intacta; cookie auxiliar Lax Secure/HttpOnly restringida a la transacción. Callback
sin secretos en URL final y finalización autenticada/CSRF ligada a sesión/stamp.
Cambio de cuenta/logout invalida intentos pendientes. Popup de interacción real,
redirección completa como alternativa y en móvil; mensajes entre ventanas sin
secretos y con comprobación de origen/fuente.

OpenRouter/Anthropic usan REST en hosts fijos, transporte/admisión existentes y
validadores locales. Catálogo OpenRouter con TTL y metadatos de precio/capacidad;
sin precio conocido no se ofrece como gratis. Solicitudes gratuitas restringen
precio a cero, sin fallback. Pago explícito por modelo, sin compra automática.
Límites personales separados del institucional; agregación conservadora por
usuario/proveedor y proveedor compartido evita reiniciar presupuesto al rotar key.
Cuenta externa desconocida permanece desconocida; no se promete cuota disponible.

QA con proveedores/OAuth simulados y PostgreSQL real aislado. Comprobación con
cuenta externa real y requisitos institucionales para datos reales pendientes.
Auditoría, evolución y resultados en [BYOK-1](../iterations/BYOK_1.md).

## Catálogo gratuito y corrección posterior — 24/10/2026

Por encargo explícito, los catálogos personales expuestos al navegador solo ofrecen
modelos Free + PriceKnown; la UI elimina la opción de pago y su consentimiento.
Los catálogos locales sin precio cero verificado no ofrecen modelos. Se conserva
el protocolo previo y los vínculos históricos; no se convierten ni retargetean.
Límites de tokens desconocidos deshabilitan ese modelo sin abortar todo el catálogo
OpenRouter. Detalles en [PERSONAL_AI](../architecture/PERSONAL_AI.md) y
[QA del catálogo](../iterations/QA_OPENROUTER_CATALOG.md).
