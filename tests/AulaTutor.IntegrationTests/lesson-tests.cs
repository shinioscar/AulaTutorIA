using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Lessons;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private static LessonContent Content() => new("Clase SQL", "SQL", "Descripción", "Conocer tablas", "Básico", 45, "Explicar, practicar y ofrecer pistas", "",
        [new("o1", "Distinguir evidencia y dominio")], [new("s1", "Introducción", "Contenido original", [new("e1", "o1", true,
            new("¿Un acierto demuestra dominio?", "TrueFalse", ["Verdadero", "Falso"], ["Falso"], "Distinguir evidencia"), new(["Razona la respuesta"], "REFERENCIA_PRIVADA", "Invitar a comparar dos ejemplos antes de la solución"))]),
        new("s2", "Reflexión", "Contenido segundo", [new("e2", "o1", false, new("Escribe un ejemplo", "OpenResponse", [], [], "Pertinencia"), new(["Ejemplo pertinente"], "", "Dar un contexto alternativo"))])]);
    private async Task<HttpClient> Teacher()
    {
        var (pending,user)=await Register("Teacher");pending.Dispose();using var admin=await Login("admin@example.invalid");
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/teacher-decision",new TeacherDecisionRequest(true))).StatusCode);
        return await Login(user.Email);
    }
    private static async Task<LessonView> CreateLesson(HttpClient c,LessonContent? p=null,Guid? key=null)
    {var response=await c.PostAsJsonAsync("/api/lessons/",new CreateLessonRequest(key??Guid.NewGuid(),p??Content()));Assert.Equal(HttpStatusCode.Created,response.StatusCode);return(await response.Content.ReadFromJsonAsync<LessonView>())!;}
    private static async Task<LessonView> Lesson(HttpClient c,Guid id)=>(await c.GetFromJsonAsync<LessonView>($"/api/lessons/{id}"))!;
    private static async Task<LessonView> Command(HttpClient c,LessonView l,string action,Guid? target=null,Guid? key=null)
    {var response=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/{action}",new LessonCommandRequest(key??Guid.NewGuid(),l.Revision,target));Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(await response.Content.ReadFromJsonAsync<LessonView>())!;}
    private static async Task<LessonView> Design(HttpClient c,LessonView l,LessonAiRequest? r=null)
    {var response=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/design",r??new(Guid.NewGuid(),l.Revision,"Plan","Genera una clase básica"));Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(await response.Content.ReadFromJsonAsync<LessonView>())!;}
    private static async Task<LessonSimulationView> Sim(HttpClient c,LessonView l,Guid? key=null)
    {var response=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/simulations",new LessonCommandRequest(key??Guid.NewGuid(),l.Revision));Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(await response.Content.ReadFromJsonAsync<LessonSimulationView>())!;}
    private static async Task<LessonSimulationView> SimTurn(HttpClient c,LessonSimulationView s,string action,string? text=null,Guid? key=null,bool retry=false)
    {var response=await c.PostAsJsonAsync($"/api/lessons/{s.LessonId}/simulations/{s.Id}/messages",new LessonAiRequest(key??Guid.NewGuid(),s.Revision,action,text,retry));Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(await response.Content.ReadFromJsonAsync<LessonSimulationView>())!;}

    [Fact] public async Task CreateDraftIsIdempotentAndHasNoAiCall()
    {using var c=await Teacher();var key=Guid.NewGuid();var p=Content() with {Objectives=[],Sections=[],Methodology="",Level="",DurationMinutes=0};var l=await CreateLesson(c,p,key);var replay=await CreateLesson(c,p,key);Assert.Equal(l.Id,replay.Id);Assert.NotEmpty(l.Validation.Errors);Assert.Equal(0,provider.Calls);Assert.Single((await c.GetFromJsonAsync<LessonItem[]>("/api/lessons/"))!);Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/lessons/",new CreateLessonRequest(key,Content()))).StatusCode);}
    [Fact] public async Task EditsReorderSectionsAndPreserveHistoryWithConflicts()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var content=l.Content with {Sections=l.Content.Sections.Reverse().ToArray(),Title="Editada"};
        var response=await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,content));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var saved=(await response.Content.ReadFromJsonAsync<LessonView>())!;Assert.Equal("s2",saved.Content.Sections[0].Id);Assert.Equal(1,saved.DraftRevision);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content))).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();Assert.Equal(2,await db.LessonDraftRevisions.CountAsync());Assert.Equal("s1",LearningJson.Read<LessonContent>((await db.LessonDraftRevisions.SingleAsync(d=>d.Revision==0)).ContentJson).Sections[0].Id);
    }
    [Fact] public async Task ProposalsRequireExplicitApplicationAndCanBeDiscarded()
    {using var c=await Teacher();var l=await CreateLesson(c);l=await Design(c,l);Assert.Equal("Contenido original",l.Content.Sections[0].Content);Assert.Single(l.Proposals);var applied=await Command(c,l,"apply-proposal",l.Proposals[0].Id);Assert.Contains("Mock",applied.Content.Sections[0].Content);Assert.Equal("Applied",applied.Proposals[0].State);var second=await Design(c,applied);var discarded=await Command(c,second,"discard-proposal",second.Proposals.Last().Id);Assert.Equal(applied.DraftRevision,discarded.DraftRevision);Assert.Equal("Discarded",discarded.Proposals.Last().State);}
    [Fact] public async Task InvalidGeneratedPlanFailsSafelyAndRetryUsesCapturedContext()
    {using var c=await Teacher();var l=await CreateLesson(c);provider.ResponseOverride="{\"explanation\":\"bad\",\"content\":null}";var key=Guid.NewGuid();l=await Design(c,l,new(key,l.Revision,"Plan","Plan"));Assert.Empty(l.Proposals);Assert.Equal("InvalidResponse",l.Operations.Single().ErrorCode);Assert.Equal("Contenido original",l.Content.Sections[0].Content);provider.ResponseOverride=null;l=await Design(c,l,new(key,l.Revision,"Plan","Plan",true));Assert.Single(l.Proposals);Assert.Equal(2,provider.Calls);Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(provider.Requests[0].Messages.Last().Text),System.Text.Json.Nodes.JsonNode.Parse(provider.Requests[1].Messages.Last().Text)));}
    [Fact] public async Task LateProposalCannotOverwriteManualEdit()
    {
        using var c=await Teacher();var l=await CreateLesson(c);provider.Hold=true;var pending=Design(c,l);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var response=await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content with {Title="Edición posterior"}));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        provider.Release.TrySetResult();l=await pending;Assert.Equal("Edición posterior",l.Content.Title);Assert.True(l.Proposals.Single().Outdated);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/apply-proposal",new LessonCommandRequest(Guid.NewGuid(),l.Revision,l.Proposals.Single().Id))).StatusCode);
    }
    [Fact] public async Task SimulationsUseDraftSnapshotWithoutAcademicTablesOrPrivateSolutions()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var s=await Sim(c,l);Assert.Equal(0,provider.Calls);s=await SimTurn(c,s,"Start");s=await SimTurn(c,s,"Answer","Verdadero");Assert.Equal("Incorrect",s.Operations.Last().Outcome);s=await SimTurn(c,s,"Hint");s=await SimTurn(c,s,"Answer","Falso");Assert.Equal("CorrectWithHelp",s.Operations.Last().Outcome);s=await SimTurn(c,s,"Message","Tengo otra duda");Assert.Equal(0,s.SectionIndex);
        var json=await c.GetStringAsync($"/api/lessons/{l.Id}/simulations/{s.Id}");Assert.DoesNotContain("acceptedAnswers",json);Assert.DoesNotContain("referenceSolution",json);Assert.DoesNotContain("REFERENCIA_PRIVADA",json);Assert.All(provider.Requests,r=>Assert.DoesNotContain("REFERENCIA_PRIVADA",string.Join("",r.Messages.Select(m=>m.Text))));
        Assert.Contains(TutoringOrchestrator.SystemPolicy,provider.Requests.First().Messages.First().Text);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();Assert.Empty(await db.TutoringSessions.ToArrayAsync());Assert.Empty(await db.ExerciseAttempts.ToArrayAsync());Assert.Empty(await db.LearningReports.ToArrayAsync());Assert.Empty(await db.GroupMemberships.ToArrayAsync());
    }
    [Fact] public async Task SimulationRequiresAnswerAndKeepsSnapshotAcrossStopResumeAndEdit()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var s=await Sim(c,l);s=await SimTurn(c,s,"Start");
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/simulations/{s.Id}/messages",new LessonAiRequest(Guid.NewGuid(),s.Revision,"Next"))).StatusCode);
        var stopped=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/simulations/{s.Id}/stop",new LessonCommandRequest(Guid.NewGuid(),s.Revision));s=(await stopped.Content.ReadFromJsonAsync<LessonSimulationView>())!;Assert.Equal("Stopped",s.State);var calls=provider.Calls;
        l=await Lesson(c,l.Id);Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content with {Sections=[l.Content.Sections[1]]}))).StatusCode);
        var resumed=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/simulations/{s.Id}/resume",new LessonCommandRequest(Guid.NewGuid(),s.Revision));s=(await resumed.Content.ReadFromJsonAsync<LessonSimulationView>())!;Assert.Equal("Contenido original",s.SectionContent);Assert.Equal(calls,provider.Calls);
        s=await SimTurn(c,s,"Answer","Falso");s=await SimTurn(c,s,"Next");Assert.Equal("s2",Content().Sections[s.SectionIndex].Id);s=await SimTurn(c,s,"Answer","Ejemplo abierto");Assert.Equal("NeedsReview",s.Operations.Last().Outcome);
    }
    [Fact] public async Task ApprovalRequiresReviewAndStructureButNotSimulation()
    {
        using var c=await Teacher();var l=await CreateLesson(c,Content() with {Methodology=""});Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/approve",new LessonCommandRequest(Guid.NewGuid(),l.Revision))).StatusCode);
        l=await Command(c,l,"review");Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/approve",new LessonCommandRequest(Guid.NewGuid(),l.Revision))).StatusCode);
        var response=await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,Content()));l=(await response.Content.ReadFromJsonAsync<LessonView>())!;l=await Command(c,l,"review");l=await Command(c,l,"approve");Assert.Single(l.Versions);Assert.Empty(l.Simulations);Assert.Null(l.Versions.Single().PublishedAt);Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task PublicationIsExplicitAndOldVersionsSurviveNewDrafts()
    {
        using var c=await Teacher();var l=await Command(c,await CreateLesson(c),"review");l=await Command(c,l,"approve");var original=l.Versions.Single();
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content))).StatusCode);
        l=await Command(c,l,"publish",original.Id);Assert.NotNull(l.Versions.Single().PublishedAt);l=await Command(c,l,"new-draft");
        var response=await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content with {Title="Segunda versión"}));l=(await response.Content.ReadFromJsonAsync<LessonView>())!;l=await Command(c,l,"review");l=await Command(c,l,"approve");l=await Command(c,l,"publish",l.Versions.Last().Id);
        Assert.Equal(2,l.Versions.Length);Assert.Equal(original.ContentHash,l.Versions[0].ContentHash);Assert.Equal("Clase SQL",l.Versions[0].Content.Title);Assert.Equal("Segunda versión",l.Versions[1].Content.Title);
        Assert.Equal(HttpStatusCode.NotFound,(await c.PutAsJsonAsync($"/api/lessons/{l.Id}/versions/{original.Id}",Content())).StatusCode);
    }
    [Fact] public async Task PostgreSqlRejectsDirectVersionAndRevisionMutations()
    {
        using var c=await Teacher();var l=await Command(c,await CreateLesson(c),"review");l=await Command(c,l,"approve");await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var exception=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LessonVersions\" SET \"ContentJson\"='{{}}' WHERE \"Id\"={l.Versions[0].Id}"));Assert.Equal("P0001",exception.SqlState);
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"LessonDraftRevisions\" WHERE \"LessonId\"={l.Id}"));
        Assert.Equal(l.Versions[0].ContentHash,(await Lesson(c,l.Id)).Versions[0].ContentHash);
    }
    [Fact] public async Task ApprovalAndPublicationReplayAreSerialized()
    {
        using var c=await Teacher();var l=await Command(c,await CreateLesson(c),"review");var r=new LessonCommandRequest(Guid.NewGuid(),l.Revision);var responses=await Task.WhenAll(c.PostAsJsonAsync($"/api/lessons/{l.Id}/approve",r),c.PostAsJsonAsync($"/api/lessons/{l.Id}/approve",r));Assert.All(responses,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));l=await Lesson(c,l.Id);Assert.Single(l.Versions);
        r=new(Guid.NewGuid(),l.Revision,l.Versions[0].Id);responses=await Task.WhenAll(c.PostAsJsonAsync($"/api/lessons/{l.Id}/publish",r),c.PostAsJsonAsync($"/api/lessons/{l.Id}/publish",r));Assert.All(responses,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));Assert.Single((await Lesson(c,l.Id)).Versions);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/publish",r with {TargetId=Guid.NewGuid()})).StatusCode);
    }
    [Fact] public async Task AllLessonResourcesRequireOwnershipAndWritesRequireCsrf()
    {
        using var teacher=await Teacher();using var other=await Teacher();using var admin=await Login("admin@example.invalid");var(student,_)=await Register();using var studentClient=student;var(pending,_)=await Register("Teacher");using var pendingClient=pending;using var anonymous=TestHost.Client(factory);var l=await CreateLesson(teacher);var s=await Sim(teacher,l);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/lessons/")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/lessons/",new CreateLessonRequest(Guid.NewGuid(),Content()))).StatusCode);
        foreach(var client in new[]{other,student,admin,pending}) {Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/lessons/{l.Id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/lessons/{l.Id}/simulations/{s.Id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/lessons/{l.Id}/approve",new LessonCommandRequest(Guid.NewGuid(),l.Revision))).StatusCode);}
        foreach(var client in new[]{student,admin,pending})Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/lessons/",new CreateLessonRequest(Guid.NewGuid(),Content()))).StatusCode);
        teacher.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await teacher.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content))).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await teacher.PostAsJsonAsync($"/api/lessons/{l.Id}/publish",new LessonCommandRequest(Guid.NewGuid(),l.Revision))).StatusCode);
    }
    [Fact] public async Task ConcurrentGenerationReplayDoesNotDuplicateProviderCallsAndCancelFencesLateResponse()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var r=new LessonAiRequest(Guid.NewGuid(),l.Revision,"Plan","Diseñar");provider.Hold=true;var pending=Design(c,l,r);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));var replay=await c.PostAsJsonAsync($"/api/lessons/{l.Id}/design",r);Assert.Equal(HttpStatusCode.Accepted,replay.StatusCode);Assert.Equal(1,provider.Calls);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/design/{r.RequestId}/cancel",new{})).StatusCode);provider.Release.TrySetResult();l=await pending;Assert.Empty(l.Proposals);Assert.Equal("Cancelled",l.Operations.Single().State);
    }
    [Fact] public async Task ProviderFailureKeepsDraftAndModelChangesNeverFallback()
    {using var c=await Teacher();var l=await CreateLesson(c);provider.Failure=new(AiErrorCode.RateLimited,TimeSpan.FromSeconds(30));var key=Guid.NewGuid();l=await Design(c,l,new(key,l.Revision,"Plan","Diseñar"));Assert.Equal("RateLimited",l.Operations.Single().ErrorCode);Assert.NotNull(l.Operations.Single().RetryNotBefore);Assert.Equal(HttpStatusCode.TooManyRequests,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/design",new LessonAiRequest(key,l.Revision,"Plan","Diseñar",true))).StatusCode);clock.Offset=TimeSpan.FromMinutes(1);provider.Failure=null;provider.HealthModel="different";Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/lessons/{l.Id}/design",new LessonAiRequest(key,l.Revision,"Plan","Diseñar",true))).StatusCode);Assert.Equal(1,provider.Calls);Assert.Equal("Contenido original",l.Content.Sections[0].Content);}
    [Fact] public async Task RevokedTeacherCannotPersistAResponseAfterGeneration()
    {
        using var c=await Teacher();var me=(await c.GetFromJsonAsync<UserView>("/api/me"))!;var l=await CreateLesson(c);provider.Hold=true;var pending=c.PostAsJsonAsync($"/api/lessons/{l.Id}/design",new LessonAiRequest(Guid.NewGuid(),l.Revision,"Plan","Diseñar"));await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));using var admin=await Login("admin@example.invalid");Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/admin/users/{me.Id}/status",new AccountStatusRequest(false))).StatusCode);provider.Release.TrySetResult();Assert.Equal(HttpStatusCode.Unauthorized,(await pending).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();Assert.Empty(await db.LessonProposals.ToArrayAsync());Assert.Equal(LessonOperationState.Cancelled,(await db.LessonOperations.SingleAsync()).State);
    }
    [Fact] public async Task ExpiredLeaseNeedsExplicitRecoveryAndDropsOldResult()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var r=new LessonAiRequest(Guid.NewGuid(),l.Revision,"Plan","Diseñar");provider.Hold=true;var pending=Design(c,l,r);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));clock.Offset=TimeSpan.FromMinutes(5);l=await Lesson(c,l.Id);Assert.Equal("Interrupted",l.Operations.Single().ErrorCode);Assert.Equal(1,provider.Calls);provider.Hold=false;var recovered=await Design(c,l,r with {Retry=true});provider.Release.TrySetResult();await pending;Assert.Single((await Lesson(c,l.Id)).Proposals);Assert.Equal(2,provider.Calls);Assert.Equal("Succeeded",recovered.Operations.Single().State);
    }
    [Fact] public async Task TimeoutPreservesSimulationAnswerAndRetryDoesNotDuplicateIt()
    {
        using var c=await Teacher();var l=await CreateLesson(c);var s=await Sim(c,l);s=await SimTurn(c,s,"Start");
        await using(var scope=factory.Services.CreateAsyncScope())scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiProviderOptions>>().Value.TimeoutSeconds=1;
        provider.Hold=true;var key=Guid.NewGuid();s=await SimTurn(c,s,"Answer","Falso",key);Assert.Equal("Timeout",s.Operations.Last().ErrorCode);Assert.Equal("CorrectWithoutHelp",s.Operations.Last().Outcome);
        provider.Hold=false;s=await SimTurn(c,s,"Answer","Falso",key,true);Assert.Equal(2,s.Operations.Length);Assert.Equal("Succeeded",s.Operations.Last().State);Assert.Equal(2,s.Operations.Last().Attempts);
        var replay=await SimTurn(c,s,"Answer","Falso",key);Assert.Equal(s.Revision,replay.Revision);Assert.Equal(3,provider.Calls);
    }
    [Fact] public async Task SimplificationIsHelpAndFreeQuestionsNeverBecomeAnswers()
    {
        using var c=await Teacher();var s=await Sim(c,await CreateLesson(c));s=await SimTurn(c,s,"Start");s=await SimTurn(c,s,"Message","Falso");Assert.False(s.Answered);Assert.Null(s.Operations.Last().Outcome);
        s=await SimTurn(c,s,"Simplify");s=await SimTurn(c,s,"Answer","Falso");Assert.Equal("CorrectWithHelp",s.Operations.Last().Outcome);Assert.Equal(1,s.Hints);
    }
    [Fact] public async Task DesignConversationCanRefineAnUnappliedProposalWithoutChangingTheDraft()
    {
        using var c=await Teacher();var l=await Design(c,await CreateLesson(c));l=await Design(c,l,new(Guid.NewGuid(),l.Revision,"Content","Simplifica la propuesta anterior"));
        using var data=System.Text.Json.JsonDocument.Parse(provider.Requests.Last().Messages.Last().Text);Assert.Equal("UnappliedProposal",data.RootElement.GetProperty("baseSource").GetString());Assert.Contains("Mock",data.RootElement.GetProperty("draft").GetProperty("sections")[0].GetProperty("content").GetString());
        Assert.Equal("Contenido original",l.Content.Sections[0].Content);Assert.Equal(2,l.Proposals.Length);Assert.Equal(l.Proposals[0].DraftRevision,l.Proposals[1].DraftRevision);
    }
}
