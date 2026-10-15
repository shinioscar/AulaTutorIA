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

public sealed partial class LessonTests : IAsyncLifetime
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
        public bool Hold;public AiProviderException? Failure;public string? ResponseOverride;public List<AiChatRequest> Requests {get;}=[];public string HealthModel=MockAiProvider.ModelId;
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ProviderId=>mock.ProviderId;public AiCapabilities Capabilities=>mock.Capabilities;
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)=>Task.FromResult(new AiHealthResult(true,"Mock",HealthModel));
        public async Task<AiChatResult> GenerateAsync(AiChatRequest request,CancellationToken ct)
        {Interlocked.Increment(ref Calls);lock(Requests)Requests.Add(request);var hold=Hold;var failure=Failure;Started.TrySetResult();if(hold)await Release.Task.WaitAsync(ct);if(failure is not null)throw failure;return ResponseOverride is null ? await mock.GenerateAsync(request,ct) : new AiChatResult(ResponseOverride,ProviderId,MockAiProvider.ModelId,null,null,"stop");}
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

}
