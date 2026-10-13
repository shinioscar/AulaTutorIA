# Iteración 2 — Proveedores IA y diagnóstico administrativo

Fecha: 2026-10-13. Estado: completada. Alcance del nuevo encargo adjunto:
Groq, Gemini, Ollama local y Mock, con ajustes adicionales de contraseña y búsqueda
por correo. No se avanzó a Iteración 3. Documento maestro intacto, SHA256:
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Entregables

- Contrato Application sin SDK: roles definidos, sistema/conversación/texto,
  JSON/subconjunto Schema, identificación, uso real cuando existe, cancelación,
  capacidades, errores tipados y forma de eventos para streaming futuro.
- GroqAiProvider, GeminiAiProvider y OllamaAiProvider funcionales por REST oficial.
  Serialización/adaptación y normalización independientes de la lógica académica.
  IAiProvider se selecciona por configuración explícita, sin fallback.
- MockAiProvider conserva respuesta/no red/no uso inventado/no JSON y sus 8 pruebas.
  Los 5 casos de configuración externa incompleta siguen fallando antes de llamar.
- Transporte: timeout total, concurrencia por proceso, límites de entrada/salida,
  respuesta acotada, reintentos controlados 429/503, Retry-After, sin redirecciones ni
  exposición de cuerpos de error, claves o encabezados. Telemetría de metadatos.
- Diagnóstico Admin con actor vigente de organización piloto; GET sin red y POST
  con CSRF/HTTPS/prompt fijo/128 tokens/3 intentos por minuto. Pantalla Angular con
  capacidades, respuesta, duración, uso reportado o ausencia y errores seguros.
- Contraseñas 5–128 para Admin/Teacher/Student, sin composición obligatoria;
  recomendaciones en registro/bootstrap. Hashes existentes no se modifican.
- Buscador parcial por correo del selector de miembros, sobre toda la organización
  antes de paginar. Paginación preserva filtro; nueva búsqueda limpia selección;
  muestra sin resultados y correo seleccionado completo también en móvil.
- README, AGENTS, arquitectura, plan, preguntas abiertas, ADR-014/015, inventario
  QA y evidencias actualizados. Archivos detallados en FILES_2.md.

No paquetes nuevos, cambios del modelo EF ni nuevas migraciones: los cambios de
política son configuración de Identity y los proveedores no persisten datos.
No OpenAI, tutor académico, clases, planificación, motor pedagógico o evaluación.

## Configuración y capacidades

Leer README «Configurar IA» y ADR-014 para variables de Groq/Gemini/Ollama,
catálogo conservador de modelos, restricciones de URL y JSON Schema. Claves
exclusivamente backend/env privado, nunca Angular. Modelos seleccionados explícitos,
sin descarga/descubrimiento automático. No presuponer disponibilidad/acceso de un
modelo por su aparición en el catálogo. El ID normalizado es el modelo configurado;
no se confía en metadatos arbitrarios de la respuesta externa.

Formato portable: objeto raíz; type/properties/required/additionalProperties=false,
items/enum/description. Validación local recursiva; palabras clave desconocidas,
JSON incompleto, tipos/campos/enum incompatibles o truncamiento producen error.
Mock continúa solo texto. La excepción NotSupportedException original del Mock
para JSON se conserva por compatibilidad con sus pruebas; los adaptadores HTTP
usan AiProviderException. StreamingTransport no equivale a streaming implementado.

Las políticas aprobadas de Identity, CSRF, revocación, autorización por pertenencia
y privacidad se conservan salvo el cambio explícito de contraseña (ADR-015).

## Verificación realizada

| Comprobación | Resultado |
|---|---|
| ./scripts/Verify.ps1 -Smoke -Http | Pasó: restore locked, build, herramientas, tests, EF, npm ci/check/build, Compose, diff, smoke y HTTP |
| Compilación .NET Release |0 errores, 0 advertencias |
| Unitarias |46/46, 0 omitidas;11 previas conservadas |
| Integración |22/22, 0 omitidas;13 previas conservadas |
| PostgreSQL real | Migración dos veces, hashes/cookies/roles/grupos/persistencia; bases aisladas eliminadas |
| Adapters HTTP loopback |6 casos (éxito/cuota ×3 proveedores), contrato de ruta/body/uso/errores, sin DNS externo |
| Configuración y errores | Selección, Mock con clave sin selección, falta de credencial sin llamadas, timeout/cancelación/conexión, 401/403/429/5xx, Retry-After, concurrencia, JSON/esquema/respuesta inválida |
| Permisos IA | Anónimo 401, Student/Teacher 403, Admin 200; CSRF400 y límite 429 |
| Contraseñas | Bootstrap/login Admin de 5 minúsculas; Student/Teacher aceptan 5 y rechazan 4; backend con Identity real |
| Búsqueda | Más de 50 cuentas PostgreSQL; filtro previo a páginas, trim/mayúsculas, % literal, límite 254 y rechazo Student |
| HTTP HTTPS real |32 comprobaciones con Mock, cookies/CSRF, grupos, aprobación, búsqueda y diagnóstico, sobre BD temporal |
| Angular | TypeScript estricto y build producción; repetidos después del ajuste visual del correo seleccionado |
| Dependencias | NuGet incluidos transitivos: ninguna vulnerabilidad reportada; npm ci auditó 409 paquetes, 0 vulnerabilidades |
| Seguridad local | .env/certs/cache/artifacts ignorados; revisión de secretos en fuentes; maestro sin cambios |
| QA navegador | Edge/Playwright con cuentas ficticias: diagnóstico/configuración/prueba/refresco, registro/login mínimo, guard Student, búsqueda/asociación/Enter/sin resultados |

Groq/Gemini/Ollama **solo se probaron contra transportes y servidores HTTP
simulados**. Mock se probó en la aplicación real. No se ejecutó smoke con claves
reales, no se consumieron cuotas, ni se instalaron Ollama/modelos. La prueba real
opcional está disponible mediante acción explícita Admin con proveedor configurado.

QA visual: escritorio 1440×1000, móvil 390×844 y ajuste de 320 px sin scroll horizontal.
Ocho capturas en evidence-2; inventario QA_2.md. El error de cuota visual se simuló
con intercepción del navegador y se etiqueta como tal, sin atribuirlo a una llamada
externa real. Registro usa campo password oculto; no hay secretos en capturas.
Sin errores de JavaScript. Se cerró navegador y se eliminó BD/estado privado QA.
API/web se restauraron para uso local con la configuración principal; no se crearon
cuentas QA ni se modificaron usuarios de la base principal.

## Problemas y límites

- Una prueba esperaba tres POST válidos después de uno sin CSRF; el límite cuenta
  todos los intentos autorizados, incluso CSRF fallido. Se corrigió la expectativa,
  manteniendo la limitación protectora. La suite final pasa sin omisiones.
- La documentación vigente de Gemini añade responseFormat.text para GenerateContent;
  se implementó ese formato. Gemini 2.5 conserva JSON pero limita acceso a cuentas previas;
  también se admite 3.8 Flash documentado. Ollama documenta gpt-oss para salida
  estructurada; modelos desconocidos se mantienen conservadoramente solo texto.
- El selector nativo acorta correos largos en móvil: se añadió texto separado con
  el correo completo seleccionado, verificado de nuevo en Angular/navegador.
- Hubo esperas de QA sobre options nativos ocultos y navegación antes de completar
  el cambio de formulario. Se corrigió sincronización del navegador; no eran fallos
  de aplicación. Ningún test se sustituyó por un resultado inventado.
- El aviso Node HTTP/2 sobre status message del servidor de desarrollo es informativo;
  no impidió proxy, autenticación, CSRF ni validación HTTP. No se ocultaron warnings.
- Capacidad declarada no garantiza disponibilidad, semántica de respuesta, presupuesto
  ni acceso del usuario. Un esquema válido tampoco implica corrección pedagógica.
- Cuotas/reintentos son por proceso; no hay contabilidad distribuida ni auditoría
  persistente de consumo. No hay fallback externo ni subida de datos académicos.

## Preparación para Iteración 3

Antes de desarrollar tutoría privada, precisar ciclo de sesión/pausa/cierre,
retención y consentimientos; mantener propietario como frontera de autorización.
Orquestar casos de uso sobre IAiProvider y prompts versionados, nunca desde Angular.
Definir contratos pedagógicos y validadores adicionales con pruebas, en lugar de
asumir que JSON Schema valida la calidad de tutoría. Ampliar catálogo o esquema
solo con fuentes oficiales y casos de aceptación; planear costos y cancelación.
Una prueba real breve de proveedor será opcional y explícita, sin datos reales.
No avanzar automáticamente a 3 ni desarrollar aún funciones de 4–8.
