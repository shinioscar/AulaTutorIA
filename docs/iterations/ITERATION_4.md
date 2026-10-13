# Iteración 4 — Evidencias, diagnóstico y aprendizaje personalizado

Fecha: 2026-10-13. Estado: completada, verificada con Mock/proveedores simulados.
Encargo adjunto autoriza exclusivamente Iteración 4 y amplía el maestro con planes
personales generados y ejecutados desde el diagnóstico. No se desarrolló Iteración 5.
Maestro íntegro, SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.

## Resultado funcional

- Preguntas diagnósticas y respuestas explícitamente vinculadas, límites configurables,
  finalización temprana y análisis IA versionado/preliminar según suficiencia de datos.
- Plan personal con 3–5 objetivos, actividades concretas, dificultad/tiempo orientativos,
  diagnóstico/evidencias/proveedor/modelo/prompt de origen y versiones. Ajuste explícito
  de propuesta y aceptación que inicia inmediatamente en la misma conversación.
- Actividad actual/progreso persistente, dudas libres y ayudas sin perder posición,
  pausa/reanudación/recarga. Servidor exige respuestas para avanzar; trabajado no
  significa dominado. Planes independientes de futuras clases LessonVersion.
- Ejercicios libres y de plan identificados/versionados. Intentos conservan respuesta,
  orden, pistas/simplificaciones y validación antes de orientación IA. Cerrados usan
  comparador de reglas privadas; abiertos/código quedan NeedsReview, sin ejecución.
- Reporte explícito versionado con estadísticas PostgreSQL, objetivos trabajados e
  interpretaciones IA vinculadas a intentos existentes. No acepta estadísticas del
  modelo. Resumen local y evidencias sobreviven fallos. Recarga nunca regenera.
- Recomendaciones generales o fundamentadas y creación idempotente de refuerzo
  relacionado, sin incorporar todos los historiales ni generar IA al crear.
- Compartición opt-in de una versión del reporte con docente aprobado/relación vigente;
  permiso durable/revocable. Vista docente mínima solo reportes autorizados. No concede
  acceso a chat, respuestas completas, planes privados ni soluciones.

## Arquitectura y datos

ADR-018/019 y LEARNING_HTTP.md describen las decisiones y contratos. Se amplía
el mecanismo de turnos de 3 con el mismo lock corto/revisión/idempotencia/lease
cercado, sin transacciones abiertas durante IA. La aplicación valida referencias,
estructura, límites y estados. Contexto privado durable para recuperar el mismo
turno tras caída; resultado tardío nunca sobrescribe otro plan/reporte.

Migración aditiva `20261013205535_LearningEvidence`: nuevas tablas LearningExercises,
ExerciseAttempts, DiagnosticAssessments, LearningPlans, LearningReports,
LearningRecommendations y ReportShares; campos de contexto/ejercicio/retry y
EvidenceRevision/origen de refuerzo. Claves compuestas impiden intentos/planes de
otro propietario o recursos de otra sesión. Las dos migraciones previas se conservan.
No se crean usuarios, no se cambian hashes/roles/grupos de la base principal.

Capas: Domain sin frameworks, contratos/validadores/LearningOrchestrator en
Application, LearningWorkflow/LearningService/EF en Infrastructure, endpoints finos
Api y componentes standalone Angular. Sin paquetes nuevos ni cambios de lockfiles.
Se reutilizan exclusivamente IAiProvider y sus adaptadores/telemetría segura.

## Verificación

Primer ciclo completo Verify.ps1 -Smoke -Http: 115 pruebas, 75 comprobaciones HTTP,
restauración locked, compilación, consistencia EF, npm ci/check/build, Compose,
diff y smoke HTTPS. Un segundo Verify completo aprobó las 122 pruebas entonces
existentes y volvió a pasar HTTP/smoke. Después del último caso de borde del reporte,
se repitieron compilación y todas las pruebas .NET (123), HTTP completo (75) y QA
visual. Tras la corrección visual del formulario ante 429 se repitió check/build Angular
y se verificó en navegador la conservación de respuesta y el resto del recorrido.

| Comprobación final | Resultado |
|---|---|
| .NET Release | 0 errores/advertencias; 68 unitarias y 55 integración, 123 aprobadas, 0 omitidas |
| Migraciones PostgreSQL | Tres migraciones, aplicación repetible en bases aisladas y modelo sin cambios pendientes |
| Verify.ps1 -Smoke -Http | Dos ciclos completos aprobados; comprobaciones posteriores del código final descritas arriba |
| HTTP real HTTPS/Kestrel/PostgreSQL | 75 comprobaciones aprobadas, incluidos A/B/C y permisos/revocación |
| Angular estricto/producción | npm ci/check/build y repetición tras corrección visual, sin advertencias |
| Dependencias | NuGet transitive sin vulnerabilidades reportadas; npm audit 409 paquetes, 0 vulnerabilidades |
| QA visual | Escritorio 1440×1000, móvil 390×844 y ancho 320; sin overflow horizontal ni errores JS observados |
| Privacidad/renderizado | Soluciones/contexto privado excluidos; HTML literal sin ejecución; storage del navegador vacío |
| Secretos/Git/maestro | Escaneo de valores privados sin coincidencias, diff --check, locks intactos y hash original |

Las 32 pruebas nuevas cubren diagnóstico/preliminar/consignas, estructura/IDs inválidos,
plan/aceptación/contexto/progreso, intentos/reglas/ayudas, estadísticas/revisiones,
refuerzo, compartición/revocación/relación, CSRF/propiedad, fallos del proveedor,
replay/simultaneidad/cancelación y revocación Student durante generación. Se
añade reporte al límite de cien mensajes sin modificar historial ni reabrir chat. Se
conservan las 91 pruebas previas; solo cambian dos expectativas de número de
migraciones de 2 a 3. Ninguna prueba se omite ni usa InMemory/EnsureCreated.

HTTP real recorrió A/B/C con estudiantes ficticios, PostgreSQL aislado y Mock,
respetando seis envíos/minuto. Soluciones/contexto privado excluidos de respuestas.
La base HTTP temporal se eliminó al terminar. Inventario visual en QA_4.md.

## Proveedores y limitaciones

Groq local y secretos .env se conservan. Todas las verificaciones usan Mock o
proveedor/HTTP simulado, retirando claves externas de procesos temporales. Ninguna
nueva llamada real a Groq/Gemini/Ollama. El usuario informó antes del encargo una
tutoría real satisfactoria con Groq; no se atribuye al agente ni valida estos nuevos
prompts/planes/reportes. La calidad pedagógica real requiere prueba manual autorizada.

Mock es un guion genérico rotulado que demuestra flujo/estados/persistencia, sin
comprensión ni personalización semántica. Las claves/rúbricas propuestas por IA
pueden ser erróneas: un comparador verifica coincidencia, no validez curricular.
Las referencias garantizan pertenencia/trazabilidad, no veracidad semántica de una
interpretación. Abiertos/código requieren revisión humana; no existe sandbox.

Una respuesta abierta permite continuar explícitamente sin certificación. Una ruta
cerrada antes de completar mantiene objetivos pendientes; no se declara completada.
El límite de seis envíos/minuto puede introducir pausas en recorridos extensos.
Contexto acotado (ocho intentos/cuatro mensajes abreviados) puede perder detalle;
las respuestas completas permanecen en PostgreSQL para revisión del propietario.

No hay streaming, motor longitudinal, clases docentes, notas oficiales, panel de
supervisión, integración universitaria, exportación/eliminación autoservicio ni
auditoría integral. Revocación impide nuevas consultas, no borra copias previas.
Políticas institucionales/retención/consentimiento/proveedor siguen pendientes para
datos reales. Mínimo de contraseña de cinco caracteres se conserva por autorización
previa y sigue siendo riesgo para un piloto real. Cuotas por proceso no constituyen
presupuesto distribuido ni exactly-once de facturación externa.

## Entorno y entrega

LearningEvidence aplicada explícitamente a PostgreSQL local. API principal restaurada
con Groq/openai/gpt-oss-20b en HTTPS 7080/HTTP 5080 y Angular HTTPS 4200 activo;
salud consultada mediante proxy, sin generación IA. Cero bases temporales test/http
restantes. Navegadores/contextos cerrados, proceso/base/archivo de QA eliminados.
No se crearon usuarios QA en la base principal ni se modificaron credenciales existentes.
Caches/logs privados siguen ignorados. Inventario en FILES_4.md; 17 capturas en
evidence-4 y resultados detallados en QA_4.md.

La revisión automática rechazó el primer comando combinado de limpieza/reinicio
con «blocked by policy». La limpieza de proceso/base/archivo y el reinicio se
realizaron satisfactoriamente como operaciones separadas con destinos verificados;
no se ejecutó eliminación recursiva del perfil de caché del navegador.

Commit local al cerrar todas las comprobaciones; sin remoto ni push. No se avanza
a Iteración 5. La calidad pedagógica de nuevos prompts con proveedores reales y las
políticas para datos reales permanecen explícitamente pendientes.
