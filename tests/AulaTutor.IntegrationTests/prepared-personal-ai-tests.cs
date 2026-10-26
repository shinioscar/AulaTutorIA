using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Tutoring;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class PersonalAiTests
{
    private async Task<Guid> PreparedConnect(HttpClient client)
    {
        var start=await Start(client);using var returned=await Return(client,start);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete",new{})).StatusCode);
        var connection=(await View(client)).Connections.Single(c=>c.Provider=="OpenRouter");
        await Activate(client,connection.Id,"OpenRouter","qwen/qwen3.8-27b:free",false);return connection.Id;
    }
    private async Task<TutoringSessionView> PreparedCreate(HttpClient client,string experience="Tutoring")
    {
        var response=await client.PostAsJsonAsync("/api/tutoring-sessions/",new CreateTutoringRequest(Guid.NewGuid(),"Evidencia ficticia","Contrastar casos",Experience:experience,Modality:"Prepared"));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);return (await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;
    }
    private static async Task<TutoringSessionView> PreparedCall(HttpClient client,TutoringSessionView s,string action,string? answer=null,Guid? key=null)
    {
        var response=await client.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/{(action is "PrepareSession" or "FinalReport"?"messages":"cards")}",
            new TutoringTurnRequest(key??Guid.NewGuid(),s.Revision,action,answer,ExerciseId:action=="SubmitAttempt"?s.Prepared!.CurrentExerciseId:null));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;
    }
    private async Task SetPreparedReply(string experience,bool final=false,TutoringSessionView? s=null)
    {
        string context;
        if(!final) context=LearningJson.Write(new{experience,count=PreparedRules.Count(experience,"Short")});
        else
        {
            await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
            var script=LearningJson.Read<PreparedContent>((await db.PreparedLearning.SingleAsync(p=>p.SessionId==s!.Id)).ContentJson!);
            var exercises=await db.LearningExercises.Where(e=>e.SessionId==s!.Id&&e.PlanId==null).ToArrayAsync();
            context=LearningJson.Write(new{s!.Goal,script.Objectives,evidence=s.Learning!.Attempts.Select(a=>new{id=a.Id,objectiveId=script.Activities[exercises.Single(e=>e.Id==a.ExerciseId).Order].ObjectiveId})});
        }
        gateway.Text=(await new MockAiProvider().GenerateAsync(new(!final?"prepared_generate":experience=="Diagnostic"?"prepared_diagnostic_evaluate":"prepared_evaluate",[new("user",context)],new("text"),Guid.NewGuid().ToString()),default)).Text;
    }
    [Theory][InlineData("Tutoring")][InlineData("Diagnostic")]
    public async Task PreparedPersonalSnapshotUsesOnePhysicalGenerationZeroLocalAndOneGroupedEvaluation(string experience)
    {
        using var client=await User();var connection=await PreparedConnect(client);var s=await PreparedCreate(client,experience);
        Assert.Equal(0,gateway.Generations);await SetPreparedReply(experience);s=await PreparedCall(client,s,"PrepareSession");Assert.Equal("Ready",s.Prepared!.GenerationState);Assert.Equal(1,gateway.Generations);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync("/api/ai-connections/selection",new SelectAiRequest(null,null,false,false))).StatusCode);
        s=await PreparedCall(client,s,"Start");
        while(s.Prepared!.CurrentExerciseId is not null)
        {
            var e=s.Learning!.Exercises.Single(e=>e.Id==s.Prepared.CurrentExerciseId);
            if(experience=="Tutoring")s=await PreparedCall(client,s,"Hint");
            s=await PreparedCall(client,s,"SubmitAttempt",e.Kind=="OpenResponse"?"Explicación ficticia":"Falso");s=await PreparedCall(client,s,"NextActivity");
        }
        var closed=await client.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/complete",new TutoringTransitionRequest(s.Revision));Assert.Equal(HttpStatusCode.OK,closed.StatusCode);s=(await closed.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal(1,gateway.Generations);await SetPreparedReply(experience,true,s);var key=Guid.NewGuid();s=await PreparedCall(client,s,"FinalReport",key:key);
        Assert.Equal(2,gateway.Generations);Assert.Equal("OpenRouter",s.Learning!.Reports[0].ProviderId);if(experience=="Diagnostic")Assert.NotNull(s.Learning.Plan);
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync("/api/ai-connections/"+connection)).StatusCode);
        s=await PreparedCall(client,s,"FinalReport",key:key);Assert.Equal(2,gateway.Generations);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var reservations=await db.AiReservations.ToArrayAsync();Assert.Equal(2,reservations.Length);Assert.All(reservations,r=>{Assert.Equal(connection,r.ConnectionId);Assert.Equal("Succeeded",r.State);Assert.StartsWith("prepared_",r.Operation);});
    }
    [Fact] public async Task RevokedPersonalPreparedConnectionDiscardsLateScriptButRetainsRecoverableTurn()
    {
        using var client=await User();var connection=await PreparedConnect(client);var s=await PreparedCreate(client);await SetPreparedReply("Tutoring");gateway.Hold=true;
        var pending=PreparedCall(client,s,"PrepareSession");await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync("/api/ai-connections/"+connection)).StatusCode);gateway.Release.TrySetResult();s=await pending;
        Assert.Null(s.Prepared!.ContentHash);Assert.Empty(s.Learning!.Exercises);Assert.Equal("Authentication",s.Turns.Single().ErrorCode);Assert.Equal(1,gateway.Generations);
    }
}
