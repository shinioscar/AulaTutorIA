using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class LearningTests
{
    private static CreateTutoringRequest PreparedRequest(string experience = "Tutoring", string length = "Short") =>
        new(Guid.NewGuid(), "Evidencia académica", "Contrastar varios ejemplos", Experience: experience, Modality: "Prepared", Length: length);
    private static ExerciseView PreparedCurrent(TutoringSessionView s) => s.Learning!.Exercises.Single(e => e.Id == s.Prepared!.CurrentExerciseId);
    private async Task<TutoringSessionView> FinishPrepared(UserView user, TutoringSessionView s)
    {
        s = await Act(user, s, "PrepareSession"); Assert.Equal("Ready", s.Prepared!.GenerationState);
        s = await Act(user, s, "Start");
        while (s.Prepared!.CurrentExerciseId is not null)
        {
            var e = PreparedCurrent(s);
            s = await Act(user, s, "SubmitAttempt", e.Kind is "OpenResponse" or "Code" ? "Una explicación ficticia; no ejecutar." : "Falso", e.Id);
            s = await Act(user, s, "NextActivity");
        }
        return await Close(user, s);
    }
    [Theory][InlineData("Tutoring")][InlineData("Diagnostic")]
    public async Task PreparedGenerationLocalWorkAndFinalEvaluationHaveOneZeroOneCalls(string experience)
    {
        var (client,user) = await Register(); using var c = client;
        var request = PreparedRequest(experience); var s = await Create(client, request); Assert.Equal(0,provider.Calls);
        Assert.Equal(s.Id,(await Create(client,request)).Id); Assert.Equal("Requested",s.Prepared!.GenerationState);
        s = await Act(user,s,"PrepareSession"); Assert.Equal(1,provider.Calls); Assert.Empty(s.Learning!.Exercises); Assert.Equal("Created",s.State);
        var generationKey = s.Turns.Single().RequestId;
        s = await Act(user,s,"PrepareSession",key:generationKey);Assert.Equal(1,provider.Calls);
        s = await Act(user,s,"Start");var first=PreparedCurrent(s);
        Assert.DoesNotContain("REFERENCIA_PRIVADA",LearningJson.Write(s));Assert.DoesNotContain("acceptedAnswers",LearningJson.Write(s));
        Assert.DoesNotContain("Piensa en qué falta",LearningJson.Write(s)); Assert.Single(s.Learning!.Exercises);
        if(experience=="Tutoring")
        {
            var key=Guid.NewGuid();s=await Act(user,s,"Hint",key:key);s=await Act(user,s,"Hint",key:key);
            Assert.Equal(1,PreparedCurrent(s).Hints);Assert.Contains("Compara un caso",LearningJson.Write(s));Assert.DoesNotContain("Piensa en qué falta",LearningJson.Write(s));
            s=await Act(user,s,"Simplify");
        }
        else await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"Hint"));
        s=await Direct(user,(t,_)=>t.TransitionAsync(s.Id,"pause",s.Revision,default));s=await Get(client,s.Id);
        s=await Direct(user,(t,_)=>t.TransitionAsync(s.Id,"resume",s.Revision,default));
        while(s.Prepared!.CurrentExerciseId is not null)
        {
            var e=PreparedCurrent(s);s=await Act(user,s,"SubmitAttempt",e.Kind=="OpenResponse"?"Otra evidencia ficticia":"Falso",e.Id);
            if(experience=="Diagnostic")
            {
                Assert.All(s.Learning!.Attempts,a=>{Assert.Equal("Recorded",a.Outcome);Assert.Equal("Deferred",a.ValidationType);Assert.DoesNotContain("Coincide",a.Feedback);});
                await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"SubmitAttempt","Verdadero",e.Id));
            }
            s=await Act(user,s,"NextActivity");
        }
        provider.Failure=new(AiErrorCode.Unavailable);s=await Close(user,s);Assert.Equal(1,provider.Calls);
        Assert.Single(s.Learning!.Reports);Assert.Equal("Completed",s.State);Assert.Contains(s.Learning.Attempts,a=>a.Outcome=="NeedsReview");
        provider.Failure=null;var finalKey=Guid.NewGuid();s=await Act(user,s,"FinalReport",key:finalKey);Assert.Equal(2,provider.Calls);
        Assert.Equal(2,s.Learning!.Reports.Length);Assert.Equal("prepared-evaluation-v1",s.Learning.Reports[0].PromptVersion);
        if(experience=="Diagnostic"){Assert.NotNull(s.Learning.Plan);Assert.NotNull(s.Learning.Diagnostic);Assert.Equal(3,s.Learning.Plan.Content.Objectives.Length);}
        s=await Act(user,s,"FinalReport",key:finalKey);Assert.Equal(2,provider.Calls);
        await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"FinalReport"));Assert.Equal(2,provider.Calls);
        var context=provider.Requests.Last().Messages.Last().Text;
        Assert.DoesNotContain("REFERENCIA_PRIVADA",context);Assert.DoesNotContain("acceptedAnswers",context);Assert.DoesNotContain(user.Email,context);
        using var json=JsonDocument.Parse(context);Assert.Equal(s.Learning!.Attempts.Length,json.RootElement.GetProperty("evidence").GetArrayLength());
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var stored=await db.PreparedLearning.SingleAsync(p=>p.SessionId==s.Id);Assert.Equal(PreparedRules.Hash(stored.ContentJson!),stored.ContentHash);Assert.NotNull(stored.EvaluationEvidenceHash);Assert.NotNull(stored.EvaluationJson);
    }
    [Theory][InlineData("Tutoring")][InlineData("Diagnostic")]
    public async Task PreparedPrivateResourcesCannotBeReadOrMutatedByAnotherStudentTeacherOrAdmin(string experience)
    {
        var(client,user)=await Register();using var a=client;var s=await Create(a,PreparedRequest(experience));
        var(other,_)=await Register();using var b=other;using var admin=await Login("admin@example.invalid");var(pending,_p)=await Register("Teacher");using var teacher=pending;
        foreach(var reader in new[]{b,admin,teacher})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await reader.GetAsync($"/api/tutoring-sessions/{s.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await reader.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/cards",Turn(s))).StatusCode);
        }
        a.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/messages",Turn(s,"PrepareSession"))).StatusCode);Assert.Equal(0,provider.Calls);
    }
    [Theory][InlineData(AiErrorCode.RateLimited)][InlineData(AiErrorCode.Timeout)][InlineData(AiErrorCode.InvalidResponse)]
    public async Task PreparedFinalFailureKeepsEvidenceClosedStateAndLocalReportWithoutReadingRetries(AiErrorCode failure)
    {
        var(client,user)=await Register();using var a=client;var s=await FinishPrepared(user,await Create(a,PreparedRequest("Diagnostic")));
        var attempts=s.Learning!.Attempts.Select(a=>a.Id).ToArray();provider.Failure=new(failure);var key=Guid.NewGuid();s=await Act(user,s,"FinalReport",key:key);
        Assert.Equal(2,provider.Calls);Assert.Single(s.Learning!.Reports);Assert.Equal("Completed",s.State);Assert.Null(s.Learning.Plan);
        s=await Get(a,s.Id);Assert.Equal(2,provider.Calls);Assert.Equal(attempts,s.Learning!.Attempts.Select(a=>a.Id));
        provider.Failure=null;s=await Act(user,s,"FinalReport",key:key,retry:true);Assert.Equal(3,provider.Calls);Assert.NotNull(s.Learning!.Plan);
    }
    [Fact] public async Task PreparedConcurrentGenerationHasOneWinnerAndReplaySurvivesNewServiceScopes()
    {
        var(client,user)=await Register();using var a=client;var s=await Create(a,PreparedRequest());var key=Guid.NewGuid();provider.Hold=true;
        var pending=Act(user,s,"PrepareSession",key:key);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var replay=await Act(user,s,"PrepareSession",key:key);Assert.Equal("Generating",replay.Prepared!.GenerationState);Assert.Equal(1,provider.Calls);
        await Assert.ThrowsAsync<AppException>(()=>Act(user,replay,"PrepareSession"));
        provider.Release.TrySetResult();s=await pending;Assert.Equal("Ready",s.Prepared!.GenerationState);
        s=await Get(a,s.Id);Assert.Equal(1,provider.Calls);Assert.Equal("Ready",s.Prepared!.GenerationState);
    }
    [Fact] public async Task PreparedCancellationFencesLateScriptAndRequiresExplicitNewGeneration()
    {
        var(client,user)=await Register();using var a=client;var s=await Create(a,PreparedRequest());var key=Guid.NewGuid();provider.Hold=true;
        var pending=Act(user,s,"PrepareSession",key:key);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));s=await Get(a,s.Id);
        s=await Direct(user,(t,_)=>t.CancelAsync(s.Id,key,s.Revision,default));provider.Release.TrySetResult();await pending;
        s=await Get(a,s.Id);Assert.Null(s.Prepared!.ContentHash);Assert.Empty(s.Learning!.Exercises);Assert.Equal(1,provider.Calls);
        provider.Hold=false;s=await Act(user,s,"PrepareSession");Assert.Equal(2,provider.Calls);Assert.Equal("Ready",s.Prepared!.GenerationState);
    }
    [Fact] public async Task PreparedServerControlsPositionAndDiagnosticDoesNotRequireCorrectAnswers()
    {
        var(client,user)=await Register();using var a=client;var s=await Act(user,await Create(a,PreparedRequest("Diagnostic")),"PrepareSession");
        await Assert.ThrowsAsync<AppException>(()=>Close(user,s));s=await Act(user,s,"Start");
        await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"NextActivity"));await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"SubmitAttempt","Falso",Guid.NewGuid()));
        var first=PreparedCurrent(s);s=await Act(user,s,"SubmitAttempt","Verdadero",first.Id);Assert.Equal("Recorded",s.Learning!.Attempts.Single().Outcome);
        s=await Act(user,s,"NextActivity");await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"SubmitAttempt","Falso",first.Id));Assert.Equal(1,provider.Calls);
    }
    [Theory][InlineData("Prepared")][InlineData("Conversational")]
    public async Task PreparedDiagnosticPlanAcceptanceCreatesSeparatePrivateGoalSessionWithoutAnotherCall(string modality)
    {
        var(client,user)=await Register();using var a=client;var s=await FinishPrepared(user,await Create(a,PreparedRequest("Diagnostic")));s=await Act(user,s,"FinalReport");
        var request=new StartPreparedPlanRequest(Guid.NewGuid(),s.Learning!.Plan!.Id,0,modality);
        var child=await Direct(user,(t,_)=>t.StartPreparedPlanAsync(s.Id,request,default));
        var replay=await Direct(user,(t,_)=>t.StartPreparedPlanAsync(s.Id,request,default));Assert.Equal(child.Id,replay.Id);Assert.NotEqual(s.Id,child.Id);
        Assert.Equal("Created",child.State);Assert.Equal(2,provider.Calls);Assert.Equal("Completed",(await Get(a,s.Id)).State);
        Assert.Equal(s.Learning.Plan.Content.Objectives[0].Title,child.Goal);Assert.Equal(modality,child.Modality);
        Assert.NotNull((await Get(a,s.Id)).Learning!.Plan!.AcceptedAt);
    }
    [Fact] public async Task PreparedSnapshotAndExerciseSourceAreImmutableInPostgreSql()
    {
        var(client,user)=await Register();using var a=client;var s=await Act(user,await Create(a,PreparedRequest()),"PrepareSession");
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PreparedLearning\" SET \"ContentJson\"='{{}}' WHERE \"SessionId\"={s.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LearningExercises\" SET \"Prompt\"='ALTERADO' WHERE \"SessionId\"={s.Id}"));
    }
}
