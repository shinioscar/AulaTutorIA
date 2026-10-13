# Iteración 3 — Tutorías privadas y enseñanza interactiva inicial

Fecha: 2026-10-13. Alcance autorizado: exclusivamente Iteración 3 del nuevo encargo.
Se leyeron completos el maestro, las convenciones y los antecedentes. Maestro
intacto, SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05.
No se implementaron clases docentes, evaluaciones oficiales, compartición ni SQL
ejecutable. No se instalaron paquetes nuevos ni se modificaron otros proyectos.

## Funcionalidades implementadas

- Student crea tutoría privada con tema, objetivo, nivel percibido y diagnóstico
  breve o repaso directo. Crear y consultar no llama al proveedor.
- Conversación de múltiples turnos, explicación, ejercicio y observaciones
  orientativas; acciones de pista y simplificación, una solicitud por acción.
- Historial durable, listado propio paginado, reapertura tras recarga/logout,
  pausa/reanudación y cierre terminal con fecha/resumen descriptivo local.
- Autenticación/CSRF y autorización de propiedad en cada recurso. Otro estudiante,
  Teacher asignado al mismo grupo y Admin ordinario no pueden leer ni continuar.
- Orquestador en Application con política tutoring-v1, contexto reciente acotado,
  metadatos separados de instrucciones del sistema y JSON Schema cuando el
  proveedor lo soporta. Se reutilizan los adaptadores de Iteración 2.
- Persistencia de sesión/mensajes/turnos mediante migración aditiva PrivateTutoring,
  claves idempotentes, revisión, locks breves y lease que cerca respuestas tardías.
  Intento guardado antes de IA; recuperación explícita sin mensajes duplicados.
- Angular Student responsivo, estados vacíos/carga/error/generación, recuperación
  y cancelación. Código/listas se muestran por interpolación segura sin HTML
  arbitrario, ejecución ni almacenamiento del historial en el navegador.

Se mantienen identidad, grupos, diagnóstico administrativo, Mock y comprobaciones
anteriores. No se cambió la política de contraseñas autorizada de 5–128 caracteres.
Ese mínimo sigue siendo un riesgo pendiente y no es apropiado para usuarios reales.

## Decisiones y archivos

ADR-016 documenta privacidad, política, contexto, representación y conservación;
ADR-017 documenta ciclo, idempotencia, concurrencia, cancelación y límites.
Contratos en `../architecture/TUTORING_HTTP.md`, inventario en `FILES_3.md` y QA
en `QA_3.md`. README, AGENTS, plan, índice y preguntas abiertas quedan actualizados.
No hay transacción PostgreSQL abierta durante una llamada prolongada a IA.

Resumen de cierre determinista y descriptivo, sin llamada adicional: no certifica
aprendizaje ni es el módulo de evidencias/reportes de Iteración 4. Ciclo operativo
Created/Active/Paused/Completed separado de actividad pedagógica orientativa.
Proveedor/modelo fijados al crear; cambio de configuración causa 409, sin fallback.
Retención provisional: datos ficticios locales hasta limpieza explícita, pendiente
de políticas institucionales para datos reales. Las ambigüedades quedan registradas
sin modificar requisitos originales.

## Verificaciones realizadas

| Comprobación | Resultado |
|---|---|
| Verify.ps1 -Smoke -Http | Completo, satisfactorio |
| Restore NuGet locked y compilación Release | Sin errores ni advertencias |
| Pruebas .NET | 91 aprobadas: 55 unitarias y 36 integración, ninguna omitida |
| Conservación de pruebas | Las 68 anteriores siguen pasando; 23 nuevas de tutoría |
| PostgreSQL real y migraciones | Bases aisladas; migración reproducible, repetición idempotente, modelo sin cambios pendientes |
| HTTP real HTTPS/PostgreSQL | 48 comprobaciones de identidad/grupos/IA Mock/tutoría, aprobadas |
| TypeScript y Angular | npm ci, check y build aprobados; repetidos tras los ajustes visuales finales |
| Dependencias | npm audit: 0 vulnerabilidades; NuGet transitive: ninguna reportada |
| Compose, salud y proxy HTTPS | Configuración y smoke aprobados; PostgreSQL activo |
| Navegador Edge/Playwright existente | Flujo completo y revisión escritorio/móvil; sin errores JavaScript |
| Secretos, documento maestro y Git | Ningún secreto configurado en código/artefactos; privados ignorados, hash original y diff --check correctos |

Cobertura nueva: propiedad/listado, permisos entre roles, CSRF/anónimos, límites,
persistencia/reapertura, estados/resumen, contexto/política/esquema, errores de cuota,
deadline y cancelación, reintentos limitados, duplicados simultáneos, revisión vieja,
lease vencido, respuestas tardías, cambio de modelo y revocación durante generación.
No se sustituyó PostgreSQL por InMemory/EnsureCreated ni se omitieron pruebas.
Las expectativas heredadas sobre una sola migración y ausencia de rutas de tutoría
se actualizaron a dos migraciones y ausencia de clases futuras; no se retiraron tests.

En navegador se recorrió creación → diagnóstico → intento/ejercicio → pista →
simplificación → pausa → recarga/logout/login → reapertura/reanudación → cierre.
También repaso directo y nivel Intermediate, respuesta HTTP perdida después de
persistir, GET retrasado frente a respuesta nueva, HTML literal no ejecutado y
denegación visual/backend a estudiantes ajenos, Teacher y Admin. Pantallas
1440×1000, 390×844 y ancho 320 sin desbordamiento horizontal. Capturas en evidence-3.
Los estados visuales Pending/Timeout se prepararon exclusivamente en BD de QA
aislada; sus botones llamaron al backend real y Mock. Los fallos/cancelación/timeout
del servicio también están cubiertos por pruebas automatizadas con proveedor
controlado. No se presentan esas capturas como fallos de Groq real.

## Proveedores reales frente a Mock

Todo CI, HTTP y navegador de esta iteración usó Mock o transporte/proveedor
controlado; no realizó llamadas adicionales a Groq, Gemini ni Ollama. Las claves
externas se retiran del proceso de pruebas y .env no se modifica. Mock es un guion
SQL rotulado, no un LLM ni corrector de respuestas arbitrarias.

El usuario informó antes del encargo un diagnóstico real exitoso: Groq,
openai/gpt-oss-20b, 555 ms, 97 tokens de entrada y 77 de salida. Es evidencia
aportada por el usuario, no una llamada del agente ni una validación pedagógica.
Se conserva Groq activo localmente. La calidad pedagógica y los nuevos contratos
de tutoría con proveedores reales requieren prueba manual autorizada posterior.

## Problemas encontrados y límites

La API anterior tenía permisos elevados y Windows rechazó detenerla. El usuario
la cerró con Ctrl+C; se continuó con bases y procesos del proyecto exclusivamente.
La revisión visual detectó historial demasiado alto: se acotó y se priorizó la
conversación en móvil. Se corrigieron respuestas de lectura antiguas que podían
reemplazar revisiones nuevas y la indicación de carga después de denegación.
Las primeras aserciones fallidas de las pruebas nuevas se corrigieron para validar
actividad y JSON decodificado; el resultado final completo es satisfactorio.

No hay streaming, borrado/exportación de tutorías, acceso excepcional Admin,
validación académica objetiva ni renderizado TeX avanzado. La ventana corta puede
perder contexto antiguo; el historial persiste. Cancelación y reintento no garantizan
facturación externa exactly-once. Límites por proceso no son cuotas distribuidas ni
presupuesto institucional. Los logs solo contienen metadatos operativos.

## Preparación para Iteración 4

Antes de implementarla, aprobar alcance de evidencias/validadores y reglas explícitas
de compartición con consentimiento. Mantener tutorías privadas y separar observación
IA de evidencia validada. Definir retención/eliminación y uso de proveedores para
datos reales; revisar la política de contraseñas por nueva autorización. Una prueba
pedagógica real breve solo puede ejecutarse con autorización explícita del usuario.
No se avanzó automáticamente a Iteración 4.

## Entorno entregado

PrivateTutoring aplicada explícitamente en PostgreSQL local, sin semillas de usuarios
ni cambios a hashes/grupos existentes. La API queda activa en HTTPS 7080/HTTP 5080,
Angular en HTTPS 4200 y Docker PostgreSQL encendido. Salud consultada mediante el
proxy devuelve Groq/openai/gpt-oss-20b; es lectura de configuración sin consumo de IA.
La base temporal HTTP/visual, su proceso Mock y el archivo privado de QA se eliminaron.
