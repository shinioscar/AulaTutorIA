using System.Net;
using System.Net.Http.Json;
using AulaTutor.Api.Identity;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Microsoft.Extensions.Options;

namespace AulaTutor.IntegrationTests;

public sealed class TutoringTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> factory=null!;
    private string database="",maintenance="";
    private readonly string password="Aa1!"+Guid.NewGuid().ToString("N");
    private readonly ControlledProvider provider=new();private readonly TestClock clock=new();
    private sealed class TestClock : TimeProvider
    { public TimeSpan Offset {get;set;} public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow+Offset; }
    private sealed class ControlledProvider : IAiProvider
    {
        private readonly MockAiProvider mock=new();public int Calls;
        public bool Hold;public AiProviderException? Failure;public string HealthModel=MockAiProvider.ModelId;
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ProviderId=>mock.ProviderId;public AiCapabilities Capabilities=>mock.Capabilities;
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)=>Task.FromResult(new AiHealthResult(true,"Mock",HealthModel));
        public async Task<AiChatResult> GenerateAsync(AiChatRequest request,CancellationToken ct)
        {Interlocked.Increment(ref Calls);var hold=Hold;var failure=Failure;Started.TrySetResult();if(hold)await Release.Task.WaitAsync(ct);if(failure is not null)throw failure;return await mock.GenerateAsync(request,ct);}
    }
    public async Task InitializeAsync()
    {
        var configured=Environment.GetEnvironmentVariable("AULATUTOR_TEST_CONNECTION")??throw new InvalidOperationException("Real PostgreSQL test connection is required.");
        database="aulatutor_test_"+Guid.NewGuid().ToString("N");
        var config=new NpgsqlConnectionStringBuilder(configured){Database="postgres",IncludeErrorDetail=false};maintenance=config.ConnectionString;
        await using(var admin=new NpgsqlConnection(maintenance)){await admin.OpenAsync();await new NpgsqlCommand($"CREATE DATABASE \"{database}\"",admin).ExecuteNonQueryAsync();}
        config.Database=database;
        factory=TestHost.Create(config.ConnectionString).WithWebHostBuilder(builder=>builder.ConfigureServices(services=>
        {services.RemoveAll<IAiProvider>();services.AddSingleton<IAiProvider>(provider);services.RemoveAll<TimeProvider>();services.AddSingleton<TimeProvider>(clock);}));
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await db.Database.MigrateAsync();await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AdminBootstrap>().CreateAsync("admin@example.invalid",password,"Admin ficticio");
    }
    public async Task DisposeAsync()
    {
        provider.Release.TrySetResult();if(factory is not null)await factory.DisposeAsync();
        if(!System.Text.RegularExpressions.Regex.IsMatch(database,"^aulatutor_test_[a-f0-9]{32}$"))return;
        await using var admin=new NpgsqlConnection(maintenance);await admin.OpenAsync();await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();
    }
    private static async Task Csrf(HttpClient client)
    {var csrf=await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");client.DefaultRequestHeaders.Add("X-XSRF-TOKEN",csrf!.RequestToken);}
    private async Task<HttpClient> Login(string email)
    {var client=TestHost.Client(factory);await Csrf(client);Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/auth/login",new LoginRequest(email,password))).StatusCode);await Csrf(client);return client;}
    private async Task<(HttpClient Client,UserView User)> Register(string role="Student")
    {var email=$"{Guid.NewGuid():N}@example.invalid";using var anonymous=TestHost.Client(factory);await Csrf(anonymous);Assert.Equal(HttpStatusCode.Accepted,(await anonymous.PostAsJsonAsync("/api/auth/register",new RegisterRequest(email,password,"Ficticio",role))).StatusCode);var client=await Login(email);return(client,(await client.GetFromJsonAsync<UserView>("/api/me"))!);}
    private static CreateTutoringRequest CreateRequest()=>new(Guid.NewGuid(),"IF EXISTS","Comprender condiciones");
    private static TutoringTurnRequest Turn(TutoringSessionView session,string action="Start",string? text=null)=>new(Guid.NewGuid(),session.Revision,action,text);
    private static async Task<TutoringSessionView> Create(HttpClient client,CreateTutoringRequest? request=null)
    {var response=await client.PostAsJsonAsync("/api/tutoring-sessions/",request??CreateRequest());Assert.Equal(HttpStatusCode.Created,response.StatusCode);return(await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;}
    private static async Task<TutoringSessionView> Send(HttpClient client,TutoringSessionView session,TutoringTurnRequest? request=null)
    {var response=await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request??Turn(session));Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;}
    private static async Task<TutoringSessionView> Get(HttpClient client,Guid id)=>(await client.GetFromJsonAsync<TutoringSessionView>($"/api/tutoring-sessions/{id}"))!;

    [Fact]
    public async Task PrivateMultiTurnConversationPersistsReopensPausesAndCompletesWithoutExtraCall()
    {
        var(client,user)=await Register();using var _=client;var session=await Create(client);Assert.Empty(session.Messages);Assert.Equal(0,provider.Calls);
        session=await Send(client,session);Assert.Equal("Active",session.State);Assert.Equal("Diagnosis",session.Activity);Assert.Contains("¿En qué",session.Messages.Last().Text);
        session=await Send(client,session,Turn(session,"Message","Compruebo si hay al menos una fila"));Assert.Contains("```sql",session.Messages.Last().Text);
        session=await Send(client,session,Turn(session,"Hint"));Assert.Contains("Pista",session.Messages.Last().Text);
        session=await Send(client,session,Turn(session,"Simplify"));Assert.Contains("Imagina",session.Messages.Last().Text);
        using var reopened=await Login(user.Email);Assert.Equal(session.Messages.Select(m=>m.Id),(await Get(reopened,session.Id)).Messages.Select(m=>m.Id));
        var paused=await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/pause",new TutoringTransitionRequest(session.Revision));Assert.Equal(HttpStatusCode.OK,paused.StatusCode);session=(await paused.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Message","No permitido"))).StatusCode);
        var resumed=await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/resume",new TutoringTransitionRequest(session.Revision));session=(await resumed.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        var completed=await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/complete",new TutoringTransitionRequest(session.Revision));Assert.Equal(HttpStatusCode.OK,completed.StatusCode);session=(await completed.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Equal("Completed",session.State);Assert.NotNull(session.CompletedAt);Assert.Contains("Tutoría finalizada",session.Summary);Assert.Equal(4,provider.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Message","Otro"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/resume",new TutoringTransitionRequest(session.Revision))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await reopened.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/complete",new TutoringTransitionRequest(0))).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();Assert.Equal(8,(await db.Database.GetAppliedMigrationsAsync()).Count());Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Equal(8,await db.TutoringMessages.CountAsync());
        Assert.All(session.Messages.Where(m=>m.Role=="Tutor"),m=>{Assert.Equal("Mock",m.ProviderId);Assert.Equal("tutoring-v1",m.PromptVersion);Assert.Null(m.Usage);});
    }

    [Fact]
    public async Task OtherStudentsTeachersAndAdminCannotReadOrChangePrivateSessionsEvenInSameGroup()
    {
        var(a,student)=await Register();using var _a=a;var(b,other)=await Register();using var _b=b;var(teacher,teacherUser)=await Register("Teacher");using var _t=teacher;using var admin=await Login("admin@example.invalid");
        await admin.PostAsJsonAsync($"/api/admin/users/{teacherUser.Id}/teacher-decision",new TeacherDecisionRequest(true));teacher.Dispose();using var approved=await Login(teacherUser.Email);
        var group=(await(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Grupo compartido"))).Content.ReadFromJsonAsync<GroupView>())!;
        await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(student.Id,"Student"));await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(teacherUser.Id,"Teacher"));
        var session=await Create(a);Assert.Empty((await b.GetFromJsonAsync<TutoringSessionItem[]>("/api/students/me/tutoring-sessions"))!);
        foreach(var client in new[]{b,approved,admin})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/tutoring-sessions/{session.Id}")).StatusCode);
            foreach(var action in new[]{"pause","resume","complete"})Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/{action}",new TutoringTransitionRequest(0))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await approved.GetAsync("/api/students/me/tutoring-sessions")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await admin.PostAsJsonAsync("/api/tutoring-sessions/",CreateRequest())).StatusCode);Assert.Equal(0,provider.Calls);
    }

    [Fact]
    public async Task AnonymousAndCsrfWritesAreDeniedBeforeAnyGeneration()
    {
        using var anonymous=TestHost.Client(factory);var id=Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/students/me/tutoring-sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync($"/api/tutoring-sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/tutoring-sessions/",CreateRequest())).StatusCode);
        var(client,_)=await Register();using var _c=client;var session=await Create(client);client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/tutoring-sessions/",CreateRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session))).StatusCode);
        foreach(var action in new[]{"pause","resume","complete"})Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/{action}",new TutoringTransitionRequest(0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/turns/{id}/cancel",new TutoringTransitionRequest(0))).StatusCode);Assert.Equal(0,provider.Calls);
    }

    [Fact]
    public async Task IdempotencyBindsPayloadAndStaleRevisionsAreRejected()
    {
        var(client,_)=await Register();using var _c=client;var creation=CreateRequest();var session=await Create(client,creation);Assert.Equal(session.Id,(await Create(client,creation)).Id);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/tutoring-sessions/",creation with{Topic="Otro tema"})).StatusCode);
        var request=Turn(session);session=await Send(client,session,request);var replay=await Send(client,session,request);Assert.Equal(session.Messages.Select(m=>m.Id),replay.Messages.Select(m=>m.Id));Assert.Equal(1,provider.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request with{Action="Message",Text="Otro contenido"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",new TutoringTurnRequest(Guid.NewGuid(),0,"Message","Vieja revisión"))).StatusCode);
    }

    [Fact]
    public async Task InvalidInputsAndHistoryLimitDoNotCallProvider()
    {
        var(client,_)=await Register();using var _c=client;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/tutoring-sessions/",CreateRequest() with{Topic="x"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/tutoring-sessions/",CreateRequest() with{RequestId=Guid.Empty})).StatusCode);
        var session=await Create(client);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Message","Antes de inicio"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Start","No admite texto"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Message",new string('x',2001)))).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();var stored=await db.TutoringSessions.SingleAsync();stored.NextSequence=99;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session))).StatusCode);Assert.Equal(0,provider.Calls);
    }

    [Fact]
    public async Task QuotaFailureIsDurableRetryAfterHonoredAndSameAttemptRecovered()
    {
        provider.Failure=new AiProviderException(AiErrorCode.RateLimited,TimeSpan.FromSeconds(60));var(client,_)=await Register();using var _c=client;
        var session=await Create(client);var request=Turn(session);session=await Send(client,session,request);Assert.Single(session.Messages);Assert.Equal("RateLimited",session.Turns.Single().ErrorCode);Assert.False(session.Turns.Single().CanRetry);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request with{Revision=session.Revision,Retry=true})).StatusCode);Assert.Equal(1,provider.Calls);
        clock.Offset=TimeSpan.FromSeconds(61);provider.Failure=null;session=await Get(client,session.Id);Assert.True(session.Turns.Single().CanRetry);
        session=await Send(client,session,request with{Revision=session.Revision,Retry=true});Assert.Equal(2,session.Messages.Length);Assert.Equal(2,session.Turns.Single().Attempts);Assert.Equal(2,provider.Calls);
    }

    [Fact]
    public async Task ConcurrentDuplicateDoesNotHoldDatabaseLockAndLateCanceledReplyIsDiscarded()
    {
        provider.Hold=true;var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        var first=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var duplicate=await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request);Assert.Equal(HttpStatusCode.Accepted,duplicate.StatusCode);session=(await duplicate.Content.ReadFromJsonAsync<TutoringSessionView>())!;
        Assert.Single(session.Messages);Assert.Equal(1,provider.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session,"Message","Concurrente"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/complete",new TutoringTransitionRequest(session.Revision))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/turns/{request.RequestId}/cancel",new TutoringTransitionRequest(session.Revision))).StatusCode);
        provider.Release.TrySetResult();Assert.Equal(HttpStatusCode.OK,(await first).StatusCode);session=await Get(client,session.Id);Assert.Single(session.Messages);Assert.Equal("Cancelled",session.Turns.Single().State);Assert.Null(session.PendingRequestId);
    }

    [Fact]
    public async Task ExpiredLeaseCanBeRecoveredAndOlderWorkerCannotDuplicateReply()
    {
        provider.Hold=true;var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        var old=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));clock.Offset=TimeSpan.FromSeconds(200);
        session=await Get(client,session.Id);Assert.Equal("Interrupted",session.Turns.Single().ErrorCode);Assert.True(session.Turns.Single().CanRetry);
        provider.Hold=false;session=await Send(client,session,request with{Revision=session.Revision,Retry=true});provider.Release.TrySetResult();await old;
        session=await Get(client,session.Id);Assert.Equal(2,session.Messages.Length);Assert.Equal(2,provider.Calls);Assert.Equal(2,session.Turns.Single().Attempts);
    }

    [Fact]
    public async Task ClientCancellationPreservesAttemptForExplicitRetry()
    {
        provider.Hold=true;var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);using var cancellation=new CancellationTokenSource();
        var call=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request,cancellation.Token);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>call);
        for(var i=0;i<30;i++){session=await Get(client,session.Id);if(session.Turns.Single().State=="Failed")break;await Task.Delay(30);}
        Assert.Single(session.Messages);Assert.Equal("Canceled",session.Turns.Single().ErrorCode);
        provider.Hold=false;session=await Send(client,session,request with{Revision=session.Revision,Retry=true});Assert.Equal(2,session.Messages.Length);
    }

    [Fact]
    public async Task RevokedStudentDoesNotPersistOrReceiveGeneratedReply()
    {
        provider.Hold=true;var(client,user)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        var call=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request);await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));using var admin=await Login("admin@example.invalid");
        await admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/status",new AccountStatusRequest(false));provider.Release.TrySetResult();Assert.Equal(HttpStatusCode.Unauthorized,(await call).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Single(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().TutoringMessages.ToArrayAsync());
    }
    [Fact]
    public async Task GenerationTimeoutPreservesAttemptAndCanBeRecovered()
    {
        factory.Services.GetRequiredService<IOptions<AiProviderOptions>>().Value.TimeoutSeconds=1;
        provider.Hold=true;var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        session=await Send(client,session,request);Assert.Single(session.Messages);Assert.Equal("Timeout",session.Turns.Single().ErrorCode);
        provider.Hold=false;session=await Send(client,session,request with{Revision=session.Revision,Retry=true});Assert.Equal(2,session.Messages.Length);Assert.Equal(2,provider.Calls);
    }
    [Fact]
    public async Task ModelChangeDoesNotFallBackOrGenerateUnexpectedly()
    {
        var(client,_)=await Register();using var _c=client;var session=await Create(client);provider.HealthModel="different-model";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",Turn(session))).StatusCode);
        Assert.Equal(0,provider.Calls);Assert.Empty((await Get(client,session.Id)).Messages);
    }
    [Fact]
    public async Task RepeatedFailuresHaveBoundedAttemptsAndCanCloseWithLocalSummary()
    {
        provider.Failure=new AiProviderException(AiErrorCode.Connection);var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        session=await Send(client,session,request);
        for(var attempt=0;attempt<2;attempt++)session=await Send(client,session,request with{Revision=session.Revision,Retry=true});
        Assert.Equal(3,provider.Calls);Assert.Single(session.Messages);Assert.False(session.Turns.Single().CanRetry);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request with{Revision=session.Revision,Retry=true})).StatusCode);
        var closed=await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/complete",new TutoringTransitionRequest(session.Revision));
        Assert.Equal(HttpStatusCode.OK,closed.StatusCode);Assert.Equal("Completed",(await closed.Content.ReadFromJsonAsync<TutoringSessionView>())!.State);Assert.Equal(3,provider.Calls);
    }
    [Fact]
    public async Task GenerationEndpointIsRateLimitedEvenForIdempotentReplay()
    {
        var(client,_)=await Register();using var _c=client;var session=await Create(client);var request=Turn(session);
        session=await Send(client,session,request);for(var i=0;i<5;i++)session=await Send(client,session,request);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",request)).StatusCode);Assert.Equal(1,provider.Calls);
    }
}
