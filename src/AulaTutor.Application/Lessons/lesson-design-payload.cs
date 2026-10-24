using AulaTutor.Application.Identity;
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
        if (Objectives is null || Objectives.Length > 12) throw new AppException(400, "lesson.objectives: usa un array con máximo 12 objetivos.");
        if (Sections is null || Sections.Length > 12 || Sections.Any(s => s is null)) throw new AppException(400, "lesson.sections: usa un array con máximo 12 objetos de sección.");
        if (Exercises is null || Exercises.Length > 24) throw new AppException(400, "lesson.exercises: usa un array con máximo 24 ejercicios.");
        for (var i = 0; i < Exercises.Length; i++)
        {
            if (Exercises[i] is null) throw new AppException(400, $"lesson.exercises[{i}]: incluye el objeto completo del ejercicio.");
            if (!Sections.Any(s => s.Id == Exercises[i].SectionId)) throw new AppException(400, $"lesson.exercises[{i}].sectionId: debe referenciar una sección existente de lesson.sections.");
        }
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
