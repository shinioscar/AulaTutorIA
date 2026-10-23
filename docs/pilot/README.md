# Piloto interno académico — Iteración 9

Solo cuentas, grupos, respuestas y clases ficticias. No es un despliegue con
estudiantes reales, estudio educativo aprobado ni validación de calidad de un LLM.
El maestro permanece intacto. El escenario no necesita cuota gratuita externa.

## Escenario de referencia

SQL Server: **Introducción a los procedimientos almacenados**, 40 minutos
orientativos, cuatro objetivos y cuatro actividades obligatorias:

| Actividad | Objetivo | Tipo | Resultado verificable |
|---|---|---|---|
| e-create | Reconocer la definición | Opción múltiple | Coincidencia con regla privada |
| e-parameter | Identificar parámetro de entrada | Respuesta corta | Coincidencia textual, no comprensión certificada |
| e-exists | Distinguir filas y valores | Verdadero/falso | Coincidencia con regla privada |
| e-procedure | Integrar condición, parámetro y definición | Código | NeedsReview, sin ejecutar SQL |

Fuente única de contenido del piloto: [JSON V2](sql-procedures-v2.json), validado
por el importador real. La referencia es de autoría local, no salida de ChatGPT.
No constituye revisión curricular independiente. Los conceptos se contrastaron con
documentación primaria: definición y lote de [CREATE PROCEDURE](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-procedure-transact-sql?view=sql-server-ver17),
[parámetros](https://learn.microsoft.com/en-us/sql/relational-databases/stored-procedures/specify-parameters?view=sql-server-ver17),
existencia de filas en [EXISTS](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/exists-transact-sql?view=sql-server-ver17)
y separación de lotes mediante [GO](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/sql-server-utilities-statements-go?view=sql-server-ver17).
GO es una orden de herramientas como SSMS; AulaTutor conserva todo el guion como
texto. No se instala ni conecta SQL Server, ni se ejecutan las respuestas.

Un grupo, un profesor aprobado y tres alumnos ficticios en la demo de navegador.
Tres clases publicadas/asignadas: manual conversacional, integrada precargada e
importada V2. Mock genera un guion genérico rotulado; el docente lo aplica y después
lo sustituye explícitamente por la referencia SQL antes de aprobar. Esto verifica
el ciclo académico común, sin atribuir conocimiento SQL a la generación Mock.

A completa cuatro entregas con una pista; B registra cuatro errores, cinco pistas,
una alternativa y su guion abierto. Ambos conservan una actividad NeedsReview.
C queda sin iniciar para una demostración manual posterior. En integración la
cohorte es de dos alumnos; en navegador es de tres, con dos finalizados y uno sin
iniciar. No mezclar estos denominadores.

## Preparación reproducible de demostración

Requiere PostgreSQL Compose activo, .NET/Node configurados, Edge y módulo Playwright
ya instalados. No instala navegadores ni dependencias nuevas. Ejecutar desde raíz:

```powershell
. ./scripts/Initialize-Environment.ps1
./scripts/Verify.ps1 -Smoke -Http
./scripts/Test-E2E.ps1 -PlaywrightModule 'RUTA_ABSOLUTA_AL_MODULO_PLAYWRIGHT' -KeepRunning
```

API y Angular locales deben estar detenidos antes del primer comando de verificación.
Identificar sus PID y rutas con .cache/local-processes.json y Get-CimInstance;
detener exclusivamente los procesos del proyecto verificados. Los scripts abortan
si los puertos 4200/5080/7080 están ocupados o hay otro entorno HTTP QA retenido.
No ejecutar verificaciones simultáneas ni detener PostgreSQL para este recorrido.

El runner crea aulatutor_http_GUID, aplica las siete migraciones y el bootstrap
explícito solo allí. Usa HTTPS, cookies, CSRF y límites normales. Ejecuta regresión,
creación, perfiles A/B, supervisión y reinicio real. Puede tardar varios minutos;
las esperas por ventanas de seis envíos no se eliminan ni se elevan cuotas.
Con KeepRunning conserva **solo después del pase satisfactorio** API/Angular Mock
en https://localhost:4200 y la base temporal. Sin esa opción limpia automáticamente.

Los identificadores y correos del escenario están en .cache/pilot-scenario.json.
El profesor es pilot-teacher@example.invalid y C es pilot-student-c@example.invalid;
A/B son las cuentas ficticias del fixture HTTP. La contraseña aleatoria común
permanece únicamente en el archivo privado .cache/http-qa.json. El operador puede
consultarlo localmente para entrar en la demo; no imprimirlo, versionarlo, pegarlo
en un informe ni capturarlo. No hay contraseña predeterminada en esta guía.

La demo Mock es independiente de la configuración Groq conservada en .env. No
editar ese archivo ni reutilizar una sesión Mock cambiándole el proveedor.
No se crean usuarios, asignaciones o contenidos en la base principal.

## Recorridos y limpieza

- [Guía docente](TEACHER_DEMO.md).
- [Guía estudiantil](STUDENT_DEMO.md).
- [Funcionalidades y límites](FUNCTIONAL_MATRIX.md).
- [Usabilidad observada](USABILITY.md).
- [Rúbrica y prueba real posterior](PEDAGOGICAL_VALIDATION.md).
- [Bloqueantes antes de usuarios reales](READINESS.md).
- [Resultados y metodología](../iterations/ITERATION_9.md).

Al terminar la demostración retenida:

```powershell
./scripts/Stop-PilotDemo.ps1
```

Verifica marcador de propósito, nombre de base, PID y ejecutables antes de parar
los procesos y eliminar exclusivamente la base y los tres archivos privados de
la demo. Nunca elimina la principal. Si detecta un PID reutilizado o metadatos
incoherentes, se detiene para inspección. Una interrupción abrupta requiere el
procedimiento de recuperación de [OPERATIONS](../architecture/OPERATIONS.md).

Restaurar después los servicios habituales con Initialize-Environment y el loader
privado, la API Release en https://localhost:7080 y Angular HTTPS en 4200 con el
proxy existente según [Ejecutar API y web](../../README.md#ejecutar-api-y-web).
Conservar Groq configurado; comprobar únicamente GET /api/health.
No ejecutar el diagnóstico IA Admin para comprobar el arranque. La entrega de 9
restaura esos servicios después de limpiar la demostración de QA.
