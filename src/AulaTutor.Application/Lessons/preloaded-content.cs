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
        static void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok) { if (!ok) throw new AppException(400, "Completa la introducción, pistas progresivas, explicaciones y retroalimentación precargada de cada ejercicio. Máximo tres ayudas de cada tipo, 600 caracteres por ayuda y 20000 en la clase."); }
        bool Text(string? s) => s is not null && (!complete || !string.IsNullOrWhiteSpace(s)) && s.Length <= 600 && !s.Any(ch => char.IsControl(ch) && ch is not ('\n' or '\r' or '\t'));
        Check(Text(p.Introduction) && p.Exercises is not null && p.Exercises.Length <= 24);
        var exercises = c.Sections.SelectMany(s => s.Exercises).ToArray();
        Check(p.Exercises!.All(a => a is not null) && p.Exercises.Length == exercises.Length && p.Exercises.Select(a => a.ExerciseId).Distinct().Count() == p.Exercises.Length);
        foreach (var aid in p.Exercises)
        {
            Check(aid is not null);
            var e = exercises.SingleOrDefault(e => e.Id == aid!.ExerciseId);
            Check(!complete || e is not null && !string.IsNullOrWhiteSpace(e.Rubric.ReferenceSolution));
            Check(e is not null && aid!.Hints is {Length: <= 3} && aid.Alternatives is {Length: <= 3} && (!complete || aid.Hints.Length > 0 && aid.Alternatives.Length > 0) &&
                aid.Hints.All(Text) && aid.Alternatives.All(Text) && Text(aid.CorrectFeedback) && Text(aid.IncorrectFeedback) && Text(aid.ReviewFeedback) &&
                aid.CommonErrors is {Length: <= 3} && aid.CommonErrors.All(Text) && aid.ReinforcementSuggestions is {Length: <= 3} && aid.ReinforcementSuggestions.All(Text));
            // Semantic disclosure still requires teacher review; reject obvious direct first-hint keys.
            Check(aid.Hints.Length == 0 || !e!.Activity.AcceptedAnswers.Any(a =>
                string.Equals(a.Trim(), aid.Hints[0].Trim(), StringComparison.OrdinalIgnoreCase) ||
                a.Trim().Length >= 3 && aid.Hints[0].Contains(a.Trim(), StringComparison.OrdinalIgnoreCase)));
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
