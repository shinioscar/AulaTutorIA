# Inventario de Iteración 5

- Domain/Lessons/lesson.cs: identidad, borrador/revisión/versión/publicación,
  propuestas, simulaciones, operaciones y decisiones idempotentes.
- Application/Lessons: contratos académicos/HTTP, validación de borrador/revisión,
  LessonOrchestrator y adaptación al motor pedagógico existente.
- Infrastructure/Lessons: servicio de propietario, persistencia/transiciones,
  propuestas explícitas, generación/lease/retry/cancel y simulación aislada.
- Persistence/lesson-model.cs, AulaTutorDbContext, snapshot y nueva migración
  20261013233137_TeacherLessons (+Designer): nueve tablas e inmutabilidad PostgreSQL.
- Api/Lessons/lesson-endpoints.cs, Program y IdentityOpenApi: composición,
  endpoints protegidos/CSRF, rate limit y documentación de respuestas.
- Infrastructure/DependencyInjection/Ai/MockAiProvider y Application/Tutoring/
  learning-orchestrator: registros, guiones Mock rotulados y soporte booleano en schema.
- Web/lessons: servicio, listado, editor, resumen, simulación y estilos tokenizados.
  Rutas, guard docente y enlaces de cabecera/Inicio; dashboard conservado.
- Tests: lesson-tests unitarios y fixture/tests de integración PostgreSQL;
  expectativas previas actualizadas a cuarta migración y rutas protegidas.
- Scripts/Http-Lessons.ps1 y Http-Identity: 32 comprobaciones HTTP docentes nuevas,
  conservando las 75 anteriores; serialización de contratos anidados.
- Documentación: ADR-021, LESSONS_HTTP, ITERATION_5, QA_5, 26 PNG en evidence-5;
  README, AGENTS, índice/plan/ambigüedades y este inventario actualizados.

Sin nuevos paquetes/lockfiles, cambios de .env o del maestro, asignaciones de 6,
SDK, modelos descargados ni integración institucional. Artifacts/cache ignorados.
