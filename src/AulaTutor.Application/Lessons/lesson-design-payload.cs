using System.Text.Json;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.Application.Lessons;

// Shallow provider-only wire format. Browser and persistence retain LessonContent.
public sealed record LessonDesignSection(string Id, string Title, string Body);
public sealed record LessonDesignExercise(string Id, string SectionId, string ObjectiveId, bool Required,
    string Kind, string Prompt, string[] Options, string[] AcceptedAnswers, string PublicRubric,
    string[] Criteria, string ReferenceSolution, string HintRules);
public sealed record LessonDesignPayload(string Explanation, string Title, string Subject, string Description,
    string Prerequisites, string Level, int DurationMinutes, string Methodology, string Instructions,
    LessonObjective[] Objectives, LessonDesignSection[] Sections, LessonDesignExercise[] Exercises)
{
    public LessonPlanProposal ToProposal()
    {
        if (Objectives is null || Sections is null || Exercises is null || Objectives.Length > 12 ||
            Sections.Length > 12 || Exercises.Length > 24 || Sections.Any(s => s is null) ||
            Exercises.Any(e => e is null || !Sections.Any(s => s.Id == e.SectionId))) throw new JsonException();
        var content = new LessonContent(Title, Subject, Description, Prerequisites, Level, DurationMinutes,
            Methodology, Instructions, Objectives, Sections.Select(s => new LessonSection(s.Id, s.Title, s.Body,
                Exercises.Where(e => e.SectionId == s.Id).Select(e => new LessonExercise(e.Id, e.ObjectiveId,
                    e.Required, new ExerciseProposal(e.Prompt, e.Kind, e.Options, e.AcceptedAnswers, e.PublicRubric),
                    new LessonRubric(e.Criteria, e.ReferenceSolution, e.HintRules))).ToArray())).ToArray());
        return new(Explanation, content);
    }
    public static LessonDesignPayload FromProposal(LessonPlanProposal p)
    {
        var c = p.Content;
        return new(p.Explanation, c.Title, c.Subject, c.Description, c.Prerequisites, c.Level, c.DurationMinutes,
            c.Methodology, c.Instructions, c.Objectives,
            c.Sections.Select(s => new LessonDesignSection(s.Id, s.Title, s.Content)).ToArray(),
            c.Sections.SelectMany(s => s.Exercises.Select(e => new LessonDesignExercise(e.Id, s.Id, e.ObjectiveId,
                e.Required, e.Activity.Kind, e.Activity.Prompt, e.Activity.Options, e.Activity.AcceptedAnswers,
                e.Activity.Rubric, e.Rubric.Criteria, e.Rubric.ReferenceSolution, e.Rubric.HintRules))).ToArray());
    }
}
