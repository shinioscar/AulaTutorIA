# Plan por iteraciones

Fuente: documento maestro v 1.0 §18. Una sola iteración por encargo.

| Iteración | Estado | Entregables / aceptación |
|---|---|---|
| 0 | Completada; histórico en ITERATION_0.md | Repo, arquitectura, configuración, Compose, Mock, salud, web y CI local. |
| 1 | Completada; registro ITERATION_1.md | Identity, registro/login piloto, validación Teacher, grupos y autorización por recurso; pruebas de acceso cruzado. |
| 2 | Implementada; registro ITERATION_2.md | IAiProvider/Mock, Groq/Gemini/Ollama funcionales por encargo adicional, configuración, capacidades, telemetría y diagnóstico Admin. |
| 3 | Pendiente | Tutoría libre, diagnóstico, chat, práctica y pausa/reanudación; cierre dentro de la aplicación. |
| 4 | Pendiente | Evidencias, validadores, reportes, compartición; correspondencia con intentos. |
| 5 | Pendiente | Constructor, simulación, aprobación, versiones inmutables. |
| 6 | Pendiente | Asignaciones, progreso independiente por alumno con misma versión. |
| 7 | Pendiente | Panel docente, revisión, progreso y atención humana pendiente autorizada. |
| 8 | Pendiente | Límites, 429, auditoría, recuperación, concurrencia y E 2E. |
| 9 | Pendiente | Piloto SQL con datos ficticios, guías y métricas. |
| 10 | Futuro | Derivación/conector institucional autorizado; no necesario para MVP. |

Recomendaciones de Iteración 1: cerrar decisiones de cookies/CSRF y bootstrap;
añadir EF Core 10/Npgsql compatibles e Identity con migración inicial reproducible;
verificar PostgreSQL con Docker activo; probar Student A/B, Teacher X/Y y Admin
sin privilegio automático sobre chats. No implementar tutorías en ese encargo.

Los encargos de 0/1 se conservan. La Iteración 2 añade proveedores según autorización
explícita del 2026-10-13, más política de contraseña y búsqueda por correo (ADR-014/015).
No avanzar a 3 automáticamente. Mantener cookies/CSRF, pertenencia y privacidad.
