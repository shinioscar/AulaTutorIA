using System.Text.Json;

namespace AulaTutor.Application.Tutoring;

// One activity per objective is part of the contract, not another provider-generated array.
public sealed record LearningPlanObjectivePayload(string Title, Guid[] EvidenceIds, string Explanation,
    string Difficulty, int EstimatedMinutes, ExerciseProposal Exercise);
public sealed record LearningPlanPayload(string MainGoal, string Progression, LearningPlanObjectivePayload[] Objectives)
{
    public PlanContent ToPlan()
    {
        if (Objectives is null || Objectives.Length > 5 || Objectives.Any(o => o is null)) throw new JsonException();
        return new(MainGoal, Progression, Objectives.Select(o => new LearningPlanObjective(o.Title, o.EvidenceIds,
            [new(o.Explanation, o.Difficulty, o.EstimatedMinutes, o.Exercise)])).ToArray());
    }
    public static LearningPlanPayload FromPlan(PlanContent p) => new(p.MainGoal, p.Progression,
        p.Objectives.Select(o => new LearningPlanObjectivePayload(o.Title, o.EvidenceIds, o.Activities.Single().Explanation,
            o.Activities.Single().Difficulty, o.Activities.Single().EstimatedMinutes, o.Activities.Single().Exercise)).ToArray());
}
