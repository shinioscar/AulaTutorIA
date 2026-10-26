# Acceso local privado con Tailscale

Modo de desarrollo para el piloto ficticio, autorizado el 26/10/2026. Conserva
Angular y API en loopback, PostgreSQL y el proveedor local configurados. No aplica
migraciones, crea usuarios ni consume IA al iniciar. No es un despliegue de
producción ni habilita datos reales.

El recorrido es navegador → HTTPS Tailscale Serve → HTTPS Angular localhost:4200
→ proxy relativo `/api/**` → HTTPS API localhost:7080. Serve presenta el
certificado del nombre `*.ts.net`; `https+insecure` se limita al salto de loopback
con el certificado de desarrollo. No se deshabilita la validación TLS del cliente,
se abre CORS ni se cambia la autenticación de la aplicación.

## Iniciar

Requisitos: Tailscale conectado con DNS/HTTPS habilitados, PostgreSQL del proyecto
activo, preparación local completada, dependencias existentes y API compilada en
Release. El script usa PowerShell 7 y Tailscale instalado; no instala componentes,
compila ni modifica `.env`. No ejecutar verificaciones que ocupen estos puertos
mientras el entorno esté activo.

Terminal 1, desde la raíz, sin cargar secretos del backend en esta terminal:

```powershell
. ./scripts/Initialize-Environment.ps1
Set-Location src/AulaTutor.Web
npm.cmd start -- --hmr=false --live-reload=false
```

Terminal 2, desde la raíz, con los puertos API 5080/7080 libres:

```powershell
./scripts/start-tailscale.ps1
```

La web desactiva recarga automática/HMR en este modo: las actualizaciones se ven
al refrescar manualmente y no dependen de un WebSocket de desarrollo por el proxy.

El script deriva el origen HTTPS del equipo conectado y lo fija para el retorno
OAuth mediante `PersonalAi__PublicOrigin` y `PersonalAi__AllowedOrigins__0` solo
en el proceso. No deriva retornos desde cabeceras del navegador. En este modo,
usar la URL Tailscale también en el equipo anfitrión para conectar proveedores
personales. Volver al arranque local habitual en una terminal nueva restablece
el origen predeterminado localhost. No se probó autorización externa real.

Serve se configura en segundo plano y conserva sus otras rutas; el script rechaza
una raíz ocupada por otro destino, TCP incompatible o Funnel en ese puerto.
No habilita Funnel. La API queda en primer plano y Angular en su propia terminal.
El equipo debe permanecer encendido y ambos procesos activos; no se instala
arranque automático de Windows.

En el dispositivo remoto, conectar Tailscale a la misma tailnet con acceso permitido
por su política y abrir la URL impresa. Las cuentas, roles y permisos de AulaTutor
siguen siendo necesarios. La IP Tailscale sola no sustituye el nombre HTTPS.

## Consultar y detener

```powershell
tailscale serve status
# Detener únicamente esta publicación HTTPS:
tailscale serve --https=443 off
```

Detener API y Angular con Ctrl+C en sus terminales. No usar `serve reset`, Funnel,
limpieza de bases ni eliminación de volúmenes para detener este entorno.
Serve en segundo plano conserva su configuración al reiniciar Tailscale; tras un
reinicio del equipo hay que iniciar de nuevo API y Angular.

Documentación oficial: [Tailscale Serve](https://tailscale.com/docs/features/tailscale-serve).

## Comprobación del 26/10/2026

Se comprobó HTTPS con validación normal del certificado desde el anfitrión hacia
su nombre Tailscale: documento Angular y `/api/health` 200, emisión CSRF 200,
endpoint privado preparado 401 sin sesión y login sin CSRF rechazado con 400.
Con token/cookies válidos, una cuenta ficticia inexistente recibió 401: el proxy
conserva la validación CSRF y el rechazo de credenciales. Cookies CSRF Secure y
SameSite Strict; la antiforgery también HttpOnly. El callback sin transacción
devolvió 302 al origen Tailscale fijo con rechazo, sin intercambio externo.

Edge cargó `/login` como contexto seguro; se revisó la captura y el formulario.
Sin errores de WebSocket tras desactivar recarga; los avisos HTTP observados
fueron el 401 esperado de `/api/me` anónimo y el favicon 404 heredado. Se verificó
el rechazo del script ante puertos API ocupados, sintaxis PowerShell, enlaces,
diff, revisión de secretos y hash del maestro. No se repitió la batería completa
de aplicación: no cambiaron backend, frontend ni sus paquetes. No se ejecutaron
inferencias ni se crearon cuentas. Acceso desde otro dispositivo de la tailnet
pendiente de comprobación manual.
