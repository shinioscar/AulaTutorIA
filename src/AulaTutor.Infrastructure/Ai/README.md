# Proveedores IA

`IAiProvider` y los DTO están en Application. Infrastructure implementa Mock,
Groq, Gemini y Ollama por REST oficial, sin SDK. Selección/configuración al inicio,
sin fallback ni descubrimiento remoto; Mock sigue predeterminado y sin red.

Adaptadores singleton sin contexto de usuario; AiHttpTransport limita concurrencia
por proceso, timeout total, tamaño y reintentos. AiDiagnostics scoped verifica
Admin actual, usa prompt fijo y registra metadatos sin entrada/salida/secretos.
Salud y consulta de configuración nunca llaman a un proveedor. OpenAI reservado.

Contrato, catálogo por modelo, subconjunto JSON Schema, errores y fuentes:
docs/decisions/ADR-014-proveedores-ia.md. Variables/operación: README de raíz.
Tests de adaptadores usan HTTP controlado y credenciales ficticias; no cuotas reales.
El Mock conserva sus pruebas/limitaciones originales, incluido JSON no soportado.
