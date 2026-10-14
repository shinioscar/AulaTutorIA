# Corrección Groq posterior a Iteración 5

Fecha: 2026-10-13. Encargo explícito: investigar rechazos de diseño docente,
revisar panel Student y probar Groq real respetando cuota. Solo datos ficticios.

## Inventario de verificación

| Comportamiento | Prueba funcional | Revisión visual |
|---|---|---|
| Propuesta completa con ejercicios | HTTP real, aplicar y revisar; replay sin nueva generación | Editor y resumen docente |
| Diagnóstico, intento, análisis y plan | HTTP real con Student propietario | Diagnóstico y ruta legibles |
| Inicio de ruta, pista y simplificación | HTTP real, conserva actividad y no certifica dominio | Panel y chat en Light/Dark |
| Reporte tras cierre | HTTP real, evidencias privadas conservadas | Reporte orientativo |
| Reintento después de espera | Respuesta HTTP simulada en navegador, GET recupera elegibilidad sin POST IA | Botón y espera visibles |
| Error docente comprensible | Respuesta HTTP simulada, sin cuerpo externo | Aviso español y borrador conservado |
| Móvil | 390 y 320 px, sin overflow global | Consigna, campos y acciones accesibles |

Exploración: espera de cuota, conteo de diagnóstico cuando hay ejercicios libres,
desbordamiento móvil con contenido SQL y conservación de intentos en errores.

## Resultado real y cuota

Se hicieron 37 peticiones reales controladas a Groq, incluidas las exploratorias
que reprodujeron fallos, con separación mínima de 65 segundos y sin 429. No se
ejecutaron herramientas/SQL ni se utilizaron cuentas, clases o chats del usuario.
La cuenta indicó 8000 tokens/minuto; no se confundió ese límite con los 30 RPM.
No hay total inventado de tokens/costos: varias respuestas fallidas no reportan uso.

| Caso | Resultado observado |
|---|---|
| Diseño docente antiguo | HTTP 400 json_validate_failed; objetos convertidos a arrays/cadenas y JSON inválido |
| Diseño v2 | Propuesta completa validada; dos recorridos HTTP de aplicación/revisión correctos, replay con attempts=1 |
| Diagnóstico e intento | Consigna abierta, intento registrado como NeedsReview, sin ejecución SQL |
| Análisis con una evidencia | Preliminar, sin fortalezas/dificultades categóricas; contexto explicita ese requisito |
| Plan personal | Transporte con una actividad por objetivo; tres objetivos en el recorrido real y activación correcta |
| Pista inicial | tool_use_failed: el modelo emitió name/arguments sin herramientas habilitadas; salida descartada |
| Recuperación de pista | Misma clave, tercer intento explícito desde navegador: Succeeded, sin duplicar intento; JSON object/medium |
| Simplificación | Succeeded desde navegador; conserva actividad y registra ayuda |
| Reporte tras cierre | Succeeded; 2 ejercicios presentados, 1 intento NeedsReview, 1 pista, 0 objetivos trabajados; sin nota/dominio |

Muestras de uso realmente reportado: diseño validado 1543 entrada/789 salida;
pista recuperada 1801/361; simplificación 1845/212. El reporte al cierre no crea
otro mensaje de chat con Usage: no se le atribuye un contador ausente de su DTO.
Se probaron razonamiento/presupuesto/formato por separado: aumentar tokens o
reducir razonamiento no resolvía por sí solo el constructor anidado. ADR-022
describe el contrato final, sin fallback ni activación de funciones.

## Navegador y evidencia

- Login y navegación reales con cuentas ficticias de una BD aislada. Tema Light/Dark,
  editor, actividad, ruta y reporte inspeccionados; anchos 1440/390/320 sin overflow global.
- Error y espera docentes simulados por intercepción GET: botón deshabilitado durante
  espera y habilitado después, sin POST IA. Texto español, borrador conservado.
- Espera Student simulada: un GET manual y otro al vencer RetryNotBefore habilitan
  recuperación, sin reenvío automático. La pista real se reintentó después por clic explícito.
- Conteo simulado: cinco ejercicios libres más uno diagnóstico no agotan el límite
  de cinco preguntas diagnósticas. SQL se renderiza en bloque seguro con scroll propio.
- La revisión detectó aviso de carga persistente tras GET exitoso; se añadió limpieza
  de ese aviso, conservando mensajes de envío incierto/conflicto. Comprobación final en
  la misma vista: GET 503 → aviso → GET 200 → aviso eliminado, con cero POST.

Doce capturas en `evidence-groq-fix/`. `teacher-error*`, `teacher-final-error-staged*`,
`student-load-recovered-staged*`, `student-wait*` y
`student-code-count-staged*` muestran estados simulados de UI; los archivos
`student-*-real*` y `teacher-success-light.png` muestran resultados reales ficticios.
La captura de conteo inicial incluye el aviso de carga antiguo que motivó el ajuste.
No se certifica auditoría WCAG ni exactitud curricular de todas las respuestas IA.

## Regresión y cierre

174 pruebas (100 unitarias/74 integración) aprobadas en la verificación final.
Un pase intermedio tuvo cuatro timeouts de PostgreSQL al eliminar bases de prueba,
sin fallos de aserción funcional; las cuatro bases huérfanas se eliminaron con nombres
validados. La repetición final completa pasó las 74 pruebas sin omisiones.
Build/restore locked, consistencia EF, TypeScript/Angular y 70 controles de tema correctos.
Smoke HTTPS/HTTP relativo correcto con Mock; 107 comprobaciones HTTP aprobadas.
El maestro conserva su SHA256 original. Bases/cuentas/estado privado Groq de QA eliminados;
la configuración .env no se modificó. Cero bases temporales al cerrar; API principal
restaurada con Groq/openai/gpt-oss-20b y Angular HTTPS activo. Salud consultada sin
generación. Commit local de la corrección, sin remoto/push ni avance a Iteración 6.
