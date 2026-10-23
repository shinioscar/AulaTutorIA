# Archivos de Iteración 7

Inventario de fuentes y documentación versionables. Sin nuevas dependencias directas;
parches compatibles de CLI/build y package-lock. El maestro original y las cinco
migraciones anteriores permanecen intactos.

## Nuevos

- docs/architecture/SUPERVISION_HTTP.md
- docs/decisions/ADR-024-supervision-y-revision-humana.md
- docs/iterations/ITERATION_7.md
- docs/iterations/QA_7.md
- scripts/Http-Supervision.ps1
- src/AulaTutor.Api/Lessons/supervision-endpoints.cs
- src/AulaTutor.Application/Lessons/supervision-contracts.cs
- src/AulaTutor.Application/Lessons/supervision-rules.cs
- src/AulaTutor.Domain/Lessons/teacher-review.cs
- src/AulaTutor.Infrastructure/Lessons/supervision-reviews.cs
- src/AulaTutor.Infrastructure/Lessons/supervision-service.cs
- src/AulaTutor.Infrastructure/Persistence/Migrations/20261022224718_TeacherSupervision.cs
- src/AulaTutor.Infrastructure/Persistence/Migrations/20261022224718_TeacherSupervision.Designer.cs
- src/AulaTutor.Infrastructure/Persistence/supervision-model.cs
- src/AulaTutor.Web/src/app/supervision/review-history.ts
- src/AulaTutor.Web/src/app/supervision/supervision-page.html
- src/AulaTutor.Web/src/app/supervision/supervision-page.ts
- src/AulaTutor.Web/src/app/supervision/supervision.css
- src/AulaTutor.Web/src/app/supervision/supervision.service.ts
- tests/AulaTutor.IntegrationTests/supervision-tests.cs
- tests/AulaTutor.UnitTests/supervision-tests.cs
- docs/iterations/FILES_7.md (este inventario)
- docs/iterations/evidence-7/: 26 PNG; listado y alcance en QA_7.md.

## Modificados

- AGENTS.md
- docs/architecture/OPEN_QUESTIONS.md
- docs/architecture/README.md
- docs/design/COMPONENT_GUIDELINES.md
- docs/iterations/PLAN.md
- docs/MASTER_PLAN.md
- README.md
- scripts/Http-Identity.ps1
- src/AulaTutor.Api/Identity/IdentityOpenApi.cs
- src/AulaTutor.Api/Program.cs
- src/AulaTutor.Infrastructure/DependencyInjection.cs
- src/AulaTutor.Infrastructure/Persistence/AulaTutorDbContext.cs
- src/AulaTutor.Infrastructure/Persistence/Migrations/AulaTutorDbContextModelSnapshot.cs
- src/AulaTutor.Web/package-lock.json
- src/AulaTutor.Web/package.json
- src/AulaTutor.Web/src/app/app.html
- src/AulaTutor.Web/src/app/app.routes.ts
- src/AulaTutor.Web/src/app/home/home.html
- src/AulaTutor.Web/src/app/lessons/student-classes-page.html
- src/AulaTutor.Web/src/app/lessons/student-classes-page.ts
- src/AulaTutor.Web/src/app/tutoring/chat-page.html
- tests/AulaTutor.IntegrationTests/IdentityTests.cs
- tests/AulaTutor.IntegrationTests/TutoringTests.cs
