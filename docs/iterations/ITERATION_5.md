# Iteración 5 — Constructor docente, simulaciones y versiones

Fecha: 2026-10-13. Estado: completada y verificada. Alcance: encargo adjunto de
Iteración 5, sin avanzar a 6.
Maestro original intacto, SHA256
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
Se revisaron AGENTS/README, maestro completo, informes 0–4, ADR/ambigüedades,
motor/contratos/entidades existentes y guías UI-0 antes de implementar.

## Resultado funcional

- Constructor Angular: Mis clases/crear, datos generales, objetivos, secciones con
  orden por botones, cinco tipos de actividad, criterios privados, soluciones y
  reglas de pistas. Borradores y actividades incompletas guardables; requisitos
  completos solo antes de aprobar. Sin drag-and-drop ni rediseño de dashboards.
- Conversación de diseño Plan/Content/Exercises/Rubrics con IAiProvider y propuestas
  completas estructuradas; validación local en todos los formatos/proveedores.
  Aplicar/descartar explícitos; una generación no cambia el borrador. Revisión
  de origen impide sobrescribir una edición manual posterior.
- Simulación por snapshot de revisión, contexto y conversación persistentes,
  respuestas correctas/incorrectas/abiertas, pistas, simplificación, dudas, avance,
  detener/regresar/reabrir. Reutiliza TutoringOrchestrator y ExerciseValidators.
  Guarda respuestas y resultados antes de orientación IA; retry no los duplica.
- Aislamiento: solo LessonSimulation/LessonOperation; no tutorías/evaluaciones del
  docente, estadísticas estudiantiles, membresías, asignaciones ni chats privados.
  Claves/soluciones no se envían al modelo conversacional ni a la proyección del ensayo.
- Revisión docente con errores/advertencias y resumen. Aprobación crea LessonVersion
  completa, numerada, fechada, con responsable/hash; publicación es otra acción.
  Nuevo borrador permite otra versión sin alterar la anterior. Archivo conserva
  contenido. Triggers PostgreSQL protegen versiones, revisiones y origen de simulación.
- Autorización de propietario Teacher activo/aprobado; Admin ordinario/Student/
  Pending/Teacher ajeno sin acceso. HTTPS/CSRF, límites DTO, claves idempotentes,
  revisión y locks cortos, lease cercado, retry/cancel seguro y revalidación de rol
  después de generación. Sin transacción durante IA ni fallback externo.
- Temas Light/Dark y controles/tokenización UI-0; renderizado seguro por interpolación
  y SafeResponse existente. No nuevos paquetes, fuentes, SDK ni servicios externos.

## Datos, contratos y decisiones

Migración aditiva `20261013233137_TeacherLessons` después de InitialIdentity,
PrivateTutoring y LearningEvidence, intactas. Nueve tablas: Lessons, LessonDrafts,
LessonDraftRevisions, LessonVersions, LessonPublications, LessonProposals,
LessonSimulations, LessonOperations y LessonCommands. No seeds de usuarios ni
cambios de hashes/roles/grupos. Valores académicos completos en JSON validado;
no duplica entidades de evidencia real. Responsable/fecha/revisión/hash hacen
reproducible el contenido, sin prometer respuestas IA idénticas.

ADR-021 resuelve Approved/Published y aislamiento; LESSONS_HTTP.md describe
endpoints/DTO/límites. README, AGENTS, índice/plan/preguntas abiertas y QA actualizados.
Las futuras asignaciones deberán apuntar a LessonVersion.Id específico. No existen
LessonAssignment, ejecución por alumnos ni endpoints de edición de versiones.

## Verificación

Base inicial: Verify.ps1 -Smoke -Http pasó con 123 pruebas (68/55), 75 HTTP,
70 pares de contraste y resto de comprobaciones. Primer ciclo ampliado pasó
152 pruebas (80/72) y 107 HTTP. El segundo ciclo completo pasó 154 (81/73) y
107 HTTP. Tras añadir la base de propuesta conversacional se repitieron build y
todos los tests: **155 (81 unitarias/74 integración)**, cero omitidas. QA funcional
y visual completada con Mock; 26 capturas y detalles en QA_5.md. La verificación
de entrega repite el ciclo completo sobre el código final.

| Verificación final | Resultado |
|---|---|
| Verify.ps1 -Smoke -Http | Completo, satisfactorio sobre código final |
| .NET Release / tests | 0 advertencias/errores; 155 aprobadas (81 unitarias/74 integración), 0 omitidas |
| EF / PostgreSQL real | Cuatro migraciones reproducibles, modelo sin cambios pendientes; inmutabilidad/aislamiento/concurrencia probados |
| HTTP HTTPS/Kestrel | 107 comprobaciones, incluidas las 75 anteriores y 32 nuevas docentes |
| Angular / TypeScript | npm ci/check/build producción y repetición tras ajustes visuales; sin advertencias |
| Light/Dark | 70 comprobaciones de contraste/tokens; 26 capturas y QA escritorio/móvil/320 |
| Docker Compose | Configuración y build Linux de API (restore locked/publish) satisfactorios; imagen local sin arrancar otra API |
| Dependencias | NuGet transitive sin vulnerabilidades reportadas; npm ci sin vulnerabilidades reportadas; locks intactos |
| Seguridad / Git | 0 coincidencias con secretos configurados en texto versionable, diff --check y hash original del maestro |
| Limpieza | 0 bases temporales test/http, archivo privado QA eliminado; navegadores/procesos de QA cerrados |

Las 32 nuevas pruebas cubren creación incompleta/idempotencia, edición/orden/histórico,
propuestas válidas/inválidas/aplicación/descarte/desactualización, rúbricas,
simulación multiturmo/respuestas/ayudas/aislamiento/reapertura, revisión/aprobación/
publicación/segunda versión, inmutabilidad PostgreSQL, CSRF/roles/propiedad,
concurrencia/replay/cancelación/lease vencido/timeout/cuota/cambio de modelo y
revocación docente durante generación. PostgreSQL real, bases aisladas eliminadas,
sin InMemory/EnsureCreated ni pruebas omitidas. Las 123 previas se conservan;
solo cambian expectativas de migraciones 3→4 y de ruta futura→ruta protegida.

## Proveedores y límites

Todas las pruebas, HTTP y navegador usan Mock o proveedor/HTTP controlado; ninguna
nueva llamada real a Groq/Gemini/Ollama. Groq local/.env/claves privadas conservados.
Mock ofrece guiones rotulados, sin comprensión semántica ni planificación curricular
real. La calidad de los nuevos prompts, claves/rúbricas y enseñanza con proveedores
reales requiere revisión manual autorizada posterior. No se atribuyen al agente
las pruebas reales previas informadas por el usuario.

Hay límites piloto de tamaño/revisiones/turnos/propuestas y seis acciones IA/minuto
por docente/proceso. Ventana corta puede omitir contexto antiguo; historial durable
conservado. Una salida válida estructuralmente no demuestra veracidad pedagógica.
Abiertos/código quedan NeedsReview; un comparador cerrado verifica coincidencia,
sin calificación oficial ni dominio. Simulación permite avanzar tras ensayar errores
y no garantiza equivalencia exacta con futuros recorridos estudiantiles.

Solo datos ficticios locales, sin borrado automático. Retención/borrado institucional,
consentimiento, exportación, confirmación email/MFA, auditoría integral, presupuesto
distribuido y exactly-once de facturación externa siguen pendientes. Triggers de
inmutabilidad requerirán un proceso de borrado futuro expresamente autorizado.
La política de contraseña de 5–128 se conserva; mínimo de cinco sigue siendo
insuficiente para un piloto real. No hay integración universitaria ni ejecución SQL.

## Incidencias y entorno

Una aserción comparaba JSON durable como texto: jsonb normaliza orden y espacios.
Se corrigió a igualdad semántica de JSON. Dos intentos adicionales de compilación
coincidieron con la API temporal y Windows bloqueó DLL; se repite compilación al
cerrar el proceso propio, sin detener servicios ajenos. La primera plantilla de
simulación tenía una comparación sin espacios que Angular interpretaba como etiqueta;
se trasladó al método de componente y el build pasó.

QA final registra 26 capturas, sin errores JavaScript observados ni overflow a 320/390/1440.
Se corrigieron la etiqueta de desactualización en propuestas resueltas, altura de
campos/objetivo visible en móvil, retroalimentación junto a actividad y lecturas GET
anteriores a una mutación. Error de cuota por intercepción y Timeout preparado en BD
QA se identifican como simulados; los controles de retry/cancel usaron backend/Mock
real. Durante automatización se sincronizó la actualización de índices tras reordenar;
una espera sobre el antiguo botón deshabilitado no era un fallo de persistencia.

El primer comando de limpieza alcanzó procesos propios pero terminó antes de eliminar
la base temporal; se repitió con filtro por ejecutable y pertenencia al proyecto.
API/base/archivo privado de QA y Angular temporal eliminados; perfil y logs ignorados.
No se tocaron procesos ajenos ni usuarios de la base principal. TeacherLessons aplicada
explícitamente a PostgreSQL local, sin bootstrap ni cambios de cuentas. API principal
restaurada con Groq y Angular HTTPS; salud solo lee configuración, sin generación.
Commit local de cierre con verificación satisfactoria; sin remoto ni push. No avanzar a 6.
