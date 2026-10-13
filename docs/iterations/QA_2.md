# Inventario de QA visual y funcional — Iteración 2

Entorno: backend Mock con PostgreSQL temporal aulatutor_http_GUID; navegador Edge
automatizado con Playwright del runtime disponible. Solo datos @example.invalid.
Sin instalar runner/paquetes en el proyecto y sin cuotas reales.

| Requisito / control | Comprobación funcional | Estado visual y evidencia |
|---|---|---|
| Diagnóstico solo Admin | Login ficticio, enlace y GET de configuración; Student no puede entrar | Configuración en escritorio 1440×1000 y móvil 390×844 |
| Configuración saneada | Mock/modelo, capacidades y límites; consulta sin generación | Pantalla inicial y botón de actualización |
| Prueba explícita | Botón, resultado Mock, duración y ausencia de uso inventado | Resultado desktop/mobile, sin secretos |
| Errores seguros | Respuesta de error SIMULADA mediante intercepción del navegador; backend se prueba en .NET | Mensaje y Retry-After visible; no afirmar llamada real |
| Contraseña mínima 5 | Registro ficticio con cinco minúsculas y login; con cuatro botón deshabilitado | Recomendación y formulario en desktop/móvil |
| Búsqueda por correo | Admin abre grupo, busca correo parcial de Student y Teacher, comprueba selector | Campo accesible y resultados en desktop/móvil |
| Búsqueda vacía / sin resultados | Limpiar muestra lista; correo inexistente avisa sin selección; Enter envía búsqueda | Aviso, controles y selección vacía |
| Paginación | Test PostgreSQL con más de 50 cuentas encuentra usuario fuera de primera página | Paginadores conservados junto al selector |
| Regresión | Grupos/permisos/cookies/CSRF/login/logout en suite y HTTP reales | Navegación tras cambios y cierre de sesión |

Exploración adicional: acceso directo Student a /admin/ai y ancho de 320 px.
Revisar captura y métricas de ajuste (sin scroll horizontal). La página puede
desplazarse verticalmente para alcanzar resultados/formularios, sin recortar
controles. No usar credenciales de QA en capturas ni imprimir el archivo privado.

Resultado final: todos los recorridos de la tabla pasaron; registro/login real de
cuenta ficticia con 5 minúsculas y rechazo visual con 4, búsqueda/asociación real de
Student B, búsqueda Teacher aprobado, vacío/sin resultados/Enter y guard Student.
Diagnóstico Mock real y error de cuota exclusivamente simulado. Sin errores JS,
sin scroll horizontal a 390/320 px; correo completo seleccionado visible debajo
del select nativo. Ocho capturas verificadas. Navegador cerrado; BD/estado QA
eliminados y entorno principal restaurado sin usuarios QA.
