# Proveedores IA

`IAiProvider` y los DTO se encuentran en Application; Infrastructure contiene el
único adaptador disponible, `MockAiProvider`. Su registro es singleton porque es
inmutable y no conserva estado de usuarios. La configuración se valida al iniciar:
cualquier proveedor distinto de `Mock` produce un error, sin fallback silencioso.

En Iteración 2 se formalizará el contrato y se añadirán Groq y puntos de extensión
para Gemini/Ollama/OpenAI, sin referencias a sus SDK desde Domain/Application.
No hay claves, clientes HTTP, llamadas externas, cuotas ni enrutamiento por caso
de uso en esta iteración. El Mock no declara soporte para JSON Schema.
