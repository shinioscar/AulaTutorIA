# BYOK-1 — Conexiones personales de IA

Estado: implementación y QA simulada completadas, por encargo explícito. Base: UX-2 y afinado
`186f13a`; Git limpio al comenzar. Maestro inmutable. QA exclusivamente ficticia,
con PostgreSQL aislado y transporte simulado; ninguna inferencia externa autorizada.

## Auditoría previa y plan

Se revisaron IAiProvider y los adaptadores Mock/Groq/Gemini/Ollama, composición DI,
HttpClient sin cookies/redirecciones/loggers, transporte con deadline y admisión
por intento físico, reservas PostgreSQL, Identity/cookies/CSRF/security stamp,
Data Protection, creación/recuperación de tutorías, asignaciones, propuestas,
simulación docente y evaluación agrupada. La sección existente es compartida por
Teacher/Admin; Student necesita una entrada propia con autorización de backend.

Se reutilizan los motores académicos, comparadores, validación JSON local,
transporte y reservas. Se agregan conexiones cifradas por propietario, selección
explícita, transacciones OAuth y vínculos inmutables de inferencia. Los vínculos
se guardan en sesión, propuesta y simulación; cambiar selección afecta creaciones
posteriores. Una conexión retirada o sustituida invalida nuevas admisiones y
resultados tardíos de su revisión anterior. No se cambia el proveedor de un replay.

La custodia usa un anillo Data Protection independiente, DPAPI en Windows y
certificado externo con clave privada y directorios privados en Linux. Sin
custodia válida se bloquea exclusivamente BYOK, conservando el proveedor
institucional. La recuperación requiere respaldar por separado BD, anillo y
protector externo; no hay clave maestra en BD ni cifrado casero.

OAuth conserva la cookie principal Strict. Una cookie auxiliar Lax, Secure y
HttpOnly vincula el retorno a una transacción breve. El callback valida y consume
el código en servidor, elimina parámetros al redirigir y la finalización exige
sesión original, security stamp y CSRF. Verifier/código se cifran en backend;
el navegador solo recibe una URL oficial y estados saneados. El intercambio es
único; un resultado incierto exige reiniciar autorización, sin reintento secreto.

Reservas personales se distinguen de las institucionales, conservando cargos
inciertos, leases y enfriamiento. Se agregan propietario, conexión/revisión,
huella, cuenta externa cuando se conoce, modelo y operación. Los límites compartidos
conservadores no presuponen cuotas independientes por cada clave.

OpenRouter usa catálogo oficial validado con TTL, capacidades estructuradas y
precio verificado. Gratis es predeterminado y el envío gratuito incluye techo de
precio cero; pago exige consentimiento por modelo. No se habilitan routers,
fallbacks, reparación remota ni hosts arbitrarios. Gemini/Groq reutilizan REST;
Anthropic agrega Messages con salida estructurada y versión oficial fijada.

Bloques secuenciales: A auditoría/ADR; B custodia/persistencia/permisos; C vínculos
y cuotas; D OAuth/OpenRouter; E manuales/Anthropic; F pantalla compartida; G pruebas,
recuperación, documentación y revisión visual. Cada bloque requiere compilación
y pruebas focales antes de la verificación integral.

Riesgos principales: regreso cross-site con Strict, cambio de usuario/pestañas,
replay de OAuth, revocación durante HTTP, rotación/pérdida del protector, cambios
de catálogo/precio y límites de cuenta externa desconocidos. Se comprueban con
simulaciones de HTTP y PostgreSQL real; la autorización con una cuenta real queda
expresamente pendiente y no se presenta como verificada por QA.

## Fuentes oficiales consultadas (2026-10-23)

- [OpenRouter PKCE](https://openrouter.ai/docs/guides/overview/auth/oauth): S256,
  state, callback fijo e intercambio `/api/v1/auth/keys`.
- [Modelos OpenRouter](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties),
  [salida estructurada](https://openrouter.ai/docs/guides/features/structured-outputs)
  y [enrutamiento/precios](https://openrouter.ai/docs/guides/routing/provider-selection).
- [Gemini API keys](https://ai.google.dev/gemini-api/docs/api-key) y
  [Groq Structured Outputs](https://console.groq.com/docs/structured-outputs).
- [Claude API](https://platform.claude.com/docs/en/api/overview),
  [Models](https://platform.claude.com/docs/en/api/models/list) y
  [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs).

## Verificación y cierre

Cierre de implementación y QA simulada: 2026-10-24. Verificación ejecutada:

| Comprobación | Resultado |
|---|---|
| Verify.ps1 -Smoke -Http | Satisfactorio: restore con locks, Release, PostgreSQL, EF, TypeScript, temas, Angular, Compose, diff, smoke HTTPS y contratos HTTP. |
| Pase completo .NET sobre vínculos y permisos finales | 414 pruebas: 173 unitarias y 241 de integración con PostgreSQL real; cero fallidas u omitidas. |
| Focal final de seguridad BYOK | 43 pruebas de integración aprobadas, sin omisiones, después de acotar también la lectura HTTP del intercambio OAuth. Incluye respuesta de 200 kB rechazada antes de cargarla, limpieza de secretos temporales, PKCE, propiedad, revocación, cuotas y persistencia. |
| HTTP | 191 comprobaciones sobre API HTTPS/PostgreSQL aislados. |
| Angular/temas | TypeScript estricto, 80 comprobaciones de tema/contraste, build de producción y smoke satisfactorios. Se repitieron TypeScript, temas y build después del ajuste de foco/desplazamiento. Inicial: 300,77 kB / transferencia estimada 84,26 kB. Pantalla BYOK diferida: 25,41 kB. |
| Edge/Playwright | 266 comprobaciones: 34 recorridos funcionales y 232 capturas/mediciones responsive, incluidas 75 capturas BYOK. Cero errores JavaScript. |
| Reinicio real de API | Historial y metadatos conservados, credencial descifrada tras reiniciar, continuación con OpenRouter simulado y replay sin duplicados. También se preservaron las sesiones académicas y la cookie existentes. |
| Logs | Cuatro logs QA revisados, sin marcadores de secretos. Tres regresiones focales del lector: archivo vacío, escritor activo y rechazo de marcador ficticio. |
| Dependencias | Sin paquetes, SDK ni locks nuevos. Auditoría .NET sin vulnerabilidades reportadas; npm producción: cero. Persisten dos avisos de desarrollo: @angular/build (moderado) y undici (alto). |
| Publicación y entorno | check-publish-safety sobre contenido e historial sin coincidencias de secretos; maestro con SHA256 original y evidencia binaria ficticia revisada. Migración aditiva explícita con conteos preservados. API y Angular restaurados con Groq, sin inferencia; base/estado privado QA retirados. |

El pase completo de .NET precede únicamente al ajuste de lectura acotada de OAuth;
la batería focal de 43 casos se ejecutó después sobre ese código. No se atribuye
al pase completo una ejecución posterior a ese ajuste. Las pruebas usan claves y
cuentas ficticias, transporte simulado y bases con nombres QA autorizados.

El runner completo ejecutó satisfactoriamente HTTP, los 266 checks de navegador
y el reinicio. Su posprocesado de logs encontró dos casos Windows: lectura en
exclusiva de un archivo activo y AutomationNull en archivos vacíos. Se corrigió
el lector y se verificó separadamente con tres regresiones y los cuatro logs
de esa ejecución. No se repitieron los recorridos académicos tras ese último
ajuste exclusivo del comprobador, ni se presenta el envoltorio como una nueva
ejecución integral posterior a él.

## Revisión visual y preservación

Se revisaron las 75 capturas BYOK en siete láminas y las vistas críticas a tamaño
completo: formularios, selector/consentimiento, error de cuota y reporte agrupado.
Light/Dark y 320/390/768/1440 px, sin desbordamiento horizontal global. Se ajustó
el foco y desplazamiento al abrir formularios. Una captura del reporte se tomaba
antes del render; se añadió la espera al reporte y se volvió a capturar cargado.
La revisión complementa la comparación UX-2 con sus cuatro mockups, registrada
en [UX_2.md](UX_2.md); BYOK reutiliza sus tokens, navegación y componentes.

Se conservan [40 capturas representativas](evidence-byok-1/), incluidas
[Proveedor docente](evidence-byok-1/byok-teacher-initial-1440-dark.png),
[modelo móvil](evidence-byok-1/byok-teacher-models-390-dark.png),
[Claude manual](evidence-byok-1/byok-manual-anthropic-320-light.png),
[cuota](evidence-byok-1/byok-quota-error-390-light.png) y
[evaluación propia](evidence-byok-1/byok-preloaded-evaluation-1440-light.png).
Los formularios se capturaron vacíos, antes de introducir claves ficticias.
Logs, estados y capturas completas permanecen en rutas ignoradas.

Los intentos previos también corrigieron expectativas del harness: número de
migraciones siete→ocho, espera del menú Student, UTF-8 de la autorización simulada
y selección del reporte IA por promptVersion en vez de posición. Las comprobaciones
académicas, de permisos y de consumo se mantuvieron. Esos intentos fallidos no se
cuentan como pases.

La autorización con cuenta OpenRouter real, despliegue/recuperación del protector
Linux y rotación operativa de certificados permanecen pendientes de ensayo en
destino. No hubo cuentas ni inferencias externas reales. Continúan los requisitos
institucionales de [READINESS](../pilot/READINESS.md); este cierre no declara listo
el piloto con personas reales ni certificación WCAG o validación pedagógica.

## Cobertura del encargo

La matriz relaciona los 33 casos pedidos con las pruebas; el resultado final se
registra abajo tras completar la batería y el recorrido visual.

| Casos | Evidencia automatizada |
|---|---|
| 1–3 inicio, S256 y state | PersonalAiTests.PkceConnectsOwnerWithEncryptedCredentialAndNoSecretReadback; RejectsInvalidMissingExpiredOrReusedState |
| 4–6 cambio de cuenta, manipulación, código único/vencido | AccountSwitchRejectsReturnedAuthorizationBeforeExchange; CallbackCannotBeInstalledFromDifferentBrowserAndCannotOverrideDestination; FailedExchangeIsSingleUseAndClearsTemporarySecrets; LogoutAndConcurrentTabsCancelOldAuthorization |
| 7 popup cancelado/bloqueado | byok-qa.mjs, autorización cancelada y alternativa en pestaña; retorno rechazado |
| 8–10 intercambio, cifrado, ausencia de lectura/auditoría de secretos | PkceConnectsOwnerWithEncryptedCredentialAndNoSecretReadback; DurableVaultSurvivesNewInstanceAndSeparatesOwnerRevisionAndPurpose; SecretEchoIsRejectedAndStoredHistoryDoesNotReceiveIt |
| 11–15 Teacher/Student, aislamiento, selección e institucional | Theory Teacher/Student; OwnerCannotSelectAnotherUsersCredentialOrSupplyAnOwnerInManualInput; SelectionChangesDoNotRetargetExistingSessionAndReservationsArePersonal; recorridos Edge por rol |
| 16–20 sin fallback, revocación, modelos incompatibles/gratis/pago | UpstreamErrorHasNoFallbackAndKeepsChargedReservation; CredentialRotationRejectsOldSnapshotAndDoesNotResetBudget; DisconnectWhileWaitingDiscardsLateAcademicResult; OpenRouterCatalogRequiresCompatibleModelAndExplicitPaidPermission; FreeModelBecomingPaidFailsBeforeInferenceWithoutChangingSnapshotOrFallback |
| 21–24 cuotas, concurrencia, retries físicos y timeout | PersonalQuotaIsDistinctFromInstitutionalAndSharedAcrossRotations; PersonalConcurrentAdmissionsHaveOneWinnerAcrossIndependentScopes; EachPhysicalRetryReservesAgainWithSameProviderAndModel; TimeoutKeepsUncertainChargeWithoutInventingUsage |
| 25–28 tutorías, propuesta docente, precargadas y evaluación agrupada | Suite académica existente y byok-qa.mjs: tutoría personal fijada, propuesta Teacher, tarjetas locales sin reservas, evaluación con clave propia Student, evidencia y replay conservados |
| 29 recuperación tras reinicio | Test-RestartRecovery.ps1: metadatos, historial, descifrado persistente, continuación personal y replay sin duplicados |
| 30–32 rutas, CSRF/sesión y SSRF | AnonymousCsrfAndOwnerControlsRemainEnforced; AdminAndPendingTeacherCannotReadOrInstallPersonalConnections; RevokedRoleCannotUseConnectionAndAdminHasNoPersonalAccessByRole; ManualAndCatalogRejectArbitraryProviders; hosts fijos en pruebas de adaptadores |
| 33 restauración funcional | Verify -Smoke -Http, suite Edge completa y reinicio de API; configuración institucional previa restaurada al cierre |

Además: migración repetible que conserva reservas institucionales anteriores;
snapshots y atribución inmutables en PostgreSQL; falta de custodia falla cerrada;
logout durante HTTP descarta respuesta y retirada de rol durante espera del lock
impide confirmar salida académica. Las claves usadas en estas pruebas son ficticias.
