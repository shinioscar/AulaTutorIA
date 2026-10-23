using AulaTutor.Application.Ai;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Ai;

namespace AulaTutor.UnitTests;

public sealed class PreloadedTests
{
    private sealed class CountingProvider : IAiProvider
    {
        private readonly MockAiProvider mock=new();public int Calls;public string? Override;public string? Finish;
        public string ProviderId=>mock.ProviderId;public AiCapabilities Capabilities=>mock.Capabilities;
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)=>mock.CheckHealthAsync(ct);
        public async Task<AiChatResult> GenerateAsync(AiChatRequest r,CancellationToken ct){Calls++;var result=await mock.GenerateAsync(r,ct);return result with {Text=Override??result.Text,FinishReason=Finish??result.FinishReason};}
    }
    private static LessonExternalOptions Options(int count=3)=>new("Tema ficticio","Asignatura","Básico","Practicar",count,"Gradual","Variados",3,40,"","",true);
    private static LessonOperation Operation()=>new(){Action="PreloadedPlan",RequestId=Guid.NewGuid(),ProviderId="Mock",ModelId=MockAiProvider.ModelId,
        Text=LearningJson.Write(Options()),ContextJson=LearningJson.Write(new {draft=new LessonContent("Clase","Tema","","","Básico",40,"Practicar","",[],[])})};
    [Fact] public async Task PreloadedGenerationIsOneCallAndPreservesStructuredHintsAndAlternatives()
    {var p=new CountingProvider();var output=await new LessonOrchestrator(p,new(p)).DesignAsync(Operation(),default);Assert.Equal(1,p.Calls);Assert.NotNull(output.Content.Preloaded);Assert.All(output.Content.Preloaded!.Exercises,a=>{Assert.Equal(3,a.Hints.Length);Assert.Equal(2,a.Alternatives.Length);});Assert.Empty(LessonValidationRules.Review(output.Content).Errors);}
    [Theory][InlineData("incomplete")][InlineData("truncated")]
    public async Task PreloadedGenerationRejectsIncompleteOrTruncatedResponses(string problem)
    {var p=new CountingProvider();if(problem=="incomplete")p.Override="{}";else p.Finish="length";await Assert.ThrowsAsync<AiProviderException>(()=>new LessonOrchestrator(p,new(p)).DesignAsync(Operation(),default));Assert.Equal(1,p.Calls);}
    [Fact] public void PreloadedPromptAndImportUseSameVersionedSchemaAndBoundSingleRequest()
    {var prompt=LessonImport.Prompt(Options());Assert.Contains(PreloadedRules.Schema,prompt);Assert.Contains(PreloadedRules.Format,prompt);Assert.Equal(prompt,LessonImport.Prompt(Options()));Assert.Throws<AulaTutor.Application.Identity.AppException>(()=>LessonImport.Prompt(Options(5)));}
    [Fact] public async Task PreloadedV2RejectsMissingAidsWrongReferencesAndDirectFirstKey()
    {
        var p=new CountingProvider();var c=(await new LessonOrchestrator(p,new(p)).DesignAsync(Operation(),default)).Content;
        foreach(var broken in new[]{c with {Preloaded=c.Preloaded! with {Exercises=[]}},c with {Preloaded=c.Preloaded! with {Exercises=[null!,c.Preloaded!.Exercises[1]]}},c with {Preloaded=c.Preloaded! with {Exercises=[c.Preloaded!.Exercises[0] with {ExerciseId="missing"}]}},c with {Preloaded=c.Preloaded! with {Exercises=[c.Preloaded!.Exercises[0] with {Hints=["Falso"]},c.Preloaded.Exercises[1]]}},c with {Preloaded=c.Preloaded! with {Exercises=[c.Preloaded!.Exercises[0] with {Hints=["La respuesta es Falso."]},c.Preloaded.Exercises[1]]}}})
            Assert.Throws<AulaTutor.Application.Identity.AppException>(()=>LessonValidationRules.ValidateDraft(broken));
        var wire=new LessonImportV2(PreloadedRules.Format,LessonDesignPayload.FromProposal(new("Revisar",c)),c.Preloaded!);
        Assert.NotNull(LessonImport.Preview(LearningJson.Write(wire)).Content?.Preloaded);
        var v1=LessonImport.Preview(LearningJson.Write(new LessonImportV1(LessonImport.Format,wire.Lesson)));Assert.NotNull(v1.Content);Assert.Null(v1.Content!.Preloaded);Assert.DoesNotContain("\"preloaded\"",LearningJson.Write(v1.Content));
    }
}
