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
    { using var doc = JsonDocument.Parse(LearningJson.Write(LearningOrchestrator.SchemaFor(typeof(LessonDesignPayload)))); Assert.Contains("\"type\":\"boolean\"", doc.RootElement.GetRawText()); }
    [Fact] public void ShallowWireFormatPreservesSectionsAndPrivateRules()
    {
        var original = new LessonPlanProposal("Explicación", Content());
        var wire = LessonDesignPayload.FromProposal(original);
        Assert.Equal(LearningJson.Write(original), LearningJson.Write(wire.ToProposal()));
    }
    [Theory] [InlineData("orphan-section")] [InlineData("orphan-objective")] [InlineData("duplicate-id")] [InlineData("invalid-closed-rule")]
    public async Task ProviderPayloadCannotDropOrAcceptInvalidActivities(string problem)
    {
        var wire = LessonDesignPayload.FromProposal(new("Explicación", Content())); var e=wire.Exercises[0];
        wire.Exercises[0] = problem switch {
            "orphan-section" => e with { SectionId="missing" }, "orphan-objective" => e with { ObjectiveId="missing" },
            "duplicate-id" => e with { Id="s1" }, _ => e with { Options=[] }
        };
        var provider = new DesignProvider(LearningJson.Write(wire));
        var op=new LessonOperation { Action="Plan", RequestId=Guid.NewGuid(), ProviderId="Groq", ModelId="test", ContextJson="{}" };
        var error=await Assert.ThrowsAsync<AiProviderException>(()=>new LessonOrchestrator(provider,new(provider)).DesignAsync(op,default));
        Assert.Equal(AiErrorCode.InvalidResponse,error.Code);
    }
    [Fact] public async Task RealProviderContractIsShallowStrictAndUsesVersionedBudget()
    {
        var provider=new DesignProvider(LearningJson.Write(LessonDesignPayload.FromProposal(new("Explicación",Content()))));
        var op=new LessonOperation { Action="Exercises", RequestId=Guid.NewGuid(), ProviderId="Groq", ModelId="test", ContextJson="{}" };
        await new LessonOrchestrator(provider,new(provider)).DesignAsync(op,default);
        Assert.Equal(4096,provider.Request!.MaxOutputTokens); Assert.Equal("lesson-design-v2",provider.Request.PromptVersion);
        Assert.True(provider.Request.ResponseContract.Strict);
        using var schema=JsonDocument.Parse(provider.Request.ResponseContract.JsonSchema!);
        var properties=schema.RootElement.GetProperty("properties");
        Assert.Equal("object",properties.GetProperty("exercises").GetProperty("items").GetProperty("type").GetString());
        Assert.False(properties.GetProperty("sections").GetProperty("items").GetProperty("properties").TryGetProperty("exercises",out _));
    }
    private sealed class DesignProvider(string output):IAiProvider
    {
        public string ProviderId=>"Groq"; public AiCapabilities Capabilities=>new(true,true,true,false);
        public AiChatRequest? Request{get;private set;}
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)=>Task.FromResult(new AiHealthResult(true,"Groq","test"));
        public Task<AiChatResult> GenerateAsync(AiChatRequest request,CancellationToken ct)
        { Request=request; return Task.FromResult(new AiChatResult(output,"Groq","test",null,output,"stop")); }
    }
}
