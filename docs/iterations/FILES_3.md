# Archivos de Iteración 3

Sin paquetes nuevos ni cambios a lockfiles. Documento maestro intacto.

## Creados

- `docs/architecture/TUTORING_HTTP.md`
- `docs/decisions/ADR-016-tutorias-privadas.md`
- `docs/decisions/ADR-017-integridad-de-turnos.md`
- `docs/iterations/evidence-3/access-denied-desktop.png`
- `docs/iterations/evidence-3/chat-desktop.png`
- `docs/iterations/evidence-3/chat-mobile.png`
- `docs/iterations/evidence-3/completed-desktop.png`
- `docs/iterations/evidence-3/completed-mobile.png`
- `docs/iterations/evidence-3/direct-review-mobile.png`
- `docs/iterations/evidence-3/error-staged-mobile.png`
- `docs/iterations/evidence-3/generating-desktop.png`
- `docs/iterations/evidence-3/network-error-mobile.png`
- `docs/iterations/evidence-3/paused-desktop.png`
- `docs/iterations/evidence-3/pending-staged-desktop.png`
- `docs/iterations/evidence-3/sessions-desktop.png`
- `docs/iterations/evidence-3/sessions-empty-desktop.png`
- `docs/iterations/evidence-3/sessions-mobile.png`
- `docs/iterations/evidence-3/tutoring-created-desktop.png`
- `docs/iterations/FILES_3.md`
- `docs/iterations/ITERATION_3.md`
- `docs/iterations/QA_3.md`
- `src/AulaTutor.Api/Tutoring/TutoringEndpoints.cs`
- `src/AulaTutor.Application/Tutoring/TutoringContracts.cs`
- `src/AulaTutor.Application/Tutoring/TutoringOrchestrator.cs`
- `src/AulaTutor.Domain/Tutoring/TutoringSession.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/20261013172751_PrivateTutoring.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/20261013172751_PrivateTutoring.Designer.cs`
- `src/AulaTutor.Infrastructure/Persistence/TutoringModel.cs`
- `src/AulaTutor.Infrastructure/Tutoring/TutoringService.cs`
- `src/AulaTutor.Web/src/app/tutoring/chat-page.css`
- `src/AulaTutor.Web/src/app/tutoring/chat-page.html`
- `src/AulaTutor.Web/src/app/tutoring/chat-page.ts`
- `src/AulaTutor.Web/src/app/tutoring/safe-response.ts`
- `src/AulaTutor.Web/src/app/tutoring/sessions-page.html`
- `src/AulaTutor.Web/src/app/tutoring/sessions-page.ts`
- `src/AulaTutor.Web/src/app/tutoring/tutoring.service.ts`
- `tests/AulaTutor.IntegrationTests/TutoringTests.cs`
- `tests/AulaTutor.UnitTests/TutoringOrchestratorTests.cs`

## Modificados

- `AGENTS.md`
- `docs/architecture/IDENTITY_HTTP.md`
- `docs/architecture/OPEN_QUESTIONS.md`
- `docs/architecture/README.md`
- `docs/decisions/ADR-014-proveedores-ia.md`
- `docs/iterations/ITERATION_2.md`
- `docs/iterations/PLAN.md`
- `docs/MASTER_PLAN.md`
- `README.md`
- `scripts/Http-Identity.ps1`
- `scripts/Smoke.ps1`
- `scripts/Verify.ps1`
- `src/AulaTutor.Api/Identity/IdentityEndpoints.cs`
- `src/AulaTutor.Api/Identity/IdentityOpenApi.cs`
- `src/AulaTutor.Api/Program.cs`
- `src/AulaTutor.Infrastructure/Ai/MockAiProvider.cs`
- `src/AulaTutor.Infrastructure/DependencyInjection.cs`
- `src/AulaTutor.Infrastructure/Persistence/AulaTutorDbContext.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/AulaTutorDbContextModelSnapshot.cs`
- `src/AulaTutor.Web/src/app/app.html`
- `src/AulaTutor.Web/src/app/app.routes.ts`
- `src/AulaTutor.Web/src/app/auth/auth.service.ts`
- `src/AulaTutor.Web/src/app/home/home.html`
- `src/AulaTutor.Web/src/styles.css`
- `tests/AulaTutor.IntegrationTests/HealthEndpointTests.cs`
- `tests/AulaTutor.IntegrationTests/IdentityTests.cs`
- `tests/AulaTutor.IntegrationTests/TestHost.cs`

Domain/Application definen modelo, contratos y política; Infrastructure persiste y
coordina generaciones; Api autoriza/valida; Web incorpora listado/conversación.
Scripts/TestHost fuerzan Mock y apartan claves externas. Pruebas y evidencias
cubren permisos, persistencia, concurrencia, recuperación y diseño responsivo.
Los dos cambios históricos en ADR-014/ITERATION_2 corrigen espacios erróneos en
ruta HTTP y comando PowerShell, sin alterar decisiones ni resultados previos.
