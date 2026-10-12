# Plan por iteraciones

Fuente: documento maestro v1.0 §18. Una sola iteración por encargo.

| Iteración | Estado | Entregables / aceptación |
|---|---|---|
| 0 | Base creada; verificación registrada en ITERATION_0.md | Repo, arquitectura, configuración, Compose, Mock, salud, web y CI local. |
| 1 | Pendiente | Identity, registro/login piloto, validación Teacher, grupos y autorización por recurso; pruebas de acceso cruzado. |
| 2 | Pendiente | Formalizar IAiProvider/Mock, Groq, extensibilidad Gemini/Ollama, configuración y telemetría; sustitución sin alterar casos de uso. |
| 3 | Pendiente | Tutoría libre, diagnóstico, chat, práctica y pausa/reanudación; cierre dentro de la aplicación. |
| 4 | Pendiente | Evidencias, validadores, reportes, compartición; correspondencia con intentos. |
| 5 | Pendiente | Constructor, simulación, aprobación, versiones inmutables. |
| 6 | Pendiente | Asignaciones, progreso independiente por alumno con misma versión. |
| 7 | Pendiente | Panel docente, revisión, progreso y atención humana pendiente autorizada. |
| 8 | Pendiente | Límites, 429, auditoría, recuperación, concurrencia y E2E. |
| 9 | Pendiente | Piloto SQL con datos ficticios, guías y métricas. |
| 10 | Futuro | Derivación/conector institucional autorizado; no necesario para MVP. |

Recomendaciones de Iteración 1: cerrar decisiones de cookies/CSRF y bootstrap;
añadir EF Core 10/Npgsql compatibles e Identity con migración inicial reproducible;
verificar PostgreSQL con Docker activo; probar Student A/B, Teacher X/Y y Admin
sin privilegio automático sobre chats. No implementar tutorías en ese encargo.
