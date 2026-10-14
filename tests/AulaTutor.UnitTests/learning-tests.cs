using System.Text.Json;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Application.Ai;

namespace AulaTutor.UnitTests;

public sealed class LearningTests
{
    [Theory]
    [InlineData(ExerciseKind.MultipleChoice,"B",0,AttemptOutcome.CorrectWithoutHelp)]
    [InlineData(ExerciseKind.TrueFalse,"b",1,AttemptOutcome.CorrectWithHelp)]
    [InlineData(ExerciseKind.ShortAnswer,"  b  ",0,AttemptOutcome.CorrectWithoutHelp)]
    [InlineData(ExerciseKind.ShortAnswer,"A",2,AttemptOutcome.Incorrect)]
    [InlineData(ExerciseKind.OpenResponse,"B",0,AttemptOutcome.NeedsReview)]
    [InlineData(ExerciseKind.Code,"DROP TABLE Users",0,AttemptOutcome.NeedsReview)]
    public void ValidatorsSeparateRulesFromUnverifiedInterpretation(ExerciseKind kind,string answer,int hints,AttemptOutcome outcome)
    {var result=ExerciseValidators.Validate(kind,answer,["B"],hints);Assert.Equal(outcome,result.Outcome);Assert.Equal(kind is ExerciseKind.OpenResponse or ExerciseKind.Code?"Unverified":"Deterministic",result.Type);}
    [Fact]
    public void MissingRuleIsNotEvaluable()=>Assert.Equal(AttemptOutcome.NotEvaluable,ExerciseValidators.Validate(ExerciseKind.ShortAnswer,"anything",[],0).Outcome);
    [Fact]
    public void DiagnosticClaimsRequireExistingEvidenceAndInsufficientEvidenceCannotSupportClaims()
    {
        var id=Guid.NewGuid();var content=new AssessmentContent("Interpretación",[new("Respuesta observada",[id])],[],["Transferencia"],["Practicar"]);
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.AnalyzeDiagnosis,LearningJson.Write(content),[],false));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.AnalyzeDiagnosis,LearningJson.Write(content),[id],true));
        Assert.Contains("Interpretación",LearningOrchestrator.ValidatePayload(TutoringAction.AnalyzeDiagnosis,LearningJson.Write(content),[id],false));
    }
    [Fact]
    public void PlanBoundsPrivateRulesAndUnknownFieldsAreRejected()
    {
        var exercise=new ExerciseProposal("Pregunta", "TrueFalse",["Verdadero","Falso"],["Falso"],"Criterios");
        var activity=new LearningPlanActivity("Explicación","Beginner",5,exercise);
        var plan=new PlanContent("Meta","Progresión",Enumerable.Range(0,3).Select(i=>new LearningPlanObjective("Objetivo "+i,[],[activity])).ToArray());
        Assert.Contains("lista",LearningOrchestrator.ValidatePayload(TutoringAction.GeneratePlan,LearningJson.Write(plan),[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.GeneratePlan,LearningJson.Write(plan with{Objectives=[]}),[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.GeneratePlan,LearningJson.Write(plan with{Objectives=[new("Objetivo",[],[activity with{EstimatedMinutes=99}]),plan.Objectives[1],plan.Objectives[2]]}),[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidateExercise(exercise with{AcceptedAnswers=["Otra"]}));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidateExercise(exercise with{Kind="Code"}));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.GenerateExercise,"{\"prompt\":\"Pregunta\",\"kind\":\"Code\",\"options\":[],\"acceptedAnswers\":[],\"rubric\":\"Criterios\",\"grantAdmin\":true}",[],true));
    }
    [Fact]
    public void ReportCannotInjectStatisticsOrInventEvidence()
    {
        var report=new ReportContent("Resumen",[],["Dominio no evaluado"],[new("Práctica",[])]);
        var json=LearningJson.Write(report);
        Assert.Contains("Resumen",LearningOrchestrator.ValidatePayload(TutoringAction.FinalReport,json,[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.FinalReport,json[..^1]+",\"correctWithoutHelp\":100}",[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.FinalReport,LearningJson.Write(report with{Observations=[new("Dominio",[Guid.NewGuid()])]}),[],true));
    }
    [Fact] public void PlanWireFormatPreservesExactlyOneValidatedActivityPerObjective()
    {
        var exercise=new ExerciseProposal("Explica el concepto","OpenResponse",[],[],"Pertinencia");
        var p=new PlanContent("Meta","Ruta",Enumerable.Range(0,3).Select(i=>new LearningPlanObjective("Objetivo "+i,[],[new("Explicación","Beginner",5,exercise)])).ToArray());
        var payload=LearningPlanPayload.FromPlan(p);var restored=payload.ToPlan();
        Assert.Equal(LearningJson.Write(p),LearningJson.Write(restored));
        Assert.Contains("lista",LearningOrchestrator.ValidatePayload(TutoringAction.GeneratePlan,LearningJson.Write(restored),[],true));
        Assert.Throws<JsonException>(()=>LearningOrchestrator.ValidatePayload(TutoringAction.GeneratePlan,LearningJson.Write(payload with {Objectives=[]}),[],true));
    }
    [Fact]
    public void DiagnosticPolicyIsBoundedAndConfigurable()
    {Assert.True(new LearningPolicy{MaxDiagnosticQuestions=3,MinimumEvidence=1}.IsValid());Assert.False(new LearningPolicy{MaxDiagnosticQuestions=0}.IsValid());Assert.False(new LearningPolicy{MinimumEvidence=9}.IsValid());}
    private sealed class StructuredProvider(bool schema) : IAiProvider
    {
        public AiChatRequest? Request;
        public string ProviderId=>"Simulated";
        public AiCapabilities Capabilities=>new(true,schema,schema,false);
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)=>Task.FromResult(new AiHealthResult(true,ProviderId,"test-model"));
        public Task<AiChatResult> GenerateAsync(AiChatRequest request,CancellationToken ct)
        {Request=request;return Task.FromResult(new AiChatResult(LearningJson.Write(new ExerciseProposal("Pregunta diagnóstica","OpenResponse",[],[],"Criterios")),ProviderId,"test-model",null,null,"stop"));}
    }
    [Theory]
    [InlineData(true,"json-schema")]
    [InlineData(false,"json")]
    public async Task LearningUsesPortableProviderContractWithoutProviderSpecificDependency(bool schema,string format)
    {
        var provider=new StructuredProvider(schema);var engine=new LearningOrchestrator(provider);
        var s=new TutoringSession{ProviderId=provider.ProviderId,ModelId="test-model"};
        var reply=await engine.GenerateAsync(s,new TutoringTurn{Action=TutoringAction.DiagnosticQuestion,RequestId=Guid.NewGuid(),ContextJson="{\"evidence\":[],\"preliminary\":true}"},CancellationToken.None);
        Assert.Equal("Pregunta diagnóstica",reply.Text);Assert.Equal(format,provider.Request!.ResponseContract.Format);Assert.Equal(2048,provider.Request.MaxOutputTokens);Assert.Equal("learning-v2",provider.Request.PromptVersion);
        Assert.Equal("system",provider.Request.Messages[0].Role);Assert.DoesNotContain("Groq",provider.Request.Messages[0].Text);Assert.Equal(schema,provider.Request.ResponseContract.Strict);
    }
}
