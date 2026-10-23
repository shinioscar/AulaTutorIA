# Requisitos antes de usuarios reales

Estado de decisión: **bloqueado para datos/personas reales**. Iteración 9 autoriza
solo piloto interno ficticio. No se cambió la política de contraseñas ni se
implementaron por inferencia requisitos institucionales nuevos.

## Bloqueantes

| Prioridad | Requisito | Estado/evidencia actual | Evidencia necesaria para habilitar reales |
|---|---|---|---|
| P0 | Contraseñas robustas | Política 5–128 conservada por autorización previa; insuficiente para reales | Nueva autorización, política aprobada, implementación y regresión |
| P0 | Confirmación de correo | No implementada | Flujo anti-enumeración probado, proveedor y operación aprobados |
| P0 | Recuperación de cuentas | No implementada | Recuperación segura, tokens/expiración/revocación y soporte |
| P0 | MFA administrativo | No implementado | Enrolamiento, recuperación y revocación verificados |
| P0 | Datos y consentimiento | Solo datos ficticios; políticas reales no aprobadas | Responsable institucional, información/consentimiento y tratamiento con proveedores |
| P0 | Retención y eliminación | Sin política institucional ni borrado/exportación completos | Plazos, solicitudes de derechos, proceso auditable compatible con historial/triggers |
| P0 | TLS de despliegue | HTTPS local y proxy de mismo origen probados | Certificados/renovación, proxy/cabeceras y prueba del entorno de destino |
| P0 | Secretos y Data Protection | .env privado, DPAPI Windows; producción Linux pendiente | Custodia/rotación, mínimo acceso y cifrado/recuperación de claves en destino |
| P0 | Permisos PostgreSQL | QA requiere CREATE DATABASE y usuario de mantenimiento | Roles de aplicación/migración separados y mínimo privilegio verificado |
| P0 | Respaldos durables | Ensayo previo aislado, no política durable | Copia cifrada fuera del volumen, RPO/RTO, retención y restauraciones periódicas |
| P0 | Revisión curricular e institucional | Referencia SQL y rúbrica preparadas; sin aprobación curricular independiente | Revisión humana real, corrección de defectos y autorizaciones institucionales aplicables |
| P0 | Proveedor real | Contratos/cuotas y fallos simulados; sin prueba real en 9 | Autorización específica, condiciones vigentes del proveedor, minimización y revisión pedagógica |

Responsables y fechas deben asignarse por la institución/operador; no se inventan
aprobaciones ni plazos. Esta es una matriz técnica de pendientes, no un dictamen
legal, de cumplimiento normativo o certificación de seguridad.

## Mejoras posteriores que no bloquean este ensayo ficticio

| Prioridad | Mejora | Límite actual |
|---|---|---|
| P1 | Navegación global y acceso al recorrido | El flujo completo funciona; hay pasos y rótulos heredados mejorables |
| P1 | Visibilidad móvil de ayudas/evidencias | Puede requerir desplazamiento; evidencia persistida, sin overflow de página |
| P1 | Remediación de intentos agotados | No hay aprobación/reset automático; requiere política académica explícita |
| P2 | Optimizar contenido precargado | Límite de generación breve, introducción/contenido pueden repetirse |
| P2 | Diagnósticos precargados y modo híbrido | Fuera de alcance; no reemplazar el tutor actual |
| P2 | Nuevos proveedores/exportaciones | No necesarios para este MVP, sin autorizaciones nuevas |
| P2 | Analítica avanzada y grupos grandes | Métricas pequeñas no certifican capacidad ni eficacia educativa |

Fuentes: ADR-012/013/015/017/019/025/028, [OPERATIONS](../architecture/OPERATIONS.md)
y el encargo de 9. Ningún resultado Mock elimina estos bloqueantes.
