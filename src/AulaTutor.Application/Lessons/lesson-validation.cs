using System.Text.Json;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

public static class LessonValidationRules
{
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition) { if (!condition) throw new AppException(400, "Revisa la estructura, referencias y límites de la clase."); }
    private static void Text(string? value, int max) => Require(value is not null && value.Length <= max && !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')));
    private static bool Id(string? id) => id is not null && id.Length is >= 1 and <= 40 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static void ValidateDraft(LessonContent p)
    {
        Require(p is not null); Text(p.Title, 160); Text(p.Subject, 160); Text(p.Description, 1000);
        Text(p.Prerequisites, 1000); Text(p.Level, 100); Text(p.Methodology, 1500); Text(p.Instructions, 1500);
        Require(p.DurationMinutes is >= 0 and <= 480 && p.Objectives is not null && p.Objectives.Length <= 12 && p.Sections is not null && p.Sections.Length <= 12);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in p.Objectives!) { Require(o is not null && Id(o.Id) && ids.Add(o.Id)); Text(o.Text, 500); }
        var objectives = p.Objectives.Select(x => x.Id).ToHashSet();
        var exercises = 0;
        foreach (var s in p.Sections!)
        {
            Require(s is not null && Id(s.Id) && ids.Add(s.Id)); Text(s.Title, 160); Text(s.Content, 2500);
            Require(s.Exercises is not null && s.Exercises.Length <= 8); exercises += s.Exercises!.Length;
            foreach (var e in s.Exercises)
            {
                Require(e is not null && Id(e.Id) && ids.Add(e.Id) && e.ObjectiveId is not null && objectives.Contains(e.ObjectiveId) && e.Activity is not null && e.Rubric is not null);
                Require(Enum.GetNames<AulaTutor.Domain.Tutoring.ExerciseKind>().Contains(e.Activity.Kind));
                // Drafts may contain unfinished activities; approval uses the full shared validator.
                Text(e.Activity.Prompt, 1500); Text(e.Activity.Rubric, 600);
                Require(e.Activity.Options is not null && e.Activity.Options.Length <= 5 && e.Activity.AcceptedAnswers is not null && e.Activity.AcceptedAnswers.Length <= 8);
                foreach (var option in e.Activity.Options!) Text(option, 200);
                foreach (var answer in e.Activity.AcceptedAnswers!) Text(answer, 200);
                Require(e.Activity.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() == e.Activity.Options.Length);
                if (e.Activity.Kind is "OpenResponse" or "Code") Require(e.Activity.Options.Length == 0 && e.Activity.AcceptedAnswers.Length == 0);
                if (e.Activity.Kind == "ShortAnswer") Require(e.Activity.Options.Length == 0);
                if (e.Activity.Kind is "MultipleChoice" or "TrueFalse") Require(e.Activity.AcceptedAnswers.Length <= 1 && e.Activity.AcceptedAnswers.All(e.Activity.Options.Contains));
                Require(e.Rubric.Criteria is not null && e.Rubric.Criteria.Length <= 6);
                foreach (var c in e.Rubric.Criteria!) Text(c, 300);
                Text(e.Rubric.ReferenceSolution, 1500); Text(e.Rubric.HintRules, 600);
            }
        }
        PreloadedRules.Validate(p);
        Require(exercises <= 24 && LearningJson.Write(p).Length <= 20000);
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
