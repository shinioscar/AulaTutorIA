# Archivos de Iteración 1

El maestro original permanece intacto. Secrets/caches/artifacts excluidos.

## Nuevos

```text
docs/architecture/IDENTITY_HTTP.md
docs/decisions/ADR-012-identidad-y-autorizacion.md
docs/decisions/ADR-013-persistencia-y-bootstrap.md
docs/iterations/evidence-1/admin-desktop.png
docs/iterations/evidence-1/admin-mobile.png
docs/iterations/evidence-1/groups-admin-desktop.png
docs/iterations/evidence-1/groups-admin-mobile.png
docs/iterations/evidence-1/login-error-desktop.png
docs/iterations/evidence-1/pending-mobile.png
docs/iterations/evidence-1/register-desktop.png
docs/iterations/evidence-1/student-groups-mobile.png
docs/iterations/evidence-1/teacher-groups-desktop.png
docs/iterations/FILES_1.md
docs/iterations/ITERATION_1.md
dotnet-tools.json
scripts/Bootstrap-Admin.ps1
scripts/Http-Identity.ps1
scripts/Load-LocalConfiguration.ps1
scripts/Prepare-Local.ps1
src/AulaTutor.Api/Identity/IdentityEndpoints.cs
src/AulaTutor.Api/Identity/IdentityOpenApi.cs
src/AulaTutor.Application/Identity/IdentityContracts.cs
src/AulaTutor.Domain/Identity/AcademicIdentity.cs
src/AulaTutor.Infrastructure/Identity/AdminBootstrap.cs
src/AulaTutor.Infrastructure/Identity/AppUser.cs
src/AulaTutor.Infrastructure/Identity/CurrentActor.cs
src/AulaTutor.Infrastructure/Identity/GroupService.cs
src/AulaTutor.Infrastructure/Identity/IdentityRegistration.cs
src/AulaTutor.Infrastructure/Identity/IdentityService.cs
src/AulaTutor.Infrastructure/Persistence/AulaTutorDbContext.cs
src/AulaTutor.Infrastructure/Persistence/Migrations/20261012170343_InitialIdentity.cs
src/AulaTutor.Infrastructure/Persistence/Migrations/20261012170343_InitialIdentity.Designer.cs
src/AulaTutor.Infrastructure/Persistence/Migrations/AulaTutorDbContextModelSnapshot.cs
src/AulaTutor.Web/src/app/admin/admin-page.html
src/AulaTutor.Web/src/app/admin/admin-page.ts
src/AulaTutor.Web/src/app/app.routes.ts
src/AulaTutor.Web/src/app/auth/auth-page.html
src/AulaTutor.Web/src/app/auth/auth-page.ts
src/AulaTutor.Web/src/app/auth/auth.service.ts
src/AulaTutor.Web/src/app/groups/groups-page.html
src/AulaTutor.Web/src/app/groups/groups-page.ts
src/AulaTutor.Web/src/app/home/home.html
src/AulaTutor.Web/src/app/home/home.ts
tests/AulaTutor.IntegrationTests/IdentityTests.cs
tests/AulaTutor.IntegrationTests/TestHost.cs
tests/AulaTutor.UnitTests/AccessRulesTests.cs
```

## Modificados

```text
.env.example
AGENTS.md
deploy/api.Dockerfile
deploy/compose.yaml
docs/architecture/OPEN_QUESTIONS.md
docs/architecture/README.md
docs/iterations/PLAN.md
README.md
scripts/Smoke.ps1
scripts/Verify.ps1
src/AulaTutor.Api/AulaTutor.Api.csproj
src/AulaTutor.Api/packages.lock.json
src/AulaTutor.Api/Program.cs
src/AulaTutor.Api/Properties/launchSettings.json
src/AulaTutor.Domain/README.md
src/AulaTutor.Infrastructure/AulaTutor.Infrastructure.csproj
src/AulaTutor.Infrastructure/packages.lock.json
src/AulaTutor.Web/package-lock.json
src/AulaTutor.Web/package.json
src/AulaTutor.Web/proxy.conf.json
src/AulaTutor.Web/src/app/app.html
src/AulaTutor.Web/src/app/app.ts
src/AulaTutor.Web/src/main.ts
src/AulaTutor.Web/src/styles.css
tests/AulaTutor.IntegrationTests/HealthEndpointTests.cs
tests/AulaTutor.IntegrationTests/packages.lock.json
tests/AulaTutor.UnitTests/packages.lock.json
```
