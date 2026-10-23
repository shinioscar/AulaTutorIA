# Demostración estudiantil — datos ficticios

Usar el [entorno aislado](README.md). C está inscrito pero sin sesiones iniciadas;
permite recorrer manualmente cualquiera de las tres clases. A/B conservan los
resultados del guion automatizado para comparar historiales.

## Clase precargada o importada V2

1. Mis clases → abrir el detalle SQL deseado → Iniciar clase → Comenzar clase
   precargada. También hay un botón Iniciar directamente en la lista.
2. Leer el objetivo y una actividad a la vez. Las opciones y el campo de texto
   corresponden a la versión aprobada; no se descargan sus soluciones privadas.
3. Responder y registrar. Una coincidencia cerrada solo contrasta la regla. Para
   código, el texto queda NeedsReview: AulaTutor no lo ejecuta ni verifica sintaxis.
4. Dame una pista entrega la siguiente ayuda disponible; Otra explicación entrega
   una alternativa. No solicitan IA. Las ayudas anteriores se conservan en la sesión.
5. Si no se confirma el envío, Recuperar misma solicitud evita duplicar un intento
   o ayuda confirmado. No cambiar la respuesta mientras esa solicitud está incierta.
6. Pausar → recargar → continuar. Comprobar actividad, entregas y feedback guardados.
   Un texto aún no enviado no es evidencia y puede perderse al recargar.
7. Siguiente actividad respeta requisitos del servidor. No se salta una actividad
   cerrada obligatoria con un error. Finalizar exige entregar las obligatorias.
8. Consultar el resultado local aun cuando no haya cuota. Solicitar evaluación
   cualitativa final con IA es otra acción explícita, opcional y preliminar. En
   esta demo responde Mock. Recargar el reporte no regenera una evaluación.

La interfaz presenta dos áreas en escritorio y una columna con ayudas en móvil.
En 320 px puede requerir desplazamiento vertical; no deben ocultarse controles ni
aparecer desplazamiento horizontal de página. Se puede alternar Light/Dark.

## Clase conversacional

Abrir SQL conversacional → iniciar → Comenzar conversación. El inicio explícito,
respuestas y ayudas utilizan Mock; aquí sí hay invocaciones de proveedor simulado.
Pedir una pista, responder primero incorrectamente y después corregir. Solicitar
un ejercicio de refuerzo: no sustituye los obligatorios ni su identidad aprobada.
Pausar, recargar y reanudar. Generar reporte orientativo explícitamente y finalizar.
El reporte de cierre local no requiere IA y conserva evidencia pendiente humana.

Mock prueba el recorrido, no comprende de forma general tus dudas ni acredita
calidad de retroalimentación SQL. No introducir datos reales ni secretos.

## Resultados esperados del guion

| Perfil | Intentos | Errores cerrados | Pistas | Alternativas | Código pendiente |
|---|---:|---:|---:|---:|---:|
| A | 4 | 0 | 1 | 0 | 1 |
| B | 8 | 4 | 5 | 1 | 1 |
| C | 0 iniciales | 0 | 0 | 0 | Sin comenzar |

La evidencia de A/B es independiente aunque ambos ejecutan la misma versión.
El docente responsable puede supervisar respuestas, intentos y ayudas de esta
clase mientras la relación esté vigente. Las tutorías autónomas conservan su
privacidad; Teacher/Admin no adquieren acceso a su chat.
