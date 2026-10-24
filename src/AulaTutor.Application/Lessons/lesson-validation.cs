using System.Text.Json;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public static class LessonValidationRules
{
    // Messages contain only server-owned field names, positions and counts, never submitted values.
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new AppException(400, message); }
    private static void Text(string? value, int max, string field)
    {
        Require(value is not null, $"{field}: debe ser texto, no null.");
        Require(value.Length <= max, $"{field}: tiene {value.Length} caracteres; máximo {max}. Reduce únicamente este texto.");
        Require(!value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')), $"{field}: elimina los caracteres de control no permitidos.");
    }
    private static bool Id(string? id) => id is not null && id.Length is >= 1 and <= 40 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static void ValidateDraft(LessonContent p)
    {
        Require(p is not null, "Falta el contenido de la clase.");
        Text(p.Title, 160, "lesson.title"); Text(p.Subject, 160, "lesson.subject"); Text(p.Description, 1000, "lesson.description");
        Text(p.Prerequisites, 1000, "lesson.prerequisites"); Text(p.Level, 100, "lesson.level");
        Text(p.Methodology, 1500, "lesson.methodology"); Text(p.Instructions, 1500, "lesson.instructions");
        Require(p.DurationMinutes is >= 0 and <= 480, "lesson.durationMinutes: usa un entero entre 0 y 480; para revisar la clase debe ser mayor que 0.");
        Require(p.Objectives is not null && p.Objectives.Length <= 12, "lesson.objectives: usa un array con máximo 12 objetivos.");
        Require(p.Sections is not null && p.Sections.Length <= 12, "lesson.sections: usa un array con máximo 12 secciones.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < p.Objectives.Length; i++)
        {
            var o = p.Objectives[i];
            Require(o is not null && Id(o.Id), $"lesson.objectives[{i}].id: usa 1–40 caracteres ASCII alfanuméricos, guion o underscore.");
            Require(ids.Add(o.Id), $"lesson.objectives[{i}].id: identificador repetido; todos los IDs de objetivos, secciones y ejercicios deben ser únicos.");
            Text(o.Text, 500, $"lesson.objectives[{i}].text");
        }
        var objectives = p.Objectives.Select(x => x.Id).ToHashSet();
        var exercises = 0;
        for (var i = 0; i < p.Sections.Length; i++)
        {
            var s = p.Sections[i]; var section = $"lesson.sections[{i}]";
            Require(s is not null && Id(s.Id), $"{section}.id: usa 1–40 caracteres ASCII alfanuméricos, guion o underscore.");
            Require(ids.Add(s.Id), $"{section}.id: identificador repetido; no reutilices IDs de objetivos, secciones o ejercicios.");
            Text(s.Title, 160, section + ".title"); Text(s.Content, 2500, section + ".body");
            Require(s.Exercises is not null && s.Exercises.Length <= 8, $"{section}: máximo 8 ejercicios asociados."); exercises += s.Exercises.Length;
            for (var j = 0; j < s.Exercises.Length; j++)
            {
                var e = s.Exercises[j]; var field = $"Sección {i + 1}, ejercicio {j + 1}";
                Require(e is not null && Id(e.Id), $"{field}, id: usa 1–40 caracteres ASCII alfanuméricos, guion o underscore.");
                Require(ids.Add(e.Id), $"{field}, id: identificador repetido; todos los IDs académicos deben ser únicos.");
                Require(e.ObjectiveId is not null && objectives.Contains(e.ObjectiveId), $"{field}, objectiveId: debe referenciar un objetivo existente de lesson.objectives.");
                Require(e.Activity is not null && e.Rubric is not null, $"{field}: faltan actividad o criterios.");
                Require(Enum.GetNames<AulaTutor.Domain.Tutoring.ExerciseKind>().Contains(e.Activity.Kind), $"{field}, kind: usa MultipleChoice, TrueFalse, ShortAnswer, OpenResponse o Code.");
                // Drafts may contain unfinished activities; approval uses the full shared validator.
                Text(e.Activity.Prompt, 1500, field + ", prompt"); Text(e.Activity.Rubric, 600, field + ", publicRubric");
                Require(e.Activity.Options is not null && e.Activity.Options.Length <= 5, $"{field}, options: máximo 5 opciones.");
                Require(e.Activity.AcceptedAnswers is not null && e.Activity.AcceptedAnswers.Length <= 8, $"{field}, acceptedAnswers: máximo 8 respuestas.");
                foreach (var option in e.Activity.Options) Text(option, 200, field + ", options");
                foreach (var answer in e.Activity.AcceptedAnswers) Text(answer, 200, field + ", acceptedAnswers");
                Require(e.Activity.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() == e.Activity.Options.Length, $"{field}, options: elimina las opciones repetidas (sin distinguir mayúsculas).");
                if (e.Activity.Kind is "OpenResponse" or "Code") Require(e.Activity.Options.Length == 0 && e.Activity.AcceptedAnswers.Length == 0, $"{field}: OpenResponse/Code requieren options y acceptedAnswers vacíos.");
                if (e.Activity.Kind == "ShortAnswer") Require(e.Activity.Options.Length == 0, $"{field}: ShortAnswer requiere options vacío.");
                if (e.Activity.Kind is "MultipleChoice" or "TrueFalse") Require(e.Activity.AcceptedAnswers.Length <= 1 && e.Activity.AcceptedAnswers.All(e.Activity.Options.Contains), $"{field}, acceptedAnswers: usa una respuesta idéntica a una opción, incluyendo mayúsculas y espacios.");
                Require(e.Rubric.Criteria is not null && e.Rubric.Criteria.Length <= 6, $"{field}, criteria: máximo 6 criterios.");
                foreach (var c in e.Rubric.Criteria) Text(c, 300, field + ", criteria");
                Text(e.Rubric.ReferenceSolution, 1500, field + ", referenceSolution"); Text(e.Rubric.HintRules, 600, field + ", hintRules");
            }
        }
        PreloadedRules.Validate(p);
        Require(exercises <= 24, "lesson.exercises: máximo 24 ejercicios en total.");
        var characters = LearningJson.Write(p).Length;
        Require(characters <= 20000, $"La clase ocupa {characters} caracteres en su representación interna; máximo 20000 (exceso: {characters - 20000}). " +
            "Reduce textos redundantes sin eliminar objetivos, ejercicios ni ayudas obligatorias. Este tamaño incluye estructura JSON y escapes Unicode; no equivale al tamaño del texto pegado.");
    }
    public static LessonValidation Review(LessonContent p)
    {
        ValidateDraft(p); var errors = new List<string>(); var warnings = new List<string>();
        try { PreloadedRules.Validate(p, true); } catch (AppException e) { errors.Add(e.Message); }
        if (p.Preloaded is not null) warnings.Add("Revisa que las primeras pistas, explicaciones y feedback no revelen soluciones privadas. La comprobación estructural no detecta todas las revelaciones semánticas.");
        if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.Subject) || string.IsNullOrWhiteSpace(p.Level) || p.DurationMinutes < 1) errors.Add("Completa título, tema, nivel y duración orientativa.");
        if (p.Objectives.Length == 0 || p.Objectives.Any(o => string.IsNullOrWhiteSpace(o.Text))) errors.Add("Define al menos un objetivo con texto.");
        if (p.Sections.Length == 0 || p.Sections.Any(s => string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Content))) errors.Add("Cada sección necesita título y contenido.");
        if (string.IsNullOrWhiteSpace(p.Methodology)) errors.Add("Define las reglas pedagógicas de la clase.");
        var exercises = p.Sections.SelectMany(s => s.Exercises).ToArray();
        if (exercises.Length == 0) errors.Add("Incluye al menos una actividad estructurada.");
        foreach (var e in exercises)
        {
            try { LearningOrchestrator.ValidateExercise(e.Activity); }
            catch (Exception ex) when (ex is JsonException or NullReferenceException) { errors.Add($"Actividad {e.Id}: completa consigna, rúbrica, opciones y regla privada válida."); }
            if (e.Rubric.Criteria.Length == 0 || e.Rubric.Criteria.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(e.Rubric.HintRules)) errors.Add($"Actividad {e.Id}: completa criterios y reglas de pistas.");
            if (e.Activity.Kind is "OpenResponse" or "Code") warnings.Add($"Actividad {e.Id}: evaluación orientativa; requiere revisión humana. No se ejecuta código ni SQL.");
        }
        foreach (var o in p.Objectives.Where(o => !exercises.Any(e => e.ObjectiveId == o.Id))) errors.Add($"Objetivo {o.Id}: falta una actividad asociada.");
        warnings.Add("La aprobación es responsabilidad docente; una regla válida no certifica corrección curricular ni dominio.");
        return new(errors.ToArray(), warnings.ToArray());
    }
}
