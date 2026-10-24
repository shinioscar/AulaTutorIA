using System.Text.Json;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public sealed record PreloadedExercise(string ExerciseId, string[] Hints, string[] Alternatives,
    string CorrectFeedback, string IncorrectFeedback, string ReviewFeedback, string[] CommonErrors, string[] ReinforcementSuggestions);
public sealed record PreloadedContent(string Introduction, PreloadedExercise[] Exercises);
public sealed record LessonImportV2(string Format, LessonDesignPayload Lesson, PreloadedContent Preloaded);

public static class PreloadedRules
{
    public const string Format = "aulatutor-lesson-v2";
    public static string Schema => LearningJson.Write(LearningOrchestrator.SchemaFor(typeof(LessonImportV2)));
    public static LessonContent Convert(LessonImportV2 input)
    {
        if (input.Format != Format) throw new JsonException();
        if (string.IsNullOrWhiteSpace(input.Lesson.Explanation) || input.Lesson.Explanation.Length > 1000) throw new JsonException();
        var content = input.Lesson.ToProposal().Content with { Preloaded = input.Preloaded };
        LessonValidationRules.ValidateDraft(content);
        var review = LessonValidationRules.Review(content);
        if (review.Errors.Length > 0) throw new AppException(400, string.Join(" ", review.Errors));
        return content;
    }
    public static void Validate(LessonContent c, bool complete = false)
    {
        var p = c.Preloaded; if (p is null) return;
        static void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string message)
        { if (!ok) throw new AppException(400, message); }
        void Text(string? s, string field)
        {
            Check(s is not null && (!complete || !string.IsNullOrWhiteSpace(s)), $"{field}: incluye un texto terminado y no vacío.");
            Check(s.Length <= 600, $"{field}: tiene {s.Length} caracteres; máximo 600.");
            Check(!s.Any(ch => char.IsControl(ch) && ch is not ('\n' or '\r' or '\t')), $"{field}: elimina los caracteres de control no permitidos.");
        }
        void Texts(string[]? items, string field, bool required)
        {
            Check(items is not null && items.Length <= 3 && (!required || !complete || items.Length > 0), $"{field}: usa {(required && complete ? "1–3" : "0–3")} textos, cada uno de hasta 600 caracteres.");
            for (var n = 0; n < items.Length; n++) Text(items[n], $"{field}[{n}]");
        }
        Text(p.Introduction, "preloaded.introduction");
        Check(p.Exercises is not null && p.Exercises.Length <= 24, "preloaded.exercises: usa un array con máximo 24 ayudas de ejercicios.");
        var exercises = c.Sections.SelectMany(s => s.Exercises).ToArray();
        Check(p.Exercises.Length == exercises.Length, $"preloaded.exercises: debe contener una entrada por ejercicio; hay {p.Exercises.Length} entradas para {exercises.Length} ejercicios.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < p.Exercises.Length; i++)
        {
            var aid = p.Exercises[i]; var field = $"preloaded.exercises[{i}]";
            Check(aid is not null, $"{field}: incluye el objeto completo de ayudas.");
            Check(ids.Add(aid.ExerciseId), $"{field}.exerciseId: referencia repetida; incluye cada ejercicio exactamente una vez.");
            var e = exercises.SingleOrDefault(e => e.Id == aid.ExerciseId);
            Check(e is not null, $"{field}.exerciseId: debe coincidir con un id existente en lesson.exercises.");
            Check(!complete || !string.IsNullOrWhiteSpace(e.Rubric.ReferenceSolution), $"{field}: el ejercicio asociado necesita referenceSolution privada.");
            Texts(aid.Hints, field + ".hints", true); Texts(aid.Alternatives, field + ".alternatives", true);
            Text(aid.CorrectFeedback, field + ".correctFeedback"); Text(aid.IncorrectFeedback, field + ".incorrectFeedback"); Text(aid.ReviewFeedback, field + ".reviewFeedback");
            Texts(aid.CommonErrors, field + ".commonErrors", false); Texts(aid.ReinforcementSuggestions, field + ".reinforcementSuggestions", false);
            // Semantic disclosure still requires teacher review; reject obvious direct first-hint keys.
            Check(aid.Hints.Length == 0 || !e!.Activity.AcceptedAnswers.Any(a =>
                string.Equals(a.Trim(), aid.Hints[0].Trim(), StringComparison.OrdinalIgnoreCase) ||
                a.Trim().Length >= 3 && aid.Hints[0].Contains(a.Trim(), StringComparison.OrdinalIgnoreCase)),
                $"{field}.hints[0]: la primera pista contiene una respuesta privada literal. Reescríbela para orientar sin revelar la clave.");
        }
    }
    public static PreloadedExercise Aid(LessonContent c, string exerciseId) => c.Preloaded!.Exercises.Single(a => a.ExerciseId == exerciseId);
    public static string Help(string[] items, int index)
    { if (index >= items.Length) throw new AppException(409, "Ya consultaste todas las ayudas de este tipo para la actividad. Puedes revisar las anteriores."); return items[index]; }
    public static string Feedback(PreloadedExercise aid, string outcome) => outcome switch {
        "CorrectWithHelp" or "CorrectWithoutHelp" => aid.CorrectFeedback,
        "Incorrect" => aid.IncorrectFeedback + "\n" + string.Join("\n", aid.CommonErrors.Concat(aid.ReinforcementSuggestions)),
        _ => aid.ReviewFeedback + "\nRespuesta registrada; requiere revisión humana. No se ejecuta código ni SQL." };
}
