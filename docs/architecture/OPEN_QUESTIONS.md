# Ambigüedades del documento maestro

Registro sin cambiar requisitos. Resolver mediante decisiones explícitas cuando
corresponda la iteración; no inferir políticas académicas o institucionales.

| Tema / referencia | Ambigüedad | Tratamiento en Iteración 0 / siguiente paso |
|---|---|---|
| Mock y contrato (§18 vs §23) | Iteración 2 enumera contrato/Mock, pero Iteración 0 exige arrancar con Mock e incluirlo. | Stub y contrato conceptual mínimo en 0; formalización, SDK y orquestación en 2. |
| Autenticación (§8.1) | Cookies o tokens según arquitectura definitiva; faltan reglas de registro, recuperación y aprobación manual. | Proxy de mismo origen preparado; propuesta de cookies HTTP-only + CSRF, confirmar/detallar antes de implementar Identity en 1. |
| Versiones (§4.2, §9.2, §12, ADR-005) | Inmutabilidad tras aprobación en algunos pasajes y tras publicación en otros. | Sin entidades todavía; precisar frontera Approved/Published antes de 5. |
| Organización y roles (§3, §6.1, §12) | Una institución piloto, pero alcances por organización; no se define quién precarga primer admin o valida docentes. | Definir bootstrap ficticio, estados y autorización por recurso/grupo en 1. |
| Contrato IA (§11.1) | AiResponseContract, AiMessage, AiHealthResult y AiTokenUsage carecen de definición completa. | DTO provisionales documentados; Mock solo texto, sin tokenización ficticia ni soporte JSON supuesto; completar en 2. |
| Privacidad (§4.1, §6.4, §14) | Falta duración de retención, efecto de revocación, base de compartición y acceso extraordinario administrativo. | No datos reales ni acceso a chats; precisar políticas antes de 3/4 y del piloto. |
| SQL del piloto (§10, §17.2) | PostgreSQL almacena la app; tema académico es SQL Server, pero falta definir evaluación aislada de T-SQL. | No instalar SQL Server ni ejecutar SQL de alumnos; definir sandbox al abordar ejercicios. |
| Terminado (§18.1) | Exige migraciones y pruebas de autorización aun cuando Iteración 0 excluye usuarios/datos. | No aplican aún; no crear migraciones vacías ni auth ficticia. Documentar su exigencia desde Iteración 1. |
| Plataforma y versiones (§8.1) | No se fijan Angular/PostgreSQL ni formato preciso de CI local. | ADR-011 fija versiones compatibles, scripts PowerShell y lockfiles; sin CI remoto ni publicación. |
