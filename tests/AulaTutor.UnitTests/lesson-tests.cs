using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Ai;

namespace AulaTutor.UnitTests;

public sealed class LessonTests
{
    private static LessonContent Content() => new("Título", "SQL", "", "", "Básico", 45, "Explicar y practicar", "",
        [new("o1", "Comprender el concepto")], [new("s1", "Introducción", "Contenido", [new("e1", "o1", true,
            new("Consigna", "TrueFalse", ["Verdadero", "Falso"], ["Falso"], "Criterio"), new(["Razona la respuesta"], "Referencia", "Una pista antes de la solución"))])]);
    [Fact] public void EmptyDraftCanBeSavedButCannotBeApproved()
    { var p = new LessonContent("", "", "", "", "", 0, "", "", [], []); LessonValidationRules.ValidateDraft(p); Assert.NotEmpty(LessonValidationRules.Review(p).Errors); }
    [Fact] public void CompletePlanIsReviewable()
    { Assert.Empty(LessonValidationRules.Review(Content()).Errors); }
    [Theory] [InlineData("unknown-reference")] [InlineData("duplicate-id")] [InlineData("invalid-kind")] [InlineData("private-rule")] [InlineData("oversize")] [InlineData("null-rubric")]
    public void RejectsInvalidStructure(string problem)
    {
        var p = Content(); var s = p.Sections[0]; var e = s.Exercises[0];
        p = problem switch {
            "unknown-reference" => p with { Sections = [s with { Exercises = [e with { ObjectiveId = "other" }] }] },
            "duplicate-id" => p with { Sections = [s, s] },
            "invalid-kind" => p with { Sections = [s with { Exercises = [e with { Activity = e.Activity with { Kind = "ExecuteSQL" } }] }] },
            "private-rule" => p with { Sections = [s with { Exercises = [e with { Activity = e.Activity with { AcceptedAnswers = ["Missing option"] } }] }] },
            "oversize" => p with { Instructions = new string('x', 1501) },
            _ => p with { Sections = [s with { Exercises = [e with { Rubric = null! }] }] }
        };
        Assert.Throws<AppException>(() => LessonValidationRules.ValidateDraft(p));
    }
    [Fact] public void ApprovalRequiresRubricAndHintCriteria()
    { var p = Content(); var e = p.Sections[0].Exercises[0]; p.Sections[0].Exercises[0] = e with { Rubric = new([], "", "") }; Assert.NotEmpty(LessonValidationRules.Review(p).Errors); }
    [Fact] public void UnfinishedActivitiesCanBeSavedButNotApproved()
    { var p=Content();var e=p.Sections[0].Exercises[0];p.Sections[0].Exercises[0]=e with {Activity=new("","TrueFalse",[],[],"")};LessonValidationRules.ValidateDraft(p);Assert.NotEmpty(LessonValidationRules.Review(p).Errors); }
    [Fact] public void OpenAndCodeActivitiesRemainOrientative()
    { var p = Content(); var e = p.Sections[0].Exercises[0]; p.Sections[0].Exercises[0] = e with { Activity = new("Escribe SQL", "Code", [], [], "Explicación") }; Assert.Contains(LessonValidationRules.Review(p).Warnings, w => w.Contains("revisión humana")); }
    [Fact] public async Task MockProducesAValidatedIndependentProposal()
    {
        var provider = new MockAiProvider(); var op = new LessonOperation { Action = "Plan", RequestId = Guid.NewGuid(), ProviderId = "Mock", ModelId = MockAiProvider.ModelId, ContextJson = LearningJson.Write(new { draft = Content(), request = "Agregar ejemplos" }) };
        var proposal = await new LessonOrchestrator(provider, new(provider)).DesignAsync(op, default);
        Assert.Empty(LessonValidationRules.Review(proposal.Content).Errors); Assert.Contains("Mock", proposal.Explanation); Assert.Contains("Simulación", proposal.Content.Methodology);
        Assert.Equal("Contenido", Content().Sections[0].Content);
    }
    [Fact] public void PortableSchemaIncludesBooleansWithoutFrameworkDependencies()
    { using var doc = JsonDocument.Parse(LearningJson.Write(LearningOrchestrator.SchemaFor(typeof(LessonPlanProposal)))); Assert.Contains("\"type\":\"boolean\"", doc.RootElement.GetRawText()); }
}
