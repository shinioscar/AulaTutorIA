# ADR-015 — Política de contraseña y búsqueda de participantes

Fecha: 2026-10-13. Estado: aceptado por instrucción explícita del usuario.

## Decisión

La contraseña de nuevas cuentas Admin, Teacher y Student admite 5–128 caracteres.
Identity RequiredLength=5 y RequiredUniqueChars=1; no exige mayúscula, minúscula,
número ni símbolo. La longitud y variedad se recomiendan en UI y bootstrap, sin
validación de composición obligatoria. No hay migración ni modificación de hashes
existentes. Identity sigue gestionando exclusivamente las contraseñas y permanecen
el bloqueo de 5 fallos, límite por IP, cookies seguras y CSRF. Esta política sustituye
la longitud/composición inicial del registro histórico de Iteración 1.

El selector de miembros de grupo incorpora un campo accesible de búsqueda por
correo, botón explícito y aviso sin resultados. GET /api/admin/users acepta email
opcional (máximo 254), normaliza con Identity y filtra parcialmente antes de paginar
en PostgreSQL. Contains trata % y _ literalmente, sin patrones de usuario. Se
mantienen organización piloto, Admin activo y páginas de 50; no se añaden permisos
para docentes/alumnos. Búsqueda vacía muestra todos; nueva búsqueda vuelve a página 1
y borra selección anterior; avanzar página conserva el filtro aplicado.

La selección/asociación sigue validando estado, rol, aprobación y organización
en backend. No se cambia membresía al buscar. La comprobación automatizada inserta
más de 50 cuentas ficticias en BD aislada y encuentra una cuenta fuera de página 1.
