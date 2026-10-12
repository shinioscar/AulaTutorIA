# Iteración 1 — Identidad, roles, grupos y autorización

Fecha: 12 de octubre de 2026. Estado: **completada**, sin avanzar a Iteración 2.
Trabajo exclusivo en F:\AulaTutorIA. Se leyeron íntegros AGENTS, documento maestro,
registro 0, ADR y ambigüedades antes de modificar código. Maestro preservado con
SHA-256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Funcionalidades

- Registro Student, solicitudes Teacher pendientes, login, sesión activa y logout.
- Aprobación/rechazo docente por Admin; roles válidos y activación/desactivación.
  Bootstrap explícito y transaccional para el primer Admin, sin usuario/contraseña
  predeterminados, sin elevar una cuenta pública ni resetear contraseña al repetir.
- Organización piloto única, grupos, docentes asignados, membresías Student,
  renombrado y archivo. Asociar/retirar miembros es idempotente. Se preservan datos.
- Autoridad por actor actual, organización y relación: Student no ve perfiles de
  otros ni roster; Teacher solo grupos asignados y roster mínimo sin emails; Admin
  gestiona el piloto sin recibir derecho sobre futuras conversaciones privadas.
- Angular: navegación, formularios de registro/login, estado docente, inicio,
  grupos propios y administración mínima de usuarios/roles/miembros. CanViewMembers
  se calcula por grupo para cuentas con roles Teacher+Student; backend lo verifica.
- EF Core/Identity 10.0.10 y Npgsql 10.0.3; migración InitialIdentity reproducible,
  semilla de roles/organización sin usuarios. Tool EF local; locks actualizados.
- Mock y salud/OpenAPI se mantienen, sin llamadas IA reales ni entidades pedagógicas.

Detalles de permisos/seguridad: ADR-012/ADR-013 y architecture/IDENTITY_HTTP.md.

## Seguridad implementada

Cookies Secure/HttpOnly/Strict y HTTPS local, ticket fijo de dos horas. CSRF en
todas las escrituras, incluido login y registro; tokens ligados al usuario y
renovación tras login/logout. Sin credenciales de sesión en localStorage. Cookies
antiforgery y sesión HttpOnly; XSRF-TOKEN legible únicamente sirve para CSRF.

Identity gestiona hashes, cinco fallos bloquean quince minutos y se limita
login/registro por IP. Errores públicos uniformes, no enumeración de email en
registro válido. Revocación por security stamp al cambiar permisos/estado/decisión
y cerrar sesión (todas las sesiones de la cuenta). Cuenta desactivada no inicia.

DTO validados; transacciones/advisory lock para bootstrap y administración crítica;
protección del propio/último Admin. FKs compuestas impiden relaciones entre
organizaciones. No se registran cuerpos de autenticación, contraseñas ni conexiones.
.env, certificados, claves, perfiles de navegador y estado de QA quedan fuera de Git.

## Verificaciones

| Comprobación | Resultado |
|---|---|
| Restore bloqueado + build Release | Pasa; cero advertencias y errores. |
| Unitarias | 11 pasan: 8 del Mock conservadas, 3 de autorización/privacidad. |
| Integración | 13 pasan, con PostgreSQL real; 0 omitidas. |
| Migración | Se aplica dos veces en cada BD de test; sin cambios pendientes del modelo. BD principal migrada explícitamente. |
| Registro/login | Student y Teacher Pending; hash Identity validado, duplicado uniforme, sin duplicar cuenta. |
| Credenciales/bloqueo/límite | Errores genéricos y bloqueo tras cinco fallos; petición 31 por minuto recibe 429. |
| Aprobación/roles | Teacher sin aprobación no opera; Admin aprueba y revoca sesión anterior; elevación pública/Student rechazada. |
| Recursos cruzados | Student A/B y grupos ajenos Teacher bloqueados; pertenencia retirada revoca acceso; dual Teacher+Student respeta permiso por grupo. |
| CSRF | Login/registro y escrituras autenticadas sin token o con token de otra sesión reciben 400. |
| Logout/estado | Logout, replay de cookie anterior y desactivación bloquean acceso. |
| Bootstrap | Repetición idempotente no cambia contraseña; Admin autorizado; propio/último acceso protegido. |
| Anónimos | Todos los endpoints protegidos probados (lecturas y mutaciones) devuelven 401. |
| Grupos | Crear, renombrar, asociar idempotente y archivar; preservación de membresías y rechazo de relaciones inválidas. |
| OpenAPI | Salud conservada, esquemas, cookie y cabecera CSRF; códigos 204/401; no publicado en Production. |
| Angular | npm ci, TypeScript y compilación de plantillas/build producción correctos; 409 paquetes auditados sin vulnerabilidades reportadas. |
| CI local completo | Verify.ps1 -Smoke -Http pasó; incluye salud/proxy HTTPS y recorrido real de identidad. |
| HTTP real | 24 comprobaciones en API HTTPS/Kestrel y PostgreSQL aislado; bases/usuarios de prueba eliminados. |
| Docker | Restore locked y publish Linux, API sin root y volumen de claves privado; PostgreSQL del proyecto saludable. |
| Visual/funcional | Edge existente + Playwright incluido: escritorio 1440×1000, móvil 390×844 y ancho 320; sin errores JS ni overflow horizontal. |
| Git/maestro | Diff/secret exclusions y hash originales verificados; sin remoto ni push. |

Tras el ajuste final de permisos mixtos se repitieron build, 24 pruebas, TypeScript,
Angular, HTTP y revisión funcional de Teacher/Student. No se sustituyen pruebas
de PostgreSQL con InMemory/SQLite. La principal mantiene cero usuarios de QA.

## Revisión visual

Se recorrieron registro Teacher, pendiente, login/logout, aprobación, creación y
renombrado de grupo, asociación docente, activación/desactivación y roles de usuario,
grupo autorizado Teacher y vista Student sin roster. Se verificaron error de
credenciales, recuperación, foco/Enter, Mock y ausencia de datos en localStorage.
Capturas revisadas: sin recortes, superposiciones o desbordamientos observados.

- [Registro escritorio](evidence-1/register-desktop.png)
- [Docente pendiente móvil](evidence-1/pending-mobile.png)
- [Administración escritorio](evidence-1/admin-desktop.png)
- [Administración móvil](evidence-1/admin-mobile.png)
- [Grupos Admin escritorio](evidence-1/groups-admin-desktop.png)
- [Grupos Admin móvil](evidence-1/groups-admin-mobile.png)
- [Grupo Teacher escritorio](evidence-1/teacher-groups-desktop.png)
- [Grupo Student móvil](evidence-1/student-groups-mobile.png)
- [Error de credenciales](evidence-1/login-error-desktop.png)

## Incidencias resueltas

1. El test de error genérico comparaba traceId, que varía por petición. Se verifican
   estado y mensaje público iguales, conservando la trazabilidad de cada petición.
2. WebRequestSession de PowerShell conservaba la cabecera CSRF en comprobaciones
   negativas; el harness ahora la limpia explícitamente. Backend y pruebas .NET
   validaron el rechazo; el HTTP real corregido también devuelve 400.
3. Windows bloqueó DLL mientras la API de QA estaba ejecutándose. Se detuvo solo
   el proceso del proyecto, se reejecutó build limpio y se documentó la parada previa.
4. Git detectó saltos/lineas vacías de generación PowerShell; se normalizaron según
   .gitattributes/.editorconfig conservando el maestro byte por byte.
5. La UI infería roster por rol global para permisos mixtos. Se añadió la capacidad
   por grupo calculada en backend y se probó Teacher+Student sin ampliar permisos.
6. Npgsql mostró un aviso de libgssapi ausente durante la migración Linux, aunque
   completó la conexión por contraseña. Compose deshabilita explícitamente GSS
   en este laboratorio sin Kerberos; la migración Docker se verificó nuevamente.

## Pendientes y Iteración 2

No hay confirmación email, recuperación/MFA, invitaciones autoservicio ni auditoría
integral; se mantienen como pendientes explícitos, sin simular servicios externos.
Antes de producción/datos reales: TLS/proxy del mismo origen, mínimo privilegio
PostgreSQL y cifrado de claves Linux/respaldos, más políticas institucionales.
Retención/compartición e inmutabilidad Approved/Published siguen abiertas para las
iteraciones correspondientes, sin modificar el maestro.

Siguiente encargo: exclusivamente Iteración 2, formalizar contrato Mock/proveedores,
Groq, configuración/telemetría y extensibilidad según el maestro. Conservar estos
controles de identidad/CSRF, límites y pruebas de acceso; no avanzar automáticamente.

Inventario de archivos nuevos/modificados: FILES_1.md. README y AGENTS contienen
comandos de preparación, bootstrap, desarrollo, verificación y operación.

Estado al entregar: API/PostgreSQL de Compose y Angular activos para revisión en
https://localhost:4200. Se eliminaron todas las bases temporales, el archivo privado
http-qa.json y los procesos/navegadores de QA. BD principal: una organización,
tres roles y cero usuarios. El administrador inicial se crea conscientemente con
Bootstrap-Admin.ps1; ninguna credencial de prueba sirve para entrar al piloto.
