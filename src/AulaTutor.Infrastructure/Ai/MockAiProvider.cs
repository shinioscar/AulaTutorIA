using AulaTutor.Application.Ai;

namespace AulaTutor.Infrastructure.Ai;

// Foundation response plus labeled tutoring scripts; no network or actual assessment.
public sealed class MockAiProvider : IAiProvider
{
    public string ProviderId => "Mock";
    public const string ModelId = "mock-foundation-v1";
    public AiCapabilities Capabilities => new(false, false, false, false);

    public Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.UseCase.StartsWith("tutoring_", StringComparison.Ordinal) && request.ResponseContract.Format == "text")
        {
            var count = request.Messages.Count(m => m.Role == "assistant");
            var text = request.UseCase switch
            {
                "tutoring_start" => "[Simulación Mock, sin comprensión de un LLM]\n\nAntes de practicar, cuenta qué entiendes por comprobar si existe un dato. ¿En qué caso usarías esa comprobación?",
                "tutoring_review" => "[Simulación Mock]\n\nRepaso directo: comprobar existencia responde si hay al menos un dato que cumpla una condición. Empezamos con una explicación breve. ¿Qué condición usarías para buscar a una persona concreta?",
                "tutoring_hint" => "[Simulación Mock]\n\nPista: piensa primero en la condición que quieres comprobar, antes de decidir qué acción realizar. ¿Qué dato necesitarías consultar?",
                "tutoring_simplify" => "[Simulación Mock]\n\nImagina una lista de invitados: comprobar si alguien está en ella es distinto de contar todos los invitados. ¿Qué pregunta harías para saber si Ana está en la lista?",
                _ when count < 2 => "[Simulación Mock; retroalimentación orientativa]\n\n`EXISTS` comprueba si una consulta devuelve al menos una fila. No ejecutamos SQL aquí.\n\n```sql\nIF EXISTS (SELECT 1 FROM Personas WHERE Id = 7)\n    PRINT 'Encontrado';\n```\n\n- Identifica la condición.\n- Explica qué ocurriría si no hay filas.\n\nEjercicio: ¿qué verifica el `WHERE` en este ejemplo?",
                _ => "[Simulación Mock; no valida la corrección de tu intento]\n\nRevisa cómo relacionaste la condición con la consulta. Si hay una dificultad, vuelve al dato buscado y prueba una condición más simple.\n\nEjercicio siguiente: describe cómo comprobarías la existencia de dos valores distintos, sin revelar todavía una solución completa."
            };
            return Task.FromResult(new AiChatResult(text, ProviderId, ModelId, null, null, "stop"));
        }
        if (request.ResponseContract.Format != "text")
        {
            throw new NotSupportedException("The foundation Mock supports text only.");
        }

        return Task.FromResult(new AiChatResult(
            "Proveedor simulado de la Iteración 0. No genera tutorías ni evaluaciones.",
            ProviderId, ModelId, null, null, "stop"));
    }

    public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new AiHealthResult(true, ProviderId, ModelId));
    }
}
