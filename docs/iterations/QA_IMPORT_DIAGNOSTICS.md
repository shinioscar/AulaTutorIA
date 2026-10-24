# Corrección de diagnóstico de importación externa V1/V2

Encargo posterior a Iteración 9: investigar un rechazo real de importación docente,
mejorar mensajes y prevenir repeticiones. No es una nueva integración/iteración.

## Causa reproducida

El documento PostgreSQL aportado por el usuario es JSON V2 estructuralmente
compatible: cuatro objetivos/secciones/ejercicios, referencias correctas, opciones
y ayudas válidas. Entrada: **21854 caracteres**. La reconstrucción mediante los
DTO y el serializador de producción ocupa **20038**, frente al máximo **20000**.
Exceso: **38**. La comprobación no cuenta solo texto visible: incluye estructura,
ayudas y escapes Unicode. No se atribuye el fallo a IDs ni respuestas incorrectas.

El validador lanzaba AppException al superar el total y Preview descartaba la causa,
reemplazándola por «Revisa los límites, identificadores únicos…». Las instrucciones
de corrección repetían esa ambigüedad, sin identificar la reducción necesaria.

## Corrección

- Mismo límite, serializador, contratos, permisos y revisión/publicación explícitas.
- Validadores centrales describen campo/posición y regla incumplida, sin reflejar
  textos, claves ni referencias arbitrarias de entrada. El total indica tamaño,
  máximo, exceso y cómo acortar texto redundante.
- V2 identifica errores de ayudas, su correspondencia y primera pista con clave.
- Preview conserva esos mensajes seguros en Errors y CorrectionInstructions.
  Sintaxis/forma JSON continúan con error genérico seguro. Se informa el primer
  incumplimiento de dominio; no se promete listar todos simultáneamente.
- Prompts V1/V2 comparten límites por campo (V2 antes los omitía), explican tamaño
  interno/Unicode y recomiendan margen de brevedad sin garantizar aceptación.
- Sin dependencias, migraciones, reparaciones silenciosas ni llamadas externas.

Se preparó una copia privada de la clase eliminando únicamente la última frase
redundante de `lesson.instructions`, que repetía la revisión previa a publicar.
Resultado **19932 caracteres internos** y Preview válido. Objetivos, ejercicios,
criterios, referencias y ayudas se conservan íntegros. No se guardó, aprobó ni
publicó en la base principal. Original/copia y logs quedan fuera de Git.

## Verificación

Build Release inicial: cero advertencias/errores. Unitarias: **157/157**, incluidas
17 nuevas comprobaciones de diagnóstico: fronteras 19999/20000/20038 con Unicode,
límites por campo, referencias, duplicados, primera pista, privacidad del mensaje,
compatibilidad V1 y restricciones compartidas de los prompts.

Verificación final `Verify.ps1 -Smoke -Http` aprobada sobre el código final:

| Comprobación | Resultado ejecutado |
|---|---|
| .NET Release/restore locked | Cero advertencias/errores |
| Unitarias + integración PostgreSQL | 157 + 199 = **356**, cero fallos/omitidas; conserva las 338 anteriores |
| Integración añadida | Preview y guardado rechazan el mismo campo; cero clases/eventos LessonImported/llamadas de proveedor |
| HTTP HTTPS | **191/191**, fixture temporal limpiado |
| EF | Sin cambios pendientes del modelo; sin nuevas migraciones |
| Angular/TypeScript | npm ci, check y build aprobados; npm informa cero vulnerabilidades |
| Temas/Compose/smoke | 70 pares, configuración Compose y API/proxy HTTPS aprobados |
| Navegador específico | 6 recorridos/aserciones principales, 12 capturas/mediciones, cero errores JavaScript |
| Light/Dark | 320/390/1440, original rechazado/copia válida, sin overflow de página |
| Privacidad/publicación | Diff/enlaces y detector de secretos aprobados; documentos privados excluidos de Git |

Se revisaron tres capturas a tamaño original (rechazo 320 Light/1440 Dark y vista
previa 390 Dark), además de las doce mediciones automáticas. No es auditoría WCAG.
Se comprueban prompt compartido, error exacto, instrucciones de corrección,
conservación del texto (textarea normaliza CRLF a LF), Preview válido y lista de
clases sin cambios. Sin guardar, aprobar o publicar. Las rutas de generación se
bloquean en el navegador; proveedor del fixture Mock. No se repitió la suite completa
de 60 comprobaciones de navegador; se ejecutó el recorrido afectado con el JSON real.

Fuentes: artifacts/import-fix-verify.log, import-fix-http-results.json (191 entradas),
import-diagnostics-e2e.json/log y las doce PNG import-rejected/accepted-*.png.
Runner reproducible: tests/AulaTutor.E2E/import-diagnostics.mjs, con fixture HTTP
aislado/Angular Mock y dos rutas privadas de archivos como argumentos. No incorpora
documentos del usuario al código de pruebas. No se reevaluó un proveedor real.

La limpieza con Stop-PilotDemo eliminó únicamente los procesos/base/estado propios
del fixture. API Release y Angular locales restaurados con Groq configurado, sin
generaciones reales. Maestro y .env intactos; entrega mediante commit local, sin push.

## Capacidad y alcance de los límites

El máximo 20000 es una decisión conservadora del MVP; no es un límite de PostgreSQL
ni del contrato de ChatGPT. Cambiarlo requiere revisar por separado autoría/importación,
contextos IA, admisión de tokens y evaluación final completa. El tamaño interno
escapado no es una medida directa de tokens. Esta corrección no amplía esos límites.
El modelo/importador admiten 24 ejercicios (8 por sección); el máximo 4 corresponde
al prompt/generación precargada en una solicitud, con salida máxima de 4096 tokens.
Una clase grande aceptada por el importador no garantiza que toda la evidencia de
sus alumnos quepa en la evaluación IA; el reporte local se conserva si no cabe.

Reproducciones privadas en artifacts/import-rejection-before.log,
import-rejection-after.log e import-corrected-probe.log; JSON corregido en
artifacts/clase-postgresql-corregida.json. No contienen credenciales. Los documentos
del usuario no se incorporan como fixtures públicos.

No se evaluó calidad curricular ni se solicitó regeneración a ChatGPT/Groq. La
comprobación visual de la pestaña original de Brave se detuvo por no poder verificar
su URL; el diagnóstico se reprodujo con el archivo entregado posteriormente.
