# ADR-019 — Reportes orientativos versionados y permiso mínimo

Fecha: 2026-10-13. Estado: implementado y verificado en Iteración 4.
Autorizado por el encargo de Iteración 4. No es panel de supervisión docente.

LearningReport almacena una revisión inmutable de observaciones IA, referencias a
intentos entregados y estadísticas calculadas desde PostgreSQL. No acepta estadísticas
del modelo ni porcentajes de dominio. Correctos sin/con ayuda cuentan ejercicios
distintos; los resultados incorrectos/no evaluables/pendientes cuentan intentos. Si
un ejercicio tiene un acierto sin ayuda se cuenta una vez en esa categoría, evitando
duplicarlo en aciertos con ayuda. Mensajes no se consideran aprendizaje. EvidenceRevision
cambia al registrar consignas, intentos, pistas entregadas y progreso; una revisión
anterior se muestra desactualizada. Hasta diez revisiones por sesión. Una nueva clave
no regenera una revisión vigente sin cambios; replay de la misma clave recupera el
resultado. El resumen local de cierre sigue disponible sin IA.

LearningRecommendation conserva reporte/sesión de origen, meta y evidencias. De una
a tres recomendaciones; con evidencia insuficiente se muestran como generales. El
refuerzo crea sesión privada propia idempotente, enlaza recomendación/reporte y
transfiere solo tema/meta/nivel. No mezcla historiales, no genera IA al crear y no
confiere permisos nuevos. Puede seleccionarse una recomendación histórica sabiendo
que corresponde a su reporte versionado.

ReportShare registra destinatario, versión report-only-v1 de autorización explícita,
fecha, clave idempotente y revocación. El estudiante revisa exactamente el reporte
que compartirá. Compartir exige un reporte vigente y docente activo/aprobado con
rol Teacher y relación verificable: membresías Student/Teacher en el mismo grupo
no archivado de la organización. La lectura vuelve a verificar esa relación y permiso.
Retirar relación, archivar grupo, retirar rol/aprobación o revocar impide nuevas
consultas. Hasta treinta autorizaciones por sesión. Una clave revocada no revive
el permiso; autorizar nuevamente requiere otra acción/clave.

Se comparte únicamente la versión del reporte: resumen, estadísticas, observaciones,
recomendaciones, proveedor/modelo/prompt y IDs de evidencias. No se expone endpoint
docente para respuestas completas, claves, historial, diagnóstico o plan privado.
La redacción IA puede mencionar contenido pertinente de respuestas: se informa en
la vista previa; el reporte necesita revisión del alumno antes de autorizarlo.
Teacher X/Y y Admin sin consentimiento/recurso reciben 404. Un rol Admin adicional
no crea permiso; los roles mixtos deben cumplir relación y autorización como Teacher.
La revocación no elimina copias ya obtenidas. No hay acceso extraordinario Admin.

La vista docente mínima permite consultar reportes autorizados (máximo 100), sin
roster analítico, notas, revisión oficial o integración universitaria. Política
institucional, retención, eliminación, exportación, anotaciones y auditoría integral
permanecen pendientes para un piloto real y las iteraciones posteriores.
