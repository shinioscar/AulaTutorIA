# ADR-013 — Persistencia del piloto y administrador inicial

Fecha: 2026-10-12. Estado: implementada en Iteración 1.

Una organización piloto con ID fijo, sembrada por la migración, y tres roles
Identity con IDs estables. No hay alta de instituciones ni selección de tenant.
Usuarios y grupos tienen organizationId; membresías tienen claves foráneas
compuestas a usuario/grupo para impedir relaciones entre organizaciones en la BD.
Índices únicos por grupo/nombre/organización y membresía/usuario/tipo; borrado
restrictivo. Archivar conserva datos. No se implementan entidades de aprendizaje.

Migration `InitialIdentity` crea Identity, Organizations, Groups y GroupMemberships,
con rol/organización iniciales, **sin usuarios ni contraseñas**. Tool dotnet-ef
10.0.10 local al repositorio, sin herramientas globales. Migración explícita:
`dotnet run --project src/AulaTutor.Api --no-launch-profile -- --migrate`.
El inicio normal de la web no cambia esquema ni siembra cuentas.

Bootstrap independiente: flag `--bootstrap-admin`; requiere BootstrapAdmin__Email,
BootstrapAdmin__DisplayName y BootstrapAdmin__Password privados. El wrapper
scripts/Bootstrap-Admin.ps1 pide la contraseña mediante Read-Host -AsSecureString,
la pasa en entorno al proceso hijo y limpia las variables al terminar. Nada en
argumentos CLI, repo ni logs contiene contraseña. Solo Identity crea el hash.

Una transacción y advisory lock de PostgreSQL serializan bootstrap, modificaciones
de roles/estado y asociación de miembros. Repetir para el mismo Admin activo no
cambia contraseña, nombre ni roles; no promueve cuentas registradas previamente,
ni crea un segundo administrador por bootstrap. Administradores adicionales se
otorgan por la UI/API ya autorizada. Parámetros inválidos o ausentes fallan antes
de crear una cuenta. La misma transacción protege al último Admin activo.

Data Protection: claves persistidas fuera del repositorio. En Windows se protegen
con DPAPI del usuario; en Compose un volumen /keys pertenece al usuario app
(UID 1654) con permisos 700. En Linux, el piloto confía en permisos/almacenamiento
del volumen; antes de producción se requiere definir cifrado de claves en reposo
y respaldos, TLS confiable, credenciales PostgreSQL de mínimo privilegio y proxy
de mismo origen. La imagen/API corre sin root.

Compose usa autenticación PostgreSQL por contraseña y GSS Encryption Mode=Disable:
el laboratorio no configura Kerberos y la imagen ASP.NET no incluye libgssapi.
Esto evita el aviso nativo de fallback sin instalar dependencias innecesarias;
no cambia la obligación de TLS para un despliegue externo. Referencia:
https://www.npgsql.org/doc/connection-string-parameters.html

Pruebas: cada test de identidad crea una base `aulatutor_test_<GUID>`, aplica la
migración dos veces, usa cuentas @example.invalid y contraseña aleatoria, y elimina
solo esa base al terminar. No usa EF InMemory ni SQLite, ni omite pruebas por
ausencia de PostgreSQL. Requiere una conexión local capaz de CREATE DATABASE.
HTTP/browser QA usa `aulatutor_http_<GUID>` separado. La BD principal conserva cero
usuarios de prueba y no recibe un administrador ficticio predeterminado.

Pendientes fuera del encargo: confirmación por email, recuperación de contraseña,
MFA, invitaciones autoservicio, auditoría integral (Iteración 8), gestión de
retención/compartición (Iteración 4) y roles multiinstitución. Se documentan sin
activar proveedores externos ni modificar el maestro.
