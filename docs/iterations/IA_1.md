# IA-1 — Prompt externo e importación segura docente

Fecha: 2026-10-23. Estado: **completada y verificada**. Base: OPT-1,
commit 7adbece. Encargo explícito IA-1; no autoriza IA-2, Iteración 9 ni push.

## Auditoría y decisiones

Git limpio al inicio. Se leyeron AGENTS/índice y ADR-012/021/022/025/026, contrato
docente, guías de diseño, código de validadores, revisiones/propuestas/versiones,
servicios, endpoints y UI-1. Maestro consultado para alcance, sin cambios.
La línea base histórica UI-1 es 290 pruebas, 191 HTTP y 25 comprobaciones E2E;
no se atribuyen como ejecutadas hasta registrar los resultados siguientes.

Se reutiliza el transporte poco anidado LessonDesignPayload dentro de un sobre
versionado, no un sistema académico paralelo. El generador y el importador usan
el mismo esquema derivado del tipo. Revisiones/propuestas/versiones actuales
siguen intactas. No se crea una propuesta IA falsa ni se llama a IAiProvider.
La importación crea **clase nueva**, con vista previa y guardado explícito, sin
reemplazar borradores ajenos o antiguos. Véase ADR-027 y LESSON_IMPORT.md.

## Implementación

- Formulario UI-1 compartido: dos acciones diferenciadas, opciones avanzadas
  existentes conservadas; volver desde el prompt mantiene la configuración.
- Prompt determinista personalizado, contrato real, copiar y enlace oficial sin
  transmisión automática. Herramienta externa compatible, sin API key obligatoria.
- Pegar o leer .json local (128 KiB/32000 caracteres); validación en servidor,
  errores seguros y corrección manual mediante instrucciones copiables. Sin reparación
  automática ni eliminación de Markdown. Alternativa de selección manual al clipboard.
- Vista previa educativa LessonSummary/SafeResponse, no documento JSON. Soluciones
  privadas plegadas; aviso de revisión pedagógica y advertencias de evaluación humana.
- Validación estricta de formato/versión/shape, campos, referencias, IDs, reglas,
  opciones, rúbricas, solución privada, pistas y límites; profundidad 12, entrada
  32000 y contenido 20000 caracteres. Límite global HTTP 1 MiB conservado.
- Guardado con revalidación del contenido y actor después del lock, serialización,
  historial inicial e idempotencia existentes. LessonImported atómico y mínimo,
  sin duplicado al replay. Revisión optimista posterior conserva los conflictos.
- Ciclo normal: editar, guardar, simular aislado, revisar, aprobar, publicar y asignar.
  Ninguna aprobación/publicación implícita ni modificación del motor del estudiante.

## Verificación ejecutada

Verify.ps1 -Smoke -Http aprobó 312 pruebas (135 unitarias/177 integración), sin
omitidas; incluye las 290 previas. Restore locked, Release sin advertencias,
consistencia EF, npm ci/check/check:theme/build, 70 controles de contraste/tokens,
Compose config, diff, smoke y 191 comprobaciones HTTP aprobados. Log privado:
artifacts/ia-1-verify.log. Auditoría NuGet sin paquetes vulnerables reportados;
npm ci no reportó vulnerabilidades. Ninguna dependencia nueva.

Después de incorporar la auditoría y revalidación tras lock se recompiló el backend
y se repitió la suite LessonTests (incluye clases/asignaciones/supervisión/carga):
107 aprobadas y un fallo de coordinación de una prueba nueva. El observador de
pg_stat_activity conservaba una instantánea en la transacción de bloqueo. Se
añadió pg_stat_clear_snapshot y una espera acotada, sin cambiar código de producto
ni relajar la aserción; el caso volvió a ejecutarse y aprobó (1/1). Logs:
ia-1-final-backend.log e ia-1-lock-test.log. **313 casos distintos aprobados entre
los pases: 135 unitarios/178 de integración**; no se presenta como un único pase
completo de 313. Los otros 107 no se repitieron tras modificar solo ese observador.
El caso prueba que un docente revocado mientras espera no persiste clase ni evento.

Primer build detectó CS8604 en una aserción nueva; corregido sin relajar nulabilidad.
Hash del maestro y 88 enlaces Markdown locales verificados. El control de seguridad
de publicación revisó textos actuales e historial sin coincidencias con secretos
configurados. .env, certificados, caches y estados privados continúan excluidos.

Edge/Playwright existentes: **34 comprobaciones aprobadas** (11 funcionales y 23
capturas/mediciones), conservando las 25 anteriores. Cero errores JavaScript y
overflow global observado cero en 320/390/1440, Light/Dark. Copia real de prompt e
instrucciones, retorno a opciones, errores del backend, lectura de .json, vista
previa, pérdida de respuesta de guardado y replay sin duplicación, edición,
simulación, aprobación/publicación explícitas y asignación de la clase importada.
Student ejecuta esa clase y completa/revisa feedback; su DTO no contiene soluciones
de referencia. Teacher ajeno y Student no leen/publican el recurso privado.
El entorno de navegador volvió a aprobar 191 HTTP con el backend final.

Primer pase E2E detectó CRLF en el portapapeles de Windows frente a LF en el
textarea; se ajustó solo la aserción de saltos de línea y se repitió el runner
completo. No se cambió el prompt ni el JSON académico. Resultado:
artifacts/ia-1-e2e.json; log final ia-1-e2e-final.log, ambos privados.
Las 23 PNG se revisaron en conjunto y cuatro pantallas nuevas también a tamaño
original. Evidencia en [evidence-ia-1](evidence-ia-1/), por ejemplo
[prompt Light](evidence-ia-1/external-1440-light.png),
[móvil Dark](evidence-ia-1/external-320-dark.png),
[corrección](evidence-ia-1/import-correction-dark-390.png) y
[vista previa](evidence-ia-1/import-preview-light-1440.png).
No constituye una certificación WCAG ni validación pedagógica de ChatGPT.

Reinicio real de API aprobado: cookie, historial confirmado y replay idempotente
conservados con Mock. Limpieza final: cero bases test/http/restore del patrón QA y
sin .cache/http-qa.json. API y Angular locales restaurados; salud por el proxy
confirma Groq configurado sin llamadas de generación. Evidencia privada:
artifacts/ia-1-restored.json. PostgreSQL principal y sus datos no se modificaron
durante QA; no se alteraron .env, claves ni contraseñas.

Maestro SHA256 final:
FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
Revisión visual de PNG ficticias separada de la revisión textual de secretos.
Cierre con commit local tras diff/seguridad; no push ni avance a IA-2.

Las pruebas nuevas cubren prompt determinista/opciones/esquema, importación válida
multisección y preservación, sintaxis, versión, campos/null/duplicados, IDs,
referencias, tipos, soluciones, rúbricas/pistas, tamaño/profundidad, mensajes y
corrección. Integración PostgreSQL aislada cubre preview sin escrituras, edición,
simulación segura, aprobación/publicación, inmutabilidad, reenvíos concurrentes,
colisión de clave, roles, CSRF y revocación mientras espera el lock.

El runner existente conserva los recorridos anteriores y añade prompt/copiar,
retorno a opciones, corrección, archivo, vista previa, edición/simulación/publicación
importada; usa esa clase para la asignación y recorrido Student posteriores,
comprobando ausencia de referencias privadas y permisos de otro docente/Student.
QA visual: Light/Dark en 320/390/1440 y capturas de error/vista previa.

## Límites y riesgos

JSON válido no certifica corrección pedagógica, curricular ni buenas instrucciones.
La QA utiliza datos ficticios/Mock; no valida respuestas reales de ChatGPT ni consume
cuota externa. Ninguna automatización de cuenta externa, descarga o nueva API.
Los errores de estructura complejos pueden ser generales; se ofrecen instrucciones
de corrección. No hay detección semántica de soluciones filtradas en contenido público:
requiere revisión docente antes de publicar. Lecturas Student excluyen los campos
privados; textos continúan renderizados de forma segura.

Se conserva solo el contenido validado en el sistema académico existente, no el
archivo original/proveedor externo. Una edición del texto tras un envío confirmado
requiere otra importación explícita; reintentar el mismo envío usa su clave. No se
implementa reemplazo de borrador, IA-2, retención/borrado ni nuevas notas oficiales.
Contraseña mínima y riesgos institucionales previos permanecen; no se declara
preparado un piloto real. Sin cambios al maestro, credenciales ni datos principales.
