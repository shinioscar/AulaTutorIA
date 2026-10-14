using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using AulaTutor.Infrastructure.Tutoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaTutor.IntegrationTests;

public sealed partial class LearningTests
{
    private sealed class FixedActor(UserView user) : ICurrentActor
    {public Task<Actor> GetAsync()=>Task.FromResult(new Actor(user.Id,PilotOrganization.Id,user.Roles,user.TeacherApproval == "Approved"?TeacherApproval.Approved:TeacherApproval.NotRequested,true));}
    private async Task<T> Direct<T>(UserView user,Func<ITutoringService,ILearningService,Task<T>> work)
    {
        await using var scope=factory.Services.CreateAsyncScope();var actor=new FixedActor(user);
        var service=ActivatorUtilities.CreateInstance<TutoringService>(scope.ServiceProvider,actor);
        var learning=ActivatorUtilities.CreateInstance<LearningService>(scope.ServiceProvider,actor,service);
        return await work(service,learning);
    }
    private Task<TutoringSessionView> Act(UserView user,TutoringSessionView s,string action,string? text=null,Guid? exerciseId=null,Guid? key=null,bool retry=false)=>
        Direct(user,(t,_)=>t.SendAsync(s.Id,new(key??Guid.NewGuid(),s.Revision,action,text,retry,exerciseId),CancellationToken.None));
    private Task<TutoringSessionView> Close(UserView user,TutoringSessionView s)=>Direct(user,(t,_)=>t.TransitionAsync(s.Id,"complete",s.Revision,CancellationToken.None));
    private static ExerciseView Current(TutoringSessionView s)=>s.Learning!.Exercises.Last();
    private async Task<TutoringSessionView> Diagnose(UserView user,TutoringSessionView s)
    {
        for(var i=0;i<2;i++){s=await Act(user,s,"DiagnosticQuestion");s=await Act(user,s,"SubmitAttempt","Mi explicación y un ejemplo ficticio",Current(s).Id);}
        return await Act(user,s,"AnalyzeDiagnosis");
    }
    [Fact]
    public async Task DiagnosticPlanActivationGuidedQuestionsProgressAndReloadPreserveHistory()
    {
        var(client,user)=await Register();using var _c=client;var s=await Diagnose(user,await Create(client));
        Assert.False(s.Learning!.Diagnostic!.Preliminary);Assert.Equal(2,s.Learning.Attempts.Length);
        Assert.All(s.Learning.Attempts,a=>Assert.Equal("NeedsReview",a.Outcome));
        s=await Act(user,s,"GeneratePlan");Assert.Equal(3,s.Learning!.Plan!.Content.Objectives.Length);var history=s.Messages.Select(m=>m.Id).ToArray();
        var publicJson=System.Text.Json.JsonSerializer.Serialize(s);Assert.DoesNotContain("acceptedAnswers",publicJson,StringComparison.OrdinalIgnoreCase);
        s=await Act(user,s,"ActivatePlan");Assert.Equal("Active",s.Learning!.Plan!.State);Assert.NotNull(s.Learning.Plan.AcceptedAt);Assert.Equal(history,s.Messages.Take(history.Length).Select(m=>m.Id));
        var planId=s.Learning.Plan.Id;s=await Act(user,s,"Message","Tengo una pregunta libre");Assert.Equal(0,s.Learning!.Plan!.CurrentActivity);
        Assert.Contains("Conserva esta actividad",provider.Requests.Last().Messages.Last().Text);
        s=await Act(user,s,"Hint");var exercise=s.Learning!.Exercises.Single(e=>!e.IsDiagnostic&&e.Order==0);
        Assert.Equal(1,exercise.Hints);s=await Act(user,s,"SubmitAttempt","Falso",exercise.Id);Assert.Equal("CorrectWithHelp",s.Learning!.Attempts.Last().Outcome);
        s=await Act(user,s,"NextActivity");Assert.Equal(1,s.Learning!.Plan!.CurrentActivity);Assert.Equal("Worked",s.Learning.Plan.Content.Objectives[0].Progress);
        s=await Direct(user,(t,_)=>t.TransitionAsync(s.Id,"pause",s.Revision,CancellationToken.None));
        Assert.Equal(1,(await Get(client,s.Id)).Learning!.Plan!.CurrentActivity);
        s=await Direct(user,(t,_)=>t.TransitionAsync(s.Id,"resume",s.Revision,CancellationToken.None));Assert.Equal(planId,s.Learning!.Plan!.Id);
        for(var i=1;i<3;i++){exercise=s.Learning!.Exercises.Single(e=>!e.IsDiagnostic&&e.Order==i);s=await Act(user,s,"SubmitAttempt","Falso",exercise.Id);s=await Act(user,s,"NextActivity");}
        Assert.Equal("Completed",s.Learning!.Plan!.State);Assert.NotNull(s.Learning.Plan.CompletedAt);Assert.Equal(s.Messages.Length,(await Get(client,s.Id)).Messages.Length);
    }
    [Fact]
    public async Task EmptyDiagnosisIsPreliminaryAndPlansMayUseNoClaims()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"AnalyzeDiagnosis");
        Assert.True(s.Learning!.Diagnostic!.Preliminary);Assert.Empty(s.Learning.Diagnostic.Content.Strengths);Assert.Empty(s.Learning.Diagnostic.Content.Difficulties);
        s=await Act(user,s,"GeneratePlan");Assert.Equal(3,s.Learning!.Plan!.Content.Objectives.Length);Assert.All(s.Learning.Plan.Content.Objectives,o=>Assert.Empty(o.EvidenceIds));
    }
    [Fact]
    public async Task InvalidPlanIsDurableAndRetryDoesNotDuplicateEvidenceOrReplacePriorVersion()
    {
        var(client,user)=await Register();using var _c=client;var s=await Diagnose(user,await Create(client));var key=Guid.NewGuid();
        provider.ResponseOverride="{\"mainGoal\":\"Meta\",\"progression\":\"Ruta\",\"objectives\":[]}";
        s=await Act(user,s,"GeneratePlan",key:key);Assert.Equal("InvalidResponse",s.Turns.Last().ErrorCode);Assert.Null(s.Learning!.Plan);
        provider.ResponseOverride=null;s=await Act(user,s,"GeneratePlan",key:key,retry:true);Assert.Equal(1,s.Learning!.Plan!.Version);Assert.Equal(2,s.Learning.Attempts.Length);
        s=await Act(user,s,"GeneratePlan","Necesito ejemplos más sencillos");Assert.Equal(2,s.Learning!.Plan!.Version);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Equal(LearningPlanState.Superseded,(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().LearningPlans.OrderBy(p=>p.Version).FirstAsync()).State);
    }
    [Fact]
    public async Task DeterministicAttemptsHintsReportStatisticsAndReinforcementAreTraceable()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");
        s=await Act(user,s,"Message","Duda libre no es intento");Assert.Empty(s.Learning!.Attempts);
        s=await Act(user,s,"GenerateExercise");var exercise=Current(s);var key=Guid.NewGuid();s=await Act(user,s,"SubmitAttempt","Verdadero",exercise.Id,key);
        Assert.Equal("Incorrect",s.Learning!.Attempts.Last().Outcome);var calls=provider.Calls;
        using(var context=System.Text.Json.JsonDocument.Parse(provider.Requests.Last().Messages.Last().Text))
        {
            var evidence=context.RootElement.GetProperty("evidence")[0];
            Assert.Equal("Incorrect",evidence.GetProperty("outcome").GetString());
            Assert.Contains("No coincide",evidence.GetProperty("feedback").GetString());
            Assert.False(evidence.TryGetProperty("acceptedAnswers",out _));
        }
        s=await Act(user,s,"SubmitAttempt","Verdadero",exercise.Id,key);Assert.Equal(calls,provider.Calls);Assert.Single(s.Learning!.Attempts);
        s=await Act(user,s,"Hint");s=await Act(user,s,"SubmitAttempt","Falso",exercise.Id);Assert.Equal("CorrectWithHelp",s.Learning!.Attempts.Last().Outcome);
        s=await Close(user,s);calls=provider.Calls;Assert.NotNull(s.Summary);Assert.Equal(calls,provider.Calls);
        s=await Act(user,s,"FinalReport");var report=s.Learning!.Reports.Single();Assert.Equal(2,report.Statistics.Attempts);Assert.Equal(1,report.Statistics.CorrectWithHelp);Assert.Equal(1,report.Statistics.Incorrect);Assert.Equal(1,report.Statistics.Hints);
        Assert.All(report.Content.Observations.SelectMany(o=>o.EvidenceIds),id=>Assert.Contains(s.Learning.Attempts,a=>a.Id==id));Assert.True(s.Learning.Recommendations.Single().IsGeneral);
        var recommendation=s.Learning.Recommendations.Single();var creationKey=Guid.NewGuid();
        var next=await Direct(user,(_,l)=>l.ReinforceAsync(s.Id,recommendation.Id,new(creationKey),CancellationToken.None));
        var replay=await Direct(user,(_,l)=>l.ReinforceAsync(s.Id,recommendation.Id,new(creationKey),CancellationToken.None));Assert.Equal(next.Id,replay.Id);Assert.Empty(next.Messages);
        await using var scope=factory.Services.CreateAsyncScope();var stored=await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().TutoringSessions.SingleAsync(x=>x.Id==next.Id);Assert.Equal(report.Id,stored.SourceReportId);Assert.Equal(recommendation.Id,stored.SourceRecommendationId);
    }
    [Fact]
    public async Task OpenCodeAttemptsRemainUnverifiedAndProviderCannotOverrideOutcome()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");
        provider.ResponseOverride=LearningJson.Write(new ExerciseProposal("Explica tu código", "Code",[],[],"Claridad y razonamiento, sin ejecución."));
        s=await Act(user,s,"GenerateExercise");provider.ResponseOverride=LearningJson.Write(new LearningOrchestrator.FeedbackContent("El modelo sugiere que la respuesta es correcta."));
        s=await Act(user,s,"SubmitAttempt","DROP TABLE Users;",Current(s).Id);Assert.Equal("NeedsReview",s.Learning!.Attempts.Single().Outcome);Assert.Equal("Unverified",s.Learning.Attempts.Single().ValidationType);
    }
    [Theory]
    [InlineData(AiErrorCode.RateLimited)]
    [InlineData(AiErrorCode.Timeout)]
    [InlineData(AiErrorCode.InvalidResponse)]
    public async Task LearningProviderErrorsKeepAttemptAndLocalSummaryWithExplicitRetry(AiErrorCode failure)
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"DiagnosticQuestion");var key=Guid.NewGuid();provider.Failure=new AiProviderException(failure,failure==AiErrorCode.RateLimited?TimeSpan.FromSeconds(20):null);
        s=await Act(user,s,"SubmitAttempt","Mi respuesta",Current(s).Id,key);Assert.Single(s.Learning!.Attempts);Assert.Equal(failure.ToString(),s.Turns.Last().ErrorCode);
        if(failure==AiErrorCode.RateLimited){Assert.False(s.Turns.Last().CanRetry);clock.Offset=TimeSpan.FromSeconds(21);}
        provider.Failure=null;s=await Act(user,s,"SubmitAttempt","Mi respuesta",Current(s).Id,key,true);Assert.Single(s.Learning!.Attempts);Assert.Equal(2,s.Turns.Last().Attempts);
        s=await Close(user,s);provider.Failure=new AiProviderException(failure);s=await Act(user,s,"FinalReport");Assert.Empty(s.Learning!.Reports);Assert.NotNull(s.Summary);Assert.Single(s.Learning.Attempts);
    }
    [Fact]
    public async Task ConcurrentPlanCancelAndExpiredLeaseFenceLateVersions()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"AnalyzeDiagnosis");provider.Hold=true;var key=Guid.NewGuid();
        // The provider Started flag also records the earlier call; observe durable pending instead.
        var first=Act(user,s,"GeneratePlan",key:key);
        for(var i=0;i<100;i++){var read=await Get(client,s.Id);if(read.PendingRequestId==key){s=read;break;}await Task.Delay(20);}
        Assert.Equal(key,s.PendingRequestId);var duplicate=await Act(user,s,"GeneratePlan",key:key);Assert.Equal(key,duplicate.PendingRequestId);
        s=await Direct(user,(t,_)=>t.CancelAsync(s.Id,key,s.Revision,CancellationToken.None));provider.Hold=false;
        s=await Act(user,s,"GeneratePlan");provider.Release.TrySetResult();await first;
        s=await Get(client,s.Id);Assert.Single(s.Learning!.Plan!.Content.Objectives,o=>o.Title==s.Learning.Plan.Content.Objectives[0].Title);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().LearningPlans.CountAsync());
    }
    [Fact]
    public async Task NewEvidenceMarksReportOutdatedAndRegenerationDoesNotInventCounts()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");s=await Act(user,s,"GenerateExercise");s=await Act(user,s,"SubmitAttempt","Falso",Current(s).Id);s=await Act(user,s,"FinalReport");
        Assert.False(s.Learning!.Reports[0].Outdated);s=await Act(user,s,"SubmitAttempt","Verdadero",Current(s).Id);Assert.True(s.Learning!.Reports[0].Outdated);
        s=await Act(user,s,"FinalReport");Assert.Equal(2,s.Learning!.Reports.Length);Assert.Equal(2,s.Learning.Reports[0].Statistics.Attempts);Assert.Equal(1,s.Learning.Reports[0].Statistics.CorrectWithoutHelp);
    }
    [Fact]
    public async Task PlanStateAndIncompatibleReferencesAreRejectedBeforeCallingProvider()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");var calls=provider.Calls;
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"ActivatePlan"))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"GeneratePlan"))).Status);
        Assert.Equal(404,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"SubmitAttempt","Falso",Guid.NewGuid()))).Status);Assert.Equal(calls,provider.Calls);
        s=await Diagnose(user,s);s=await Act(user,s,"GeneratePlan");s=await Act(user,s,"ActivatePlan");calls=provider.Calls;
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"NextActivity"))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"GeneratePlan","Cambiar ruta"))).Status);Assert.Equal(calls,provider.Calls);
    }
    [Fact]
    public async Task SharingRequiresConsentRelationshipAndVersionRevocationBlocksNewReads()
    {
        var(a,student)=await Register();using var _a=a;var(b,_)=await Register();using var _b=b;var(x,xUser)=await Register("Teacher");using var _x=x;var(y,yUser)=await Register("Teacher");using var _y=y;using var admin=await Login("admin@example.invalid");
        foreach(var teacher in new[]{xUser,yUser})Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/users/{teacher.Id}/teacher-decision",new TeacherDecisionRequest(true))).StatusCode);
        using var teacherX=await Login(xUser.Email);using var teacherY=await Login(yUser.Email);
        var group=(await(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Relación académica"))).Content.ReadFromJsonAsync<GroupView>())!;
        await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(student.Id,"Student"));await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(xUser.Id,"Teacher"));
        var s=await Act(student,await Create(a),"AnalyzeDiagnosis");s=await Close(student,s);s=await Act(student,s,"FinalReport");var report=s.Learning!.Reports.Single();
        var recipients=(await a.GetFromJsonAsync<ShareRecipient[]>($"/api/tutoring-sessions/{s.Id}/share-recipients"))!;Assert.Single(recipients);Assert.Equal(xUser.Id,recipients[0].Id);
        var path=$"/api/tutoring-sessions/{s.Id}/shares";var request=new ShareReportRequest(Guid.NewGuid(),report.Id,xUser.Id,true);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync(path,request with{Consent=false})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.PostAsJsonAsync(path,request with{RecipientId=yUser.Id})).StatusCode);
        var response=await a.PostAsJsonAsync(path,request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var share=(await response.Content.ReadFromJsonAsync<ShareView>())!;
        Assert.Equal(share.Id,(await(await a.PostAsJsonAsync(path,request)).Content.ReadFromJsonAsync<ShareView>())!.Id);
        Assert.Equal(HttpStatusCode.OK,(await teacherX.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await teacherX.GetAsync($"/api/tutoring-sessions/{s.Id}")).StatusCode);
        foreach(var other in new[]{teacherY,b,admin})Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.PostAsJsonAsync(path,request)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await a.DeleteAsync(path+"/"+share.Id)).StatusCode);await Csrf(a);
        Assert.Equal(HttpStatusCode.NoContent,(await a.DeleteAsync(path+"/"+share.Id)).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await teacherX.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
        var renewed=await a.PostAsJsonAsync(path,request with{RequestId=Guid.NewGuid()});Assert.Equal(HttpStatusCode.OK,renewed.StatusCode);
        await admin.DeleteAsync($"/api/groups/{group.Id}/members/{xUser.Id}/Teacher");Assert.Equal(HttpStatusCode.NotFound,(await teacherX.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
    }
    [Fact]
    public async Task HttpLearningActionsRequireCsrfOwnerAndDoNotLeakSolutions()
    {
        var(a,student)=await Register();using var _a=a;var(b,_)=await Register();using var _b=b;var s=await Create(a);var req=Turn(s,"DiagnosticQuestion");
        Assert.Equal(HttpStatusCode.NotFound,(await b.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/messages",req)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync($"/api/tutoring-sessions/{s.Id}/messages",req)).StatusCode);await Csrf(a);
        s=await Send(a,s,req);Assert.Single(s.Learning!.Exercises);Assert.Equal(1,provider.Calls);
        var response=await a.GetStringAsync($"/api/tutoring-sessions/{s.Id}");Assert.DoesNotContain("acceptedAnswers",response,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task ReportReplayAndFreshKeyCannotDuplicateCurrentVersion()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"AnalyzeDiagnosis");s=await Close(user,s);var key=Guid.NewGuid();
        s=await Act(user,s,"FinalReport",key:key);var calls=provider.Calls;s=await Act(user,s,"FinalReport",key:key);
        Assert.Equal(calls,provider.Calls);Assert.Single(s.Learning!.Reports);
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"FinalReport"))).Status);Assert.Equal(calls,provider.Calls);
    }
    [Fact]
    public async Task DiagnosticLimitCanChangeWithoutFixedQuestionRequirement()
    {
        factory.Services.GetRequiredService<IOptions<LearningPolicy>>().Value.MaxDiagnosticQuestions=1;
        factory.Services.GetRequiredService<IOptions<LearningPolicy>>().Value.MinimumEvidence=1;
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"DiagnosticQuestion");s=await Act(user,s,"SubmitAttempt","Una respuesta",Current(s).Id);
        var calls=provider.Calls;Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>Act(user,s,"DiagnosticQuestion"))).Status);Assert.Equal(calls,provider.Calls);
        s=await Act(user,s,"AnalyzeDiagnosis");Assert.False(s.Learning!.Diagnostic!.Preliminary);Assert.Equal(1,s.Learning.DiagnosticQuestionLimit);
    }
    [Fact]
    public async Task RevokedStudentCannotPersistAPlanAfterGenerationStarted()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"AnalyzeDiagnosis");provider.Hold=true;
        var key=Guid.NewGuid();var call=Act(user,s,"GeneratePlan",key:key);
        for(var i=0;i<100;i++){if((await Get(client,s.Id)).PendingRequestId==key)break;await Task.Delay(20);}
        using var admin=await Login("admin@example.invalid");Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/status",new AccountStatusRequest(false))).StatusCode);
        provider.Release.TrySetResult();Assert.Equal(401,(await Assert.ThrowsAsync<AppException>(()=>call)).Status);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().LearningPlans.ToArrayAsync());
    }
    [Fact]
    public async Task InvalidEvidenceReferenceOrInventedStatisticsDoNotCreateAReport()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"AnalyzeDiagnosis");s=await Close(user,s);
        provider.ResponseOverride=LearningJson.Write(new ReportContent("Resumen",[new("Afirmación sin fundamento",[Guid.NewGuid()])],[],[new("Practicar",[])]));
        s=await Act(user,s,"FinalReport");Assert.Equal("InvalidResponse",s.Turns.Last().ErrorCode);Assert.Empty(s.Learning!.Reports);Assert.NotNull(s.Summary);
    }
    [Fact]
    public async Task SimplificationIsHelpAndHintsRemainSeparatelyCounted()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");s=await Act(user,s,"GenerateExercise");
        s=await Act(user,s,"Simplify");Assert.Equal(1,Current(s).Simplifications);Assert.Equal(0,Current(s).Hints);
        s=await Act(user,s,"SubmitAttempt","Falso",Current(s).Id);Assert.Equal("CorrectWithHelp",s.Learning!.Attempts.Single().Outcome);Assert.Equal(1,s.Learning.Attempts.Single().SimplificationsUsed);Assert.Equal(0,s.Learning.Attempts.Single().HintsUsed);
    }
    [Fact]
    public async Task FinalReportRemainsAvailableAtHistoryLimitWithoutAppendingConversation()
    {
        var(client,user)=await Register();using var _c=client;var s=await Act(user,await Create(client),"Start");
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();var stored=await db.TutoringSessions.SingleAsync();
            for(var i=0;i<49;i++){var key=Guid.NewGuid();db.TutoringTurns.Add(new TutoringTurn{SessionId=s.Id,RequestId=key,Action=TutoringAction.Message,State=TutoringTurnState.Succeeded,Attempts=1});foreach(var role in new[]{TutoringMessageRole.Student,TutoringMessageRole.Tutor})db.TutoringMessages.Add(new TutoringMessage{SessionId=s.Id,RequestId=key,Sequence=stored.NextSequence++,Role=role,Text="Historial ficticio de prueba"});}
            await db.SaveChangesAsync();
        }
        s=await Get(client,s.Id);Assert.Equal(100,s.Messages.Length);s=await Close(user,s);s=await Act(user,s,"FinalReport");
        Assert.Single(s.Learning!.Reports);Assert.Equal(100,s.Messages.Length);Assert.Equal("Completed",s.State);
    }
}
