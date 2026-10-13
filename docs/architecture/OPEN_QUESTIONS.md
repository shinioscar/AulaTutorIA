# Ambigüedades del documento maestro

Registro sin cambiar requisitos. Resolver mediante decisiones explícitas cuando
corresponda la iteración; no inferir políticas académicas o institucionales.

| Tema / referencia | Ambigüedad | Tratamiento en Iteración 0 / siguiente paso |
|---|---|---|
| Mock y contrato (§18 vs §23) | Iteración 2 enumera contrato/Mock, pero Iteración 0 exige arrancar con Mock e incluirlo. | Stub y contrato conceptual mínimo en 0; formalización, SDK y orquestación en 2. |
| Autenticación (§8.1) | Cookies o tokens según arquitectura definitiva; faltan reglas de registro, recuperación y aprobación manual. | Resuelto en 1 mediante decisiones aprobadas: Identity, cookies Secure/HttpOnly y CSRF (ADR-012). Recuperación/confirmación quedan pendientes. |
| Versiones (§4.2, §9.2, §12, ADR-005) | Inmutabilidad tras aprobación en algunos pasajes y tras publicación en otros. | Sin entidades todavía; precisar frontera Approved/Published antes de 5. |
| Organización y roles (§3, §6.1, §12) | Una institución piloto, pero alcances por organización; no se define quién precarga primer admin o valida docentes. | Resuelto en 1: organización piloto fija, bootstrap explícito privado e idempotente, aprobación por Admin y pertenencia verificada (ADR-012/013). |
| Contrato IA (§11.1) | AiResponseContract, AiMessage, AiHealthResult y AiTokenUsage carecen de definición completa. | Resuelto en 2: ADR-014 fija roles, texto/JSON/subconjunto Schema, capacidades, cancelación y errores. Mock conserva solo texto; streaming futuro. |
| Privacidad (§4.1, §6.4, §14) | Falta duración de retención, efecto de revocación, base de compartición y acceso extraordinario administrativo. | No datos reales ni acceso a chats; precisar políticas antes de 3/4 y del piloto. |
| SQL del piloto (§10, §17.2) | PostgreSQL almacena la app; tema académico es SQL Server, pero falta definir evaluación aislada de T-SQL. | No instalar SQL Server ni ejecutar SQL de alumnos; definir sandbox al abordar ejercicios. |
| Terminado (§18.1) | Exige migraciones y pruebas de autorización aun cuando Iteración 0 excluye usuarios/datos. | No aplicaban a 0. En 1: migración InitialIdentity y pruebas de autorización real en PostgreSQL. |
| Plataforma y versiones (§8.1) | No se fijan Angular/PostgreSQL ni formato preciso de CI local. | ADR-011 fija versiones compatibles, scripts PowerShell y lockfiles; sin CI remoto ni publicación. |
| Alcance proveedores (§18) | Maestro reserva Gemini/Ollama como extensiones, el nuevo encargo exige integración funcional en 2. | Autorización explícita 2026-10-13, ADR-014; adaptadores probados con HTTP simulado, sin alterar fuente. |
| Schema y modelos (§11) | No se define dialecto JSON Schema ni política de actualizar modelos/capacidades. | Subconjunto explícito y catálogo conservador fechado en ADR-014; evaluar ampliaciones/modelos nuevos antes de declararlos. |
| Costos y reintentos (§11/18) | No hay presupuesto institucional ni reintentos con consumo duplicado definidos. |0 por defecto/máximo 1 en 429/503; cuotas distribuidas y presupuesto siguen pendientes para 8/piloto. |
