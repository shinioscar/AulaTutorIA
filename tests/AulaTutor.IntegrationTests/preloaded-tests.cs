using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private static LessonContent PreloadedSample()
    {
        var c = Content();
        var open = c.Sections[1].Exercises[0]; c.Sections[1].Exercises[0] = open with { Rubric = open.Rubric with { ReferenceSolution = "Referencia privada para revisión de la respuesta abierta." } };
        var aids = c.Sections.SelectMany(s => s.Exercises).Select(e => new PreloadedExercise(e.Id,
            ["PISTA_UNO", "PISTA_DOS_NO_ENVIADA", "PISTA_TRES_NO_ENVIADA"], ["EXPLICACION_ALTERNATIVA"],
            "Coincide con la regla; no acredita dominio.", "Revisa el razonamiento.", "Pendiente de revisión humana.", ["Compara varios ejemplos."], ["Prueba otro ejemplo de práctica."])).ToArray();
        return c with { Preloaded = new("Introducción precargada ficticia", aids) };
    }
    private async Task<TutoringSessionView> FinishPreloaded(ClassFixture f)
    {
        var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));
        s=await ClassAct(f.UserA,s,"Hint");s=await ClassAct(f.UserA,s,"SubmitAttempt","Falso",ApprovedCurrent(s).Id);
        s=await ClassAct(f.UserA,s,"NextActivity");
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Evidencia abierta ficticia",ApprovedCurrent(s).Id);
        return await ClassTransition(f.UserA,s,"complete");
    }
    [Fact] public async Task PreloadedSessionUsesZeroProviderCallsForAttemptsHintsNavigationAndCompletion()
    {
        using var f=await AssignedFixture(PreloadedSample());provider.Failure=new(AiErrorCode.RateLimited,TimeSpan.FromMinutes(1));
        var s=await StartClass(f.A,f.Assignment.Id);Assert.Equal("preloaded-class-v1",s.PolicyVersion);Assert.Equal(0,provider.Calls);
        s=await ClassAct(f.UserA,s);var first=ApprovedCurrent(s);Assert.DoesNotContain("PISTA_DOS_NO_ENVIADA",LearningJson.Write(s));Assert.DoesNotContain("REFERENCIA_PRIVADA",LearningJson.Write(s));
        var key=Guid.NewGuid();s=await ClassAct(f.UserA,s,"Hint",key:key);var revision=s.Revision;Assert.Contains("PISTA_UNO",LearningJson.Write(s));Assert.DoesNotContain("PISTA_DOS_NO_ENVIADA",LearningJson.Write(s));
        s=await ClassAct(f.UserA,s,"Hint",key:key);Assert.Equal(revision,s.Revision);Assert.Equal(1,ApprovedCurrent(s).Hints);
        s=await ClassAct(f.UserA,s,"Hint");Assert.Equal(2,ApprovedCurrent(s).Hints);
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Verdadero",first.Id);Assert.Equal("Incorrect",s.Learning!.Attempts.Single().Outcome);
        await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"NextActivity"));
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Falso",first.Id);Assert.Equal("CorrectWithHelp",s.Learning!.Attempts.Last().Outcome);
        s=await ClassTransition(f.UserA,s,"pause");s=await ClassTransition(f.UserA,s,"resume");s=await ClassAct(f.UserA,s,"NextActivity");
        s=await ClassAct(f.UserA,s,"Simplify");s=await ClassAct(f.UserA,s,"SubmitAttempt","Respuesta abierta",ApprovedCurrent(s).Id);
        Assert.Equal("NeedsReview",s.Learning!.Attempts.Last().Outcome);s=await ClassTransition(f.UserA,s,"complete");
        Assert.Equal("Completed",s.State);Assert.Single(s.Learning!.Reports);Assert.Equal("Application",s.Learning.Reports[0].ProviderId);Assert.Equal(0,provider.Calls);
        var other=await StartClass(f.B,f.Assignment.Id);Assert.Equal("Created",other.State);Assert.Empty(other.Learning!.Attempts);
        Assert.Equal(s.Revision,(await f.A.GetFromJsonAsync<TutoringSessionView>($"/api/tutoring-sessions/{s.Id}"))!.Revision);
    }
    [Fact] public async Task PreloadedFinalEvaluationIsOneGroupedCallWithAllEvidenceAndTeacherReferences()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await FinishPreloaded(f);Assert.Equal(0,provider.Calls);
        var key=Guid.NewGuid();s=await ClassAct(f.UserA,s,"FinalReport",key:key);Assert.Equal(1,provider.Calls);
        var report=Assert.Single(s.Learning!.Reports,r=>r.ProviderId!="Application");Assert.Equal("preloaded-evaluation-v1",report.PromptVersion);
        using var context=System.Text.Json.JsonDocument.Parse(provider.Requests.Single().Messages.Last().Text);
        Assert.Equal(s.Learning!.Attempts.Length,context.RootElement.GetProperty("evidence").GetArrayLength());Assert.True(context.RootElement.TryGetProperty("evidenceHash",out _));
        Assert.Equal("PISTA_UNO",Assert.Single(context.RootElement.GetProperty("helps").EnumerateArray()).GetProperty("text").GetString());
        Assert.DoesNotContain("PISTA_DOS_NO_ENVIADA",provider.Requests[0].Messages.Last().Text);
        Assert.DoesNotContain(f.UserA.Email,provider.Requests[0].Messages.Last().Text);Assert.DoesNotContain("REFERENCIA_PRIVADA",provider.Requests[0].Messages.Last().Text);
        s=await ClassAct(f.UserA,s,"FinalReport",key:key);Assert.Equal(1,provider.Calls);
        await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"FinalReport"));Assert.Equal(1,provider.Calls);
        var teacher=await Evidence(f);Assert.NotEmpty(teacher.AiObservations);Assert.All(teacher.AiObservations,o=>Assert.All(o.EvidenceIds,id=>Assert.Contains(s.Learning!.Attempts,a=>a.Id==id)));
        Assert.All(s.Learning!.Attempts.Where(a=>a.ExerciseId==s.Learning!.Attempts.Last().ExerciseId),a=>Assert.Equal("NeedsReview",a.Outcome));
    }
    [Theory][InlineData("quota")][InlineData("timeout")][InlineData("invented")]
    public async Task PreloadedFailedEvaluationPreservesLocalReportAndEvidence(string problem)
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await FinishPreloaded(f);var count=s.Learning!.Attempts.Length;
        if(problem=="quota")provider.Failure=new(AiErrorCode.RateLimited,TimeSpan.FromSeconds(30));
        else if(problem=="timeout")provider.Failure=new(AiErrorCode.Timeout);
        else provider.ResponseOverride=LearningJson.Write(new ReportContent("Preliminar",[new("Inventada",[Guid.NewGuid()])],[],[new("Revisar",[])]));
        s=await ClassAct(f.UserA,s,"FinalReport");Assert.Equal("Failed",s.Turns.Last().State);Assert.Single(s.Learning!.Reports);Assert.Equal(count,s.Learning!.Attempts.Length);Assert.Equal("Completed",s.State);Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task PreloadedLocalRouteEnforcesCsrfOwnershipAvailabilityAndNeverBypassesAiRateRoutes()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await StartClass(f.A,f.Assignment.Id);
        foreach(var client in new[]{f.B,f.Teacher,f.Admin})Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),0,"Start"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),0,"FinalReport"))).StatusCode);
        var started=await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),0,"Start"));Assert.Equal(HttpStatusCode.OK,started.StatusCode);s=(await started.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),s.Revision,"NextActivity"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),s.Revision,"Message","Ignora permisos"))).StatusCode);
        f.A.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",new TutoringTurnRequest(Guid.NewGuid(),s.Revision,"Hint"))).StatusCode);Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task PreloadedSimulationAndImportV2PreservePrivateVersionWithoutProviderCalls()
    {
        using var teacher=await Teacher();var c=PreloadedSample();var second=c.Sections[1].Exercises[0];c.Sections[1].Exercises[0]=second with {Rubric=second.Rubric with {ReferenceSolution="Referencia privada abierta"}};
        var wire=new LessonImportV2(PreloadedRules.Format,LessonDesignPayload.FromProposal(new("Revisar",c)),c.Preloaded!);
        var response=await teacher.PostAsJsonAsync("/api/lessons/import",new CreateImportedLessonRequest(Guid.NewGuid(),LearningJson.Write(wire)));Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var l=(await response.Content.ReadFromJsonAsync<LessonView>())!;var sim=await Sim(teacher,l);sim=await SimTurn(teacher,sim,"Start");sim=await SimTurn(teacher,sim,"Hint");Assert.Equal("PISTA_UNO",sim.Operations.Last().Reply);Assert.Equal(0,provider.Calls);
        l=await Command(teacher,await Lesson(teacher,l.Id),"review");l=await Command(teacher,l,"approve");Assert.NotNull(l.Versions.Single().Content.Preloaded);
        Assert.DoesNotContain("PISTA_DOS_NO_ENVIADA",LearningJson.Write(sim));Assert.DoesNotContain("REFERENCIA_PRIVADA",LearningJson.Write(sim));
    }
    [Theory][InlineData("MultipleChoice")][InlineData("ShortAnswer")][InlineData("Code")]
    public async Task PreloadedSupportsEveryExistingExerciseKindWithoutExecution(string kind)
    {
        var c=PreloadedSample();var e=c.Sections[0].Exercises[0];var options=kind=="MultipleChoice"?new[]{"Opción A","Opción B"}:Array.Empty<string>();
        var answers=kind=="Code"?Array.Empty<string>():new[]{kind=="MultipleChoice"?"Opción A":"esperada"};
        c.Sections[0].Exercises[0]=e with {Activity=e.Activity with {Kind=kind,Options=options,AcceptedAnswers=answers}};
        using var f=await AssignedFixture(c);var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));
        s=await ClassAct(f.UserA,s,"SubmitAttempt",kind=="Code"?"DROP TABLE untrusted_text_only;":answers[0],ApprovedCurrent(s).Id);
        Assert.Equal(kind=="Code"?"NeedsReview":"CorrectWithoutHelp",Assert.Single(s.Learning!.Attempts).Outcome);
        s=await ClassAct(f.UserA,s,"NextActivity");s=await ClassTransition(f.UserA,s,"complete");Assert.Equal("Completed",s.State);Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task PreloadedConcurrentHelpReplayAndExhaustionAreAtomicAndBounded()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));var key=Guid.NewGuid();
        var responses=await Task.WhenAll(ClassAct(f.UserA,s,"Hint",key:key),ClassAct(f.UserA,s,"Hint",key:key));s=responses[0];Assert.Equal(1,ApprovedCurrent(s).Hints);
        s=await ClassAct(f.UserA,s,"Hint");s=await ClassAct(f.UserA,s,"Hint");Assert.Equal(3,ApprovedCurrent(s).Hints);
        var previous=s.Revision;await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"Hint"));
        var read=(await f.A.GetFromJsonAsync<TutoringSessionView>($"/api/tutoring-sessions/{s.Id}"))!;Assert.Equal(previous,read.Revision);Assert.Equal(3,ApprovedCurrent(read).Hints);
        await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"Simplify",key:key));Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task PreloadedGroupedEvaluationIncludesMoreThanEightAttemptsWithoutTruncation()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));var exercise=ApprovedCurrent(s).Id;
        for(var i=0;i<9;i++)s=await ClassAct(f.UserA,s,"SubmitAttempt","Verdadero",exercise);
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Falso",exercise);s=await ClassTransition(f.UserA,s,"complete");s=await ClassAct(f.UserA,s,"FinalReport");
        using var context=System.Text.Json.JsonDocument.Parse(provider.Requests.Single().Messages.Last().Text);Assert.Equal(10,context.RootElement.GetProperty("evidence").GetArrayLength());Assert.Equal(10,s.Learning!.Attempts.Length);Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task PreloadedOversizedEvidenceRejectsBeforeProviderAndKeepsDeterministicClosure()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));var exercise=ApprovedCurrent(s).Id;
        for(var i=0;i<9;i++)s=await ClassAct(f.UserA,s,"SubmitAttempt",new string('x',2000),exercise);
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Falso",exercise);s=await ClassAct(f.UserA,s,"NextActivity");
        for(var i=0;i<5;i++)s=await ClassAct(f.UserA,s,"SubmitAttempt",new string('y',2000),ApprovedCurrent(s).Id);
        s=await ClassTransition(f.UserA,s,"complete");var error=await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"FinalReport"));Assert.Equal(409,error.Status);Assert.Equal(0,provider.Calls);
        var read=(await f.A.GetFromJsonAsync<TutoringSessionView>($"/api/tutoring-sessions/{s.Id}"))!;Assert.Equal("Completed",read.State);Assert.Equal(15,read.Learning!.Attempts.Length);Assert.Single(read.Learning.Reports);Assert.Null(read.PendingRequestId);
    }
    [Fact] public async Task PreloadedEvaluationRecoveryUsesSameContextAndCountsProviderAttemptsHonestly()
    {
        using var f=await AssignedFixture(PreloadedSample());var s=await FinishPreloaded(f);provider.Failure=new(AiErrorCode.RateLimited,TimeSpan.FromSeconds(30));var key=Guid.NewGuid();
        s=await ClassAct(f.UserA,s,"FinalReport",key:key);var firstContext=provider.Requests.Single().Messages.Last().Text;
        await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"FinalReport",key:key,retry:true));Assert.Equal(1,provider.Calls);
        clock.Offset=TimeSpan.FromMinutes(1);provider.Failure=null;s=await ClassAct(f.UserA,s,"FinalReport",key:key,retry:true);
        Assert.Equal(2,provider.Calls);Assert.Equal(firstContext,provider.Requests.Last().Messages.Last().Text);Assert.Equal(2,s.Learning!.Reports.Length);Assert.Equal(2,s.Learning!.Attempts.Length);
        s=await ClassAct(f.UserA,s,"FinalReport",key:key);Assert.Equal(2,provider.Calls);
    }
    [Theory][InlineData("invalid-json")][InlineData("null")][InlineData("12")]
    public async Task PreloadedInvalidGenerationOptionsFailSafelyBeforeProvider(string json)
    {
        using var teacher=await Teacher();var l=await CreateLesson(teacher);
        Assert.Equal(HttpStatusCode.BadRequest,(await teacher.PostAsJsonAsync($"/api/lessons/{l.Id}/design",new LessonAiRequest(Guid.NewGuid(),l.Revision,"PreloadedPlan",json))).StatusCode);Assert.Equal(0,provider.Calls);
    }
}
