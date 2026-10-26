using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Ai;

namespace AulaTutor.UnitTests;

public sealed class PreparedLearningTests
{
    private sealed class CountingProvider : IAiProvider
    {
        public int Calls; public string? Override; public string Finish = "stop";
        public string ProviderId => "Mock"; public AiCapabilities Capabilities => new(false, false, false, false);
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct) => Task.FromResult(new AiHealthResult(true, "Mock", MockAiProvider.ModelId));
        public async Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
        { Calls++; var r = await new MockAiProvider().GenerateAsync(request, ct); return r with { Text = Override ?? r.Text, FinishReason = Finish }; }
    }
    private static TutoringSession Session(string experience = "Tutoring") => new() { LearningExperience = experience, LearningModality = "Prepared", Topic = "Evidencias", Goal = "Contrastar ejemplos", ProviderId = "Mock", ModelId = MockAiProvider.ModelId };
    private static PreparedLearning Prepared(string length = "Short") => new() { Length = length, ContextWindow = 65536, MaxOutputTokens = 12288 };
    private static TutoringTurn Generation(string experience, string length) => new() { Action = TutoringAction.PrepareSession, RequestId = Guid.NewGuid(),
        ContextJson = LearningJson.Write(new { topic = "Evidencias", goal = "Contrastar ejemplos", level = "Beginner", experience, count = PreparedRules.Count(experience, length) }) };
    [Theory]
    [InlineData("Tutoring", "Short", 3)][InlineData("Tutoring", "Medium", 6)][InlineData("Tutoring", "Long", 9)]
    [InlineData("Diagnostic", "Short", 4)][InlineData("Diagnostic", "Medium", 8)][InlineData("Diagnostic", "Long", 12)]
    public async Task CompleteGenerationUsesExactlyOneLogicalCallWithIndependentLengths(string experience, string length, int count)
    {
        var provider = new CountingProvider(); var r = await new PreparedOrchestrator(provider).GenerateAsync(Session(experience), Prepared(length), Generation(experience, length), default);
        var c = LearningJson.Read<PreparedContent>(r.LearningPayload!); Assert.Equal(count, c.Activities.Length); Assert.Equal(1, provider.Calls);
        Assert.All(c.Activities, a => Assert.NotEmpty(a.ReferenceSolution));
        if (experience == "Diagnostic") Assert.All(c.Activities, a => { Assert.Empty(a.Aids.Hints); Assert.Empty(a.Explanation); Assert.Empty(a.Aids.CorrectFeedback); });
        else Assert.All(c.Activities, a => { Assert.NotEmpty(a.Explanation); Assert.NotEmpty(a.Example); Assert.NotEmpty(a.Aids.Hints); });
    }
    [Theory][InlineData("null")][InlineData("{}")][InlineData("{\"format\":\"prepared-learning-v1\"")][InlineData("[]")][InlineData("```json\n{}\n```")]
    public async Task InvalidOrIncompleteJsonDoesNotRepairOrCallAgain(string response)
    {
        var p = new CountingProvider { Override = response }; var e = await Assert.ThrowsAsync<AiProviderException>(() => new PreparedOrchestrator(p).GenerateAsync(Session(), Prepared(), Generation("Tutoring", "Short"), default));
        Assert.Equal(AiErrorCode.InvalidResponse, e.Code); Assert.Equal(1, p.Calls);
    }
    [Fact] public async Task TruncatedOutputIsRejectedEvenIfItsJsonIsValid()
    {
        var p = new CountingProvider { Finish = "length" }; await Assert.ThrowsAsync<AiProviderException>(() => new PreparedOrchestrator(p).GenerateAsync(Session(), Prepared(), Generation("Tutoring", "Short"), default)); Assert.Equal(1, p.Calls);
    }
    [Theory][InlineData("capacity")][InlineData("context")]
    public async Task ImpossibleCapacityRejectsBeforeAnyLogicalOrPhysicalCall(string limit)
    {
        var p = new CountingProvider(); var prepared = Prepared(); if (limit == "capacity") prepared.MaxOutputTokens = 2048; else prepared.ContextWindow = 1024;
        await Assert.ThrowsAsync<AiProviderException>(() => new PreparedOrchestrator(p).GenerateAsync(Session(), prepared, Generation("Tutoring", "Short"), default)); Assert.Equal(0, p.Calls);
    }
    [Theory][InlineData("objective")][InlineData("count")][InlineData("solution")][InlineData("aid")][InlineData("answer")]
    public async Task StructuralReferencesPrivateSolutionsAndClosedRulesAreRequired(string defect)
    {
        var p = new CountingProvider(); var r = await new PreparedOrchestrator(p).GenerateAsync(Session(), Prepared(), Generation("Tutoring", "Short"), default);
        var c = LearningJson.Read<PreparedContent>(r.LearningPayload!); var a = c.Activities[0];
        if (defect == "objective") c.Activities[0] = a with { ObjectiveId = "foreign" };
        if (defect == "count") c = c with { Activities = c.Activities[..2] };
        if (defect == "solution") c.Activities[0] = a with { ReferenceSolution = "" };
        if (defect == "aid") c.Activities[0] = a with { Aids = a.Aids with { Hints = ["Falso"] } };
        if (defect == "answer") c.Activities[0] = a with { Exercise = a.Exercise with { AcceptedAnswers = [] } };
        Assert.Throws<JsonException>(() => PreparedRules.Validate(c, "Tutoring", "Short"));
    }
    [Fact] public void FindingsRejectInventedEvidenceAndCrossObjectiveReferences()
    {
        var id = Guid.NewGuid(); var c = new PreparedContent(PreparedRules.Version, "Introducción", [new("o1", "Uno"), new("o2", "Dos"), new("o3", "Tres")], []);
        Assert.Throws<JsonException>(() => PreparedRules.ValidateFindings([new("o1", "Texto", [Guid.NewGuid()])], c, new Dictionary<Guid, string> { [id] = "o1" }));
        Assert.Throws<JsonException>(() => PreparedRules.ValidateFindings([new("o2", "Texto", [id])], c, new Dictionary<Guid, string> { [id] = "o1" }));
    }
    [Theory][InlineData("invented-objective")][InlineData("foreign-evidence")][InlineData("wrong-objective-evidence")]
    public async Task GroupedDiagnosisRejectsUnfoundedReportOrPlanWithoutThirdCall(string defect)
    {
        var p=new CountingProvider();var engine=new PreparedOrchestrator(p);var s=Session("Diagnostic");var prepared=Prepared();
        prepared.ContentJson=(await engine.GenerateAsync(s,prepared,Generation("Diagnostic","Short"),default)).LearningPayload;
        var content=LearningJson.Read<PreparedContent>(prepared.ContentJson!);var ids=Enumerable.Range(0,3).Select(_=>Guid.NewGuid()).ToArray();
        var context=LearningJson.Write(new{s.Goal,preliminary=false,content.Objectives,evidence=ids.Select((id,i)=>new{id,objectiveId=content.Objectives[i].Id})});
        var raw=await new MockAiProvider().GenerateAsync(new("prepared_diagnostic_evaluate",[new("user",context)],new("text"),Guid.NewGuid().ToString()),default);
        var result=LearningJson.Read<PreparedDiagnosticEvaluation>(raw.Text);
        if(defect=="invented-objective")result.Plan.Objectives[0]=result.Plan.Objectives[0] with {Title="Objetivo no evaluado"};
        if(defect=="wrong-objective-evidence")result.Plan.Objectives[0]=result.Plan.Objectives[0] with {EvidenceIds=[ids[1]]};
        if(defect=="foreign-evidence")result=result with {Report=result.Report with {Observations=[new("Afirmación ajena",[Guid.NewGuid()])]}};
        p.Override=LearningJson.Write(result);
        var error=await Assert.ThrowsAsync<AiProviderException>(()=>engine.GenerateAsync(s,prepared,new(){Action=TutoringAction.FinalReport,RequestId=Guid.NewGuid(),ContextJson=context},default));
        Assert.Equal(AiErrorCode.InvalidResponse,error.Code);Assert.Equal(2,p.Calls);
    }
    [Fact] public async Task DuplicatePropertiesAreRejectedEvenIfLastValueWouldBeValid()
    {
        var p=new CountingProvider();var engine=new PreparedOrchestrator(p);var s=Session();var prepared=Prepared();
        var valid=await engine.GenerateAsync(s,prepared,Generation("Tutoring","Short"),default);
        p.Override="{\"format\":\"wrong\","+valid.LearningPayload![1..];
        await Assert.ThrowsAsync<AiProviderException>(()=>engine.GenerateAsync(s,prepared,Generation("Tutoring","Short"),default));Assert.Equal(2,p.Calls);
    }
}
