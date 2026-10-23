# Operación técnica — Iteración 8

## Salud y correlación

GET /api/health sigue siendo liveness/configuración, anónimo y sin red IA o DB.
GET /api/admin/operations/readiness exige sesión Admin y actor activo. Tras
autorizar consulta conectividad PostgreSQL con plazo de tres segundos. Devuelve
200 ready o 503 unavailable, database booleano, aiAvailabilityChecked=false y
correlationId; no representa comprobación de migraciones ni disponibilidad IA.
La autenticación también depende de PostgreSQL: una caída puede impedir autorizar
la sonda. NpgsqlException devuelve 503 genérico con correlación desde el pipeline;
se prueba el login con una base inexistente del PostgreSQL del proyecto. No se
expone conexión ni nombre de base y no se habilita autenticación sin almacenamiento.

X-Correlation-ID es generado por servidor para cada solicitud y se incluye en
ProblemDetails. No se acepta como identificador confiable el enviado por cliente.
No contiene sesión, usuario, correo ni texto académico. Conservarlo al reportar un
error. Referrer-Policy no-referrer y X-Frame-Options DENY se aplican a respuestas
API; la política de documentos Angular deberá validarse en el proxy de despliegue.
No se declara CSP completa ni auditoría de accesibilidad/seguridad certificada.
El servidor limita cuerpos a 1 MiB; Content-Length excesivo recibe 413 antes de
deserializar. Kestrel limita también cuerpos transmitidos sin Content-Length.

## Cuotas y diagnóstico sin llamadas externas

ADR-025 define ventanas, reservas y parámetros AiQuota del backend. Consultar
GET /api/admin/operations/usage y /audit?page=1 como Admin activo. Ninguno genera IA.
unknownUsage no equivale a cero tokens. Succeeded describe transporte recibido,
no calidad académica. Uncertain/Reserved vencido exige revisar el intento durable
antes de usar el retry explícito; no borrar reservas para sortear presupuestos.
Cambiar límites o AiQuota__AllowExternalCalls requiere configuración privada y
reinicio controlado; mantener política idéntica en todas las instancias.
No cambiar proveedor de sesiones iniciadas ni reenviar a otro tercero por cuota.

## Verificación completa y navegador

```powershell
./scripts/Verify.ps1 -Smoke -Http
# Con API/web del proyecto detenidas y Playwright ya disponible:
./scripts/Test-E2E.ps1 -PlaywrightModule 'RUTA_AL_MODULO_PLAYWRIGHT_INSTALADO'
```

El runner E2E prepara datos ficticios con Http-Identity, fuerza Mock y vacía claves
externas del proceso temporal, abre Edge instalado y ejecuta UI/publicación/clase/
revisión, 429 simulado y respuesta perdida después de commit. Respeta seis envíos
por minuto, sin elevar cuotas. Test-RestartRecovery conserva cookie y estado tras
reinicio real de esa API y comprueba replay sin duplicar. Finalmente cierra solo
sus procesos, elimina la base aulatutor_http_GUID y su archivo privado de estado.
Si se interrumpe el proceso padre, validar PID/ruta y el nombre exacto de base
antes de la limpieza manual. No imprimir .cache/http-qa.json ni credenciales.

Las pruebas BoundedMockLoad y DashboardQueryCount generan JSON privado en artifacts
con p50/p95, recursos y consultas. Puede repetirse con el filtro dotnet test;
requiere PostgreSQL real/loader y no sustituye E2E ni prueba rendimiento de Groq.
La suite completa conserva revocación, leases, respuestas tardías, fallos 429/5xx,
cancelación y timeout del motor. No lanzar dos builds/verificaciones a la vez.

## Ensayo de respaldo/restauración

Con PostgreSQL Compose activo y API detenida para comparar un snapshot controlado:

```powershell
./scripts/Test-BackupRestore.ps1
```

Usa pg_dump custom sobre la base principal, una instantánea consistente de lectura;
crea exclusivamente aulatutor_restore_GUID desde template0 y restaura mediante
pg_restore --single-transaction --exit-on-error. Verifica historial de al menos
las migraciones presentes y elimina destino y archivo temporal en finally. No levanta
API contra la copia, no llama proveedores ni modifica/borra datos principales.
No imprime credenciales, filas académicas o contenido del archivo.
Compara exactamente el número de migraciones y los conteos de usuarios, versiones,
intentos, mensajes, revisiones y membresías entre fuente y copia; no publica filas.
El archivo temporal se crea con umask 077 y se comprueba modo 600. Su eliminación
se intenta incluso si falla la eliminación de la base aislada.

Esto es un ensayo de recuperación lógica, no una política de respaldos durables.
El archivo temporal está dentro del contenedor PostgreSQL y se elimina al terminar;
no sirve para recuperar una pérdida posterior del volumen. Para un piloto real hay
que aprobar ubicación privada cifrada fuera del volumen, periodicidad, retención,
control de accesos, RPO/RTO y restauraciones periódicas. Data Protection y secretos
necesitan respaldo/cifrado separado: pg_dump no los incluye. No versionar copias.
Ante una interrupción abrupta revisar destinos aulatutor_restore_GUID sobrantes y
eliminar solo la base exacta generada por este procedimiento, nunca la principal.

Para una recuperación real autorizada: detener escrituras del proyecto, restaurar
primero una copia aislada, verificar versión/migraciones/integridad y acceso por
roles, recuperar las claves Data Protection por el procedimiento de cifrado aprobado
y decidir explícitamente el cambio de conexión. No sobreescribir la base principal
con el script de prueba. Tras una caída, GET recupera leases vencidos sin generar IA;
un usuario examina el intento persistido y elige retry de la misma clave si procede.
Un replay completado devuelve lo guardado. Reiniciar nunca implica refund externo.
