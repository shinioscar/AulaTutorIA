using AulaTutor.Application.Ai;
using AulaTutor.Application.Tutoring;
using AulaTutor.Application.Lessons;
using System.Text.Json;

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
        if (request.UseCase.StartsWith("lesson_", StringComparison.Ordinal) && request.ResponseContract.Format == "text")
        {
            using var context = JsonDocument.Parse(request.Messages.Last().Text);
            var draft = LearningJson.Read<LessonContent>(context.RootElement.GetProperty("draft").GetRawText());
            var content = draft with { Title = string.IsNullOrWhiteSpace(draft.Title) ? "Clase demostrativa Mock" : draft.Title,
                Subject = string.IsNullOrWhiteSpace(draft.Subject) ? "Práctica formativa" : draft.Subject,
                Level = string.IsNullOrWhiteSpace(draft.Level) ? "Básico" : draft.Level,
                DurationMinutes = draft.DurationMinutes == 0 ? 45 : draft.DurationMinutes,
                Methodology = "[Simulación Mock] Explicación breve, intento explícito y pistas graduadas; revisión docente.",
                Objectives = [new("o1", "Distinguir una respuesta puntual de evidencia suficiente")],
                Sections = [new("s1", "Introducción y práctica", "[Simulación Mock] Practicar y explicar el razonamiento aporta evidencia. Una sola respuesta no demuestra dominio. Este guion prueba el flujo y no comprende materias arbitrarias.",
                    [new("e1", "o1", true, new("[Simulación Mock] ¿Una respuesta correcta demuestra dominio completo del tema?", "TrueFalse", ["Verdadero", "Falso"], ["Falso"], "Distinguir respuesta puntual y dominio."), new(["Explica la diferencia entre un acierto y evidencia suficiente."], "Falso: se necesitan evidencias variadas.", "Invitar a comparar un caso con varios ejemplos antes de dar la solución."))]),
                    new("s2", "Reflexión", "[Simulación Mock] Describe qué necesitarías observar para revisar la comprensión.",
                    [new("e2", "o1", false, new("Propón otra evidencia para el mismo objetivo.", "OpenResponse", [], [], "Proponer un ejemplo pertinente."), new(["Pertinencia del ejemplo."], "", "Invitar a proponer un contexto diferente."))])] };
            return Task.FromResult(new AiChatResult(LearningJson.Write(LessonDesignPayload.FromProposal(new("[Simulación Mock] Propuesta de demostración; revisa el contenido antes de aplicarlo. No es planificación semántica de un LLM.", content))), ProviderId, ModelId, null, null, "stop"));
        }
        if (request.UseCase.StartsWith("learning_", StringComparison.Ordinal) && request.ResponseContract.Format == "text")
        {
            using var context = JsonDocument.Parse(request.Messages.Last().Text);
            var root = context.RootElement;
            var topic = root.GetProperty("topic").GetString()!;
            var goal = root.GetProperty("goal").GetString()!;
            var ids = root.GetProperty("evidence").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).Take(3).ToArray();
            var preliminary = root.GetProperty("preliminary").GetBoolean();
            var exercise = new ExerciseProposal("[Simulación Mock] ¿Una sola respuesta correcta demuestra dominio completo de un tema?", "TrueFalse", ["Verdadero", "Falso"], ["Falso"], "Distinguir una respuesta puntual de evidencia suficiente.");
            object payload = request.UseCase switch
            {
                "learning_diagnosticquestion" => new ExerciseProposal($"[Simulación Mock] Explica con un ejemplo qué conoces sobre {topic} y qué te gustaría practicar.", "OpenResponse", [], [], "Describe una idea y un ejemplo pertinente. La respuesta queda sin verificación objetiva."),
                "learning_analyzediagnosis" => new AssessmentContent(
                    preliminary ? "[Simulación Mock] Evaluación preliminar: faltan respuestas suficientes para identificar fortalezas o dificultades." : "[Simulación Mock] Hay respuestas registradas; el guion no evalúa su contenido ni demuestra dominio.",
                    [], [], ["Comprensión y transferencia a otros contextos no verificadas."], ["Practicar el objetivo declarado con ejemplos y revisión."]),
                "learning_generateplan" => LearningPlanPayload.FromPlan(new PlanContent(goal, "[Simulación Mock] Ruta demostrativa de repaso, aplicación y reflexión; no sustituye una planificación real de un LLM.",
                    Enumerable.Range(0, 3).Select(i => new LearningPlanObjective(new[] { "Relacionar conceptos y ejemplos", "Aplicar el objetivo a un caso", "Revisar y explicar lo aprendido" }[i], ids,
                        [new LearningPlanActivity($"[Simulación Mock] Trabajaremos {topic} mediante una explicación breve y una actividad. Este guion ilustra el flujo, sin comprensión semántica.", i == 2 ? "Intermediate" : "Beginner", 5, exercise)])).ToArray())),
                "learning_generateexercise" => exercise,
                "learning_finalreport" => new ReportContent("[Simulación Mock] Los resultados objetivos se muestran desde los intentos almacenados. El guion no infiere aprendizaje.",
                    ids.Length == 0 ? [] : [new EvidenceObservation("Se registraron respuestas en ejercicios explícitos; revisar sus resultados y ayudas. Interpretación simulada, sin certificar dominio.", ids)],
                    ["Transferencia y dominio de la asignatura no evaluados."], [new RecommendationProposal("Practicar " + topic[..Math.Min(topic.Length, 140)] + " con un ejemplo nuevo", preliminary ? [] : ids)]),
                "learning_activateplan" or "learning_nextactivity" => new LearningOrchestrator.FeedbackContent(root.GetProperty("exercise").ValueKind == JsonValueKind.Null
                    ? "[Simulación Mock] Ruta trabajada. Puedes continuar preguntando libremente o finalizar para consultar tus resultados. No se certifica dominio."
                    : "[Simulación Mock] Comenzamos con una explicación breve: practicar y explicar tu razonamiento aporta evidencia, sin demostrar por sí solo dominio.\n\n" + root.GetProperty("exercise").GetProperty("prompt").GetString() + "\n\nResponde en la tarjeta; tus preguntas libres conservan esta actividad."),
                _ => new LearningOrchestrator.FeedbackContent("[Simulación Mock] Respuesta registrada. Consulta el resultado del validador en la tarjeta. La orientación simulada no cambia el resultado objetivo ni ejecuta código.")
            };
            return Task.FromResult(new AiChatResult(LearningJson.Write(payload), ProviderId, ModelId, null, null, "stop"));
        }
        if (request.UseCase.StartsWith("tutoring_", StringComparison.Ordinal) && request.ResponseContract.Format == "text")
        {
            if (request.Messages.Last().Text.StartsWith("{", StringComparison.Ordinal))
            {
                using var guidance = JsonDocument.Parse(request.Messages.Last().Text);
                if (guidance.RootElement.TryGetProperty("simulation", out var flag) && flag.ValueKind == JsonValueKind.True)
                {
                    var g = guidance.RootElement;
                    var simulated = g.GetProperty("action").GetString() switch
                    {
                        "Start" or "Next" => "[Simulación docente Mock] " + g.GetProperty("sectionContent").GetString() + "\n\n" + (g.GetProperty("exercise").ValueKind == JsonValueKind.Null ? "Recorrido trabajado; puedes detenerlo." : g.GetProperty("exercise").GetProperty("prompt").GetString()),
                        "Hint" => "[Simulación docente Mock] Pista: " + g.GetProperty("hintRules").GetString(),
                        "Simplify" => "[Simulación docente Mock] Retoma una idea a la vez y explica tu razonamiento. Se conserva la actividad actual.",
                        "Answer" => "[Simulación docente Mock] Respuesta registrada. Resultado del comparador: " + g.GetProperty("outcome").GetString() + ". La orientación no cambia ese resultado ni certifica dominio.",
                        _ => "[Simulación docente Mock] Duda registrada. Volvemos al contenido y a la actividad seleccionada; este guion no interpreta semánticamente tu mensaje."
                    };
                    return Task.FromResult(new AiChatResult(simulated, ProviderId, ModelId, null, null, "stop"));
                }
            }
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
