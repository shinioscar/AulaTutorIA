# Iteración 0 — Fundaciones

Fecha: 12 de octubre de 2026. Estado: **completada**. Encargo ejecutado únicamente
en `F:\AulaTutorIA`. Fuente leída íntegramente: documento maestro v1.0, 24
secciones / 550 líneas, sin editar su contenido.

SHA-256 del maestro conservado:
`FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05`.

## Resultado y decisiones

- Git local en main, identidad Git existente; sin remoto ni publicación.
- Solución .NET con Api, Application, Domain e Infrastructure; Angular en Web.
- IAiProvider y DTO mínimos provisionales, Mock determinista sin red y validación
  de configuración al inicio. No hay SDK/adaptadores reales ni generación HTTP.
- API de salud tipada, OpenAPI de desarrollo, ProblemDetails y logs JSON.
- Angular standalone con pantalla de estado, comprobación manual y error de
  conexión; proxy del mismo origen. Sin funcionalidades de futuras iteraciones.
- Compose de desarrollo con PostgreSQL 18.6 (imagen por digest), volumen durable
  y API opcional en perfil app. Puerto del host 15432, interno 5432.
- Versions/lockfiles/CI local, README y AGENTS para continuar en otra conversación.
- No DbContext, migraciones, Identity, seed, endpoints académicos ni runner E2E:
  se incorporarán en las iteraciones correspondientes.

Detalles: `../decisions/ADR-011-fundaciones.md`. Reglas originales: ADR-001 a 010.
Ambigüedades: `../architecture/OPEN_QUESTIONS.md`.

## Verificaciones realizadas

| Comprobación | Resultado |
|---|---|
| `./scripts/Verify.ps1 -Smoke` | Pasa: restore locked, build Release, tests, npm ci, tsc, ng build, Compose config y smoke HTTP. |
| Build .NET | 0 advertencias / 0 errores; 6 proyectos incluyendo pruebas. |
| `dotnet test` | 10 pruebas pasan: 8 unitarias y 2 integración. |
| Mock/contrato | Cancelación, sin eco de entrada ni tokens inventados; JSON no soportado se rechaza. |
| Configuración IA | Groq/Gemini/Ollama/OpenAI/vacío rechazados; arranque real con Groq en contenedor falla como se esperaba. |
| OpenAPI | Solo /api/health documentado; inaccesible en Production; rutas de tutoría inexistentes. |
| Angular | TypeScript y plantillas estrictas; bundle inicial 127.70 kB, transferencia estimada 38.13 kB. |
| npm | 405 paquetes auditados (directos y transitivos), 0 vulnerabilidades reportadas. |
| Smoke local | API y Angular arrancan; proxy /api/health devuelve Mock; procesos del smoke se cierran. |
| `docker compose ... --profile app up -d --build` | Build Linux, restore locked y publish correctos; PostgreSQL healthy y API disponible. |
| PostgreSQL | `SELECT current_database(), version()` confirma aulatutor y PostgreSQL 18.6. No tablas de negocio. |
| Contenedor API | UID/GID 1654 (usuario app), sin root; /api/health devuelve ok/Mock. |
| Navegador real | Edge con Playwright incluido, sin instalar navegador/paquete adicional; escritorio 1440×900, móvil 390×844 y ancho 320. |
| UI e interacción | Botón y Enter verifican conexión; foco visible; fallo de petición muestra mensaje y recuperación restaura Mock. |
| Capturas | Escritorio, móvil y fallo revisados; sin recortes ni desbordamiento horizontal observado. |
| Git / secretos | .env, caches, bin/obj, node_modules y artifacts ignorados; .env.example versionable; diff sin errores de espacios. |

La comprobación de salud de API **no implica** conexión a PostgreSQL. El acceso
a PostgreSQL se verificó por separado mediante psql dentro del contenedor.
Las pruebas de autorización/migraciones de §18.1 no aplican todavía a 0: no hay
usuarios ni persistencia implementados. No se afirma validar requisitos del MVP.

## Evidencia visual

- [Escritorio](evidence/desktop.png)
- [Móvil](evidence/mobile.png)
- [Error recuperable de API](evidence/api-error.png)

Logs locales del smoke en `artifacts/smoke/`; capturas originales en
`artifacts/ui/`. Esos directorios están ignorados; las tres capturas de esta
sección se conservan versionadas para futuras conversaciones.

## Problemas encontrados y solución

1. Docker no estaba activo inicialmente; el usuario lo activó y se completaron
   las verificaciones reales de contenedores.
2. Puerto 5432 ocupado por un proceso existente. Se cambió exclusivamente el
   puerto del proyecto a 15432; no se detuvo ni reconfiguró el servicio existente.
3. Plantilla .NET resolvía Microsoft.OpenApi 2.0.0 con alerta NU1903. Se fijó 2.7.5,
   corregida según GHSA-v5pm-xwqc-g5wc; auditoría y warnings-as-errors siguen activos.
4. `npm exec` para generar Angular falló dos veces con ECOMPROMISED (Lock
   compromised). Se creó una base mínima de Angular y se instaló con npm install;
   npm ci posterior y build pasaron. No se instaló CLI global ni se actualizó Node.
5. Herramienta Playwright predeterminada buscaba Chrome ausente. Se reutilizó Edge
   instalado y Playwright del runtime de Codex mediante require, porque su import
   ESM devolvía un error de exportación. Perfil QA dentro de .cache; navegador cerrado.

## Recomendaciones para Iteración 1

1. Definir primer administrador ficticio y aprobación manual de docentes; el
   registro como Teacher no debe conceder privilegios automáticamente.
2. Formalizar cookies HTTP-only, CSRF, cierre de sesión y política de registro.
3. Incorporar Identity, EF Core 10 y Npgsql compatibles; crear migración inicial
   reproducible y comprobarla contra PostgreSQL con datos ficticios.
4. Modelar alcance de roles, grupos y membresías por organización. Probar accesos
   cruzados Student A/B, Teacher X/Y y restricciones de Admin por recurso.
5. Mantener Mock y límites de alcance. La integración real de IA corresponde a 2;
   tutorías a 3. Consultar OPEN_QUESTIONS antes de concretar políticas no definidas.

## Inventario

El inventario exacto de archivos nuevos se guarda en `FILES.md`. El documento
maestro original se incorpora al repositorio intacto; no es un archivo creado
por esta iteración. `.env` fue creado localmente con contraseña aleatoria y
queda fuera de Git; no se imprime ni se registra su contenido.

Al entregar, API/PostgreSQL de este proyecto y servidor Angular permanecen
activos para revisar la base en http://127.0.0.1:4200. Se cerró el navegador de
QA y los procesos temporales del smoke. Antes de repetir smoke, liberar puertos
5080/4200; las instrucciones de parada están en README.
