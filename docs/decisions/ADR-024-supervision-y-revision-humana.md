# ADR-024 — Supervisión y revisión humana de clases asignadas

Fecha: 2026-10-22. Encargo explícito de Iteración 7. Maestro v1.0 intacto.

## Finalidad y relación vigente

La supervisión es una proyección académica, distinta de la conversación. Solo el
docente responsable de LessonAssignment, activo/aprobado y con rol Teacher y
membresía Teacher vigente, consulta sus asignaciones en grupos no archivados.
Un co-docente sin responsabilidad sobre esa asignación no recibe acceso. Admin
ordinario no tiene excepción. El estudiante debe seguir activo, con rol Student
y membresía actual del grupo. La cohorte y todos los denominadores se recalculan
con esa relación, sin incluir exmiembros ni alumnos desactivados.

Withdrawn cierra la supervisión docente; Inactive, fechas vencidas y publicación
archivada permiten revisar el trabajo histórico mientras la relación siga vigente.
Retirar membresías, archivar grupo o revocar al docente impide nuevas consultas y
escrituras (404), sin borrar revisiones. Reincorporar recupera el historial si la
asignación no fue retirada. El estudiante conserva la lectura propia de feedback
según AssignedClassAccess, incluso en asignación retirada, sin acceso a compañeros.

La proyección docente contiene consignas, respuestas explícitas a ejercicios,
resultados del comparador, ayudas y decisiones humanas; nunca TutoringMessage,
contextos de prompts, claves privadas, diagnósticos o planes personales. Los reportes
privados conservan el consentimiento/revocación de ADR-019. Las observaciones IA
de una clase solo se proyectan si sus referencias pertenecen a intentos de esa
clase; no se expone el resumen libre del reporte ni interpretaciones sin evidencia.

## Métricas y señales

Se separan estudiantes únicos, pares alumno/ejercicio e intentos. Abrir una sesión
Created no significa comenzar conversación. Entregar una respuesta no significa
coincidir con reglas; completar requisitos usa las reglas de ADR-023, incluso
NeedsReview en abiertos, sin declarar correcto lo no verificado. Los aciertos por
ejercicio se cuentan una vez, priorizando sin ayuda si existe. Los resultados de
intentos conservan sus categorías históricas y no se sustituyen por revisiones.
Cada objetivo se toma de la versión exacta; refuerzos sin identidad aprobada se
presentan aparte y no inflan métricas del núcleo. Sin porcentajes de dominio.

La cola/casos son proyecciones deterministas por ejercicio: último intento
NeedsReview/NotEvaluable; tres errores sin ninguna coincidencia registrada en ese
ejercicio; diez intentos sin coincidencia; o marca docente explícita. No se infiere motivación por inactividad,
mensajes ni tiempo. GET no crea filas. Una nueva evidencia posterior a la revisión
reabre el pendiente. Cerrar un caso no aprueba ejercicios ni reinicia intentos.

## Decisiones e historial

TeacherReview identifica una clase/sesión y opcionalmente un ejercicio. Las entradas
TeacherReviewEntry son inmutables, con autor/fecha, comentario, observación,
recomendación, resultado humano formativo, estado de revisión y seguimiento.
ReviewEvidence referencia los intentos examinados con FK compuesta a la sesión.
Estas estructuras cubren revisión, comentarios, feedback y atención sin duplicar
respuestas ni crear mensajería general. Todos sus textos son visibles al alumno.
No hay notas oficiales ni cambios sobre evidencia primaria. El feedback requiere una
sesión abierta por el estudiante; consultar el roster Sin abrir no crea sesiones ni
evidencia en nombre del alumno.

Escrituras con CSRF, GUID/hash idempotente, revisión optimista y EvidenceRevision.
Lock corto asignación→sesión, compartido con el motor, con revalidación de relación
después de esperar; cada decisión examina una instantánea vigente. Reenvío idéntico
recupera la decisión; clave con otros datos
o revisión vieja da 409. Máximo cien entradas por destino y diez referencias por
decisión. Un resultado humano fundamentado requiere comentario, observación y
referencias válidas. Persistencia atómica, sin llamadas externas ni transacciones
durante IA. Triggers protegen origen e historial, no se alteran migraciones previas.

## Alcance y pendientes

Se priorizan panel, reportes deterministas y revisión humana. El resumen cualitativo
grupal por IA es opcional en el encargo y se difiere para definir anonimización y
referencias agregadas sin reenviar respuestas personales. No se genera IA al consultar
supervisión; los reportes orientativos existentes continúan funcionando con
IAiProvider. Estadísticas/revisiones no dependen del proveedor ni de su disponibilidad.

Remediación de intentos agotados, reapertura académica, retención institucional,
auditoría integral (8), exportación y datos reales requieren decisiones posteriores.
No se cambia contraseña mínima cinco ni se elimina su riesgo documentado. Solo
datos ficticios, pruebas Mock/controladas y PostgreSQL aislado; cero cuota real.

## Parche compatible de herramientas de desarrollo

El audit de npm de esta sesión (también en la línea base, antes de cambios) reportó
cuatro paquetes afectados: CLI/build y sus transitivas MCP TypeScript SDK/Vite.
Se actualizan exclusivamente @angular/cli y @angular/build de 21.2.26 a 21.2.28,
con versiones exactas y package-lock. El runtime/compiler-cli 21.2.25, TypeScript
5.9.3, Node y .NET se conservan; no se añaden dependencias directas ni SDK IA.
El SDK MCP es una dependencia previa de desarrollo del CLI, no un proveedor IA.

Metadatos oficiales comprobados con npm view: CLI 21.2.28 depende de SDK 1.32.1;
build 21.2.28 usa Vite 7.3.7 y admite Angular ^21.0.0 / TypeScript >=5.9 <6 / Node
>=24.0.0. npm audit posterior informa cero vulnerabilidades. Se repiten instalación
locked, TypeScript, temas, Angular y smoke tras el parche, sin cambios de C#.

Fuentes primarias: [metadatos CLI](https://registry.npmjs.org/@angular%2fcli/21.2.28),
[metadatos build](https://registry.npmjs.org/@angular%2fbuild/21.2.28) y
[aviso del mantenedor MCP](https://github.com/modelcontextprotocol/typescript-sdk/security/advisories/GHSA-22jm-h49p-29qw).
