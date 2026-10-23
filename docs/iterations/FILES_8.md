# Archivos de la Iteración 8

Inventario del cierre; los logs/JSON de ejecución quedan ignorados en artifacts y el
estado privado en .cache. No se versionan secretos, volcados ni datos de cuentas.

## Modificados

- `AGENTS.md`
- `README.md`
- `docs/MASTER_PLAN.md`
- `docs/architecture/OPEN_QUESTIONS.md`
- `docs/architecture/README.md`
- `docs/iterations/PLAN.md`
- `scripts/Http-Identity.ps1`
- `src/AulaTutor.Api/Program.cs`
- `src/AulaTutor.Application/Ai/AiContracts.cs`
- `src/AulaTutor.Infrastructure/Ai/AiHttpTransport.cs`
- `src/AulaTutor.Infrastructure/Ai/HttpAiProvider.cs`
- `src/AulaTutor.Infrastructure/DependencyInjection.cs`
- `src/AulaTutor.Infrastructure/Identity/GroupService.cs`
- `src/AulaTutor.Infrastructure/Identity/IdentityService.cs`
- `src/AulaTutor.Infrastructure/Lessons/supervision-service.cs`
- `src/AulaTutor.Infrastructure/Persistence/AulaTutorDbContext.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/AulaTutorDbContextModelSnapshot.cs`
- `src/AulaTutor.Web/src/app/ai/ai-error-message.ts`
- `tests/AulaTutor.IntegrationTests/IdentityTests.cs`
- `tests/AulaTutor.IntegrationTests/TutoringTests.cs`

## Nuevos

- `docs/architecture/OPERATIONS.md`
- `docs/decisions/ADR-025-cuotas-auditoria-y-resiliencia.md`
- `docs/iterations/ITERATION_8.md`
- `docs/iterations/QA_8.md`
- `docs/iterations/evidence-8/student-completed-dark-mobile.png`
- `docs/iterations/evidence-8/student-feedback-light-mobile.png`
- `docs/iterations/evidence-8/student-quota-dark-mobile.png`
- `docs/iterations/evidence-8/student-quota-input-dark-mobile.png`
- `docs/iterations/evidence-8/teacher-published-light.png`
- `docs/iterations/evidence-8/teacher-review-dark-320.png`
- `docs/iterations/evidence-8/teacher-review-light.png`
- `scripts/Http-Operations.ps1`
- `scripts/Test-BackupRestore.ps1`
- `scripts/Test-E2E.ps1`
- `scripts/Test-RestartRecovery.ps1`
- `src/AulaTutor.Api/operational-endpoints.cs`
- `src/AulaTutor.Domain/Identity/operational-records.cs`
- `src/AulaTutor.Infrastructure/Ai/persistent-ai-quota.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/20261023015145_OperationalResilience.Designer.cs`
- `src/AulaTutor.Infrastructure/Persistence/Migrations/20261023015145_OperationalResilience.cs`
- `src/AulaTutor.Infrastructure/Persistence/operational-model.cs`
- `tests/AulaTutor.E2E/run.mjs`
- `tests/AulaTutor.IntegrationTests/admission-transport-tests.cs`
- `tests/AulaTutor.IntegrationTests/load-tests.cs`
- `tests/AulaTutor.IntegrationTests/operational-tests.cs`
- `tests/AulaTutor.IntegrationTests/quota-tests.cs`
- `tests/AulaTutor.UnitTests/quota-policy-tests.cs`
- `docs/iterations/FILES_8.md`

Las siete PNG de evidence-8 contienen datos ficticios y fueron revisadas visualmente.
Las seis migraciones anteriores y el documento maestro no se modificaron.
