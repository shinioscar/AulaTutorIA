# Plan por iteraciones

Fuente: documento maestro v1.0 §18. Una sola iteración por encargo.

| Iteración | Estado | Entregables / aceptación |
|---|---|---|
| 0 | Completada; histórico en ITERATION_0.md | Repo, arquitectura, configuración, Compose, Mock, salud, web y CI local. |
| 1 | Completada; registro ITERATION_1.md | Identity, registro/login piloto, validación Teacher, grupos y autorización por recurso; pruebas de acceso cruzado. |
| 2 | Implementada; registro ITERATION_2.md | IAiProvider/Mock, Groq/Gemini/Ollama funcionales por encargo adicional, configuración, capacidades, telemetría y diagnóstico Admin. |
| 3 | Implementada; registro ITERATION_3.md | Tutoría privada, orquestación inicial, conversación/ayudas/historial/pausa/reanudación/cierre local, concurrencia e idempotencia; Mock en QA. |
| 4 | Completada; registro ITERATION_4.md | Diagnóstico estructurado, ampliación autorizada de planes personales ejecutables, evidencias, validadores, reportes, refuerzo y compartición mínima. |
| 5 | Completada; registro ITERATION_5.md | Constructor docente, propuestas explícitas, simulación aislada y aprobación/publicación de versiones inmutables; 155 pruebas/107 HTTP. |
| 6 | Completada; cierre en ITERATION_6.md | Asignaciones de publicación concreta, progreso/evidencias independientes, mismo núcleo aprobado y refuerzo complementario; 214 pruebas/149 HTTP. |
| 7 | Pendiente | Panel docente, revisión, progreso y atención humana pendiente autorizada. |
| 8 | Pendiente | Límites, 429, auditoría, recuperación, concurrencia y E2E. |
| 9 | Pendiente | Piloto SQL con datos ficticios, guías y métricas. |
| 10 | Futuro | Derivación/conector institucional autorizado; no necesario para MVP. |

Recomendaciones de Iteración 1: cerrar decisiones de cookies/CSRF y bootstrap;
añadir EF Core 10/Npgsql compatibles e Identity con migración inicial reproducible;
verificar PostgreSQL con Docker activo; probar Student A/B, Teacher X/Y y Admin
sin privilegio automático sobre chats. No implementar tutorías en ese encargo.

Los encargos de 0/1 se conservan. La Iteración 2 añade proveedores según autorización
explícita del 2026-10-13, más política de contraseña y búsqueda por correo (ADR-014/015).
Iteraciones 3/4/5/6 autorizadas por nuevos encargos. No avanzar a 7 automáticamente. Mantener
cookies/CSRF, propiedad, privacidad y Groq local; pruebas solo Mock/simulaciones.
