# UI-1 — Experiencia docente y creación simplificada

Fecha: 2026-10-22/23. Estado: **completada y verificada**.
Base f66ada5. Encargo explícito UI-1 y repositorio indicado:
https://github.com/shinioscar/AulaTutorIA.git. No autoriza IA-1 ni Iteración 9.

## Resultado

- Mis clases: biblioteca con tarjetas, estados y fecha del backend, búsqueda por
  título y filtro de estado de la página actual, paginación y vacío/error recuperables.
  Sin cifras globales inventadas ni duplicar supervisión.
- Dos tarjetas con SVG locales abren rutas Teacher independientes:
  /teacher/lessons/new/manual y /teacher/lessons/new/ai.
- Manual: título, asignatura y descripción inicialmente; más opciones plegadas.
  Guardado incompleto y reanudación en el constructor existente.
- IA: tema, nivel, cantidad aproximada e indicaciones; dificultad, duración,
  objetivos, tipos, pistas y reglas plegados. Generar guarda primero el borrador
  con las indicaciones y solicita Plan mediante LessonService existente.
  Un fallo de confirmación abre el recurso conservado y permite revisar la solicitud;
  no se reintenta, aplica, aprueba o publica automáticamente.
- Editor único progresivo: información básica, contenido y revisión/publicación.
  Los pasos pueden recorrerse libremente conservando cambios en memoria. Objetivos,
  secciones, cinco tipos de actividad y soluciones privadas se conservan; rúbrica,
  solución y pistas se despliegan por actividad. Conversación avanzada accesible
  desde Ajustes con inteligencia artificial.
- Sin cambios a backend, DTO, migraciones, proveedores, permisos, contraseñas ni
  motor de tutoría. ADR-026 registra la decisión. Sin paquetes nuevos, colores
  literales ni almacenamiento académico en navegador/URL.

## Verificación ejecutada

Verify.ps1 -Smoke -Http aprobó 290 pruebas (116 unitarias/174 integración), cero
omitidas, conservando las 290 anteriores. Restore locked, Release, consistencia EF
(siete migraciones, modelo sin cambios), npm ci/TypeScript/Angular, Compose, diff y
smoke HTTPS/API/web/proxy aprobados. npm ci reportó cero vulnerabilidades conocidas.
Los 70 controles de contraste/tokens aprobaron. Log: artifacts/ui-1-verify.log.

Test-E2E.ps1, Edge/Playwright ya instalados: 25 comprobaciones agrupadas (diez
funcionales y quince capturas/mediciones), cero excepciones JavaScript y overflow
horizontal global observado cero en 320/390/1440. Backend Mock y PostgreSQL aislado.
La preparación del pase final repitió las 191 HTTP. Log: ui-1-e2e-suite.log;
resultado: ui-1-e2e.json, ambos privados en artifacts.

| Recorrido | Comprobación ejecutada |
|---|---|
| Profesor nuevo | Entrada manual independiente, borrador incompleto, edición/recarga, objetivo/sección/actividad de respuesta corta, respuesta privada y pista guardadas |
| Profesor con IA | Entrada guiada, 429 deliberadamente simulado conserva indicaciones; generación Mock real del adaptador, descartar/aplicar, editar antes de aprobar, simular enseñanza/detener, aprobar/publicar explícitos |
| Profesor recurrente | Búsqueda, filtro sin coincidencias y limpieza; reabrir clase existente con dos versiones y comparar versiones completas antes/después sin cambios |
| Compatibilidad | Asignación de versión exacta, dos docentes sin accesos cruzados, Student conversación/ayuda/pausa/reanudación/ejercicios/cierre, revisión y feedback propio |
| Errores/privacidad | 429 conserva texto, respuesta perdida tras commit recupera un solo turno, aislamiento de tutoría autónoma y endpoints administrativos |
| Accesibilidad/temas | Ciclo Light/Dark reversible, foco por Tab, reduced-motion, storage limitado al tema y sessionStorage vacío; inspección visual de las quince PNG |

El recorrido completo de diagnóstico/plan/reporte sigue verificado por HTTP;
el navegador verifica historial/privacidad. No se atribuyen todos esos pasos a GUI.
Mock valida flujos, no calidad curricular, cumplimiento semántico del número de
actividades ni rendimiento de proveedores reales. No es certificación WCAG.

## Incidencias del harness

El primer pase se detuvo en una aserción inmediata del conteo de tarjetas durante
el render de la búsqueda (0 frente a 1). La captura posterior mostró la coincidencia
correcta. Se añadieron esperas por enlace/estado del DOM y se mantuvo la aserción.
También se esperó título/carga de ruta antes de capturar: la primera captura Light
había sido prematura. Se repitió todo el runner satisfactoriamente, sin cambiar
el producto ni eliminar comprobaciones. Log anterior: ui-1-e2e-render-wait.log.

## Evidencias visuales

Quince PNG ficticias en evidence-ui-1. Se revisaron legibilidad, jerarquía, estados,
controles y ausencia de solapamientos/recortes horizontales. Scroll vertical esperado.

- [Biblioteca Light](evidence-ui-1/library-light.png)
- [Biblioteca Dark 320](evidence-ui-1/library-dark-320.png)
- [Manual Light 390](evidence-ui-1/manual-light-390.png)
- [Contenido manual Dark](evidence-ui-1/manual-content-dark.png)
- [Entrada IA Light](evidence-ui-1/ai-light.png)
- [Entrada IA Dark 390](evidence-ui-1/ai-dark-390.png)
- [Recuperación IA Dark 390](evidence-ui-1/ai-recovery-dark-390.png)
- [Versiones anteriores intactas](evidence-ui-1/existing-versions-light.png)
- [Publicación Light](evidence-ui-1/teacher-published-light.png)
- [429 Student Dark](evidence-ui-1/student-quota-dark-mobile.png)
- [Texto conservado Student](evidence-ui-1/student-quota-input-dark-mobile.png)
- [Cierre Student](evidence-ui-1/student-completed-dark-mobile.png)
- [Revisión Teacher Light](evidence-ui-1/teacher-review-light.png)
- [Revisión Teacher Dark 320](evidence-ui-1/teacher-review-dark-320.png)
- [Feedback Student Light](evidence-ui-1/student-feedback-light-mobile.png)

## Límites y continuidad

La búsqueda se limita a la página cargada (hasta 50); se indica en la interfaz.
La navegación global se conserva y ocupa varias líneas en móvil; decidir sidebar
u otra navegación queda fuera de UI-1. No hay drag-and-drop ni importación JSON.
Los valores de la guía IA se envían como indicaciones, no como garantía curricular.
No se ejecutaron proveedores reales ni estudio con docentes/estudiantes reales.
Los riesgos previos al piloto documentados en ITERATION_8 siguen vigentes.

## Publicación segura y entorno

.gitignore conserva .env/.env.* (excepto .env.example), certificados, caches/logs y
artefactos; añade respaldos y material privado de firma/PostgreSQL.
scripts/check-publish-safety.mjs revisa exclusión de .env, rutas/contenido actuales
versionados y objetos de texto del historial completo contra secretos configurados,
formatos de claves reconocibles y claves privadas, sin imprimir coincidencias.
Las evidencias binarias se revisan visualmente por separado. No es una garantía de
detección universal de secretos futuros; repetir antes de publicar nuevos cambios.

Cero llamadas reales de IA, sin cambios a .env/claves/datos principales; maestro
SHA256 FC76FD6C7A1882FFD2F34D472CF1B55B469405A403D67D04E21BF4636C72AF05 intacto.
Reinicio real aprobado: cookie, historial y replay conservados sin duplicados.
API y Angular locales restaurados con Groq, comprobado mediante salud sin generación;
PostgreSQL accesible con siete migraciones y cero bases QA. .cache/http-qa.json
eliminado y estado privado de procesos actualizado. Resultado: artifacts/ui-1-restored.json.
Chequeo de publicación ejecutado sin coincidencias de secretos; .env fuera del índice.
Commit de cierre local en main y remoto origin configurado al repositorio solicitado.
El resultado del envío se verifica después del commit; no se usan push forzados.
