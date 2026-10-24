using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaTutor.IntegrationTests;

public sealed partial class PersonalAiTests
{
    [Fact]
    public async Task AdminAndPendingTeacherCannotReadOrInstallPersonalConnections()
    {
        await using(var scope=factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AdminBootstrap>().CreateAsync("admin@example.invalid",password,"Admin ficticio");
        foreach(var role in new[]{"Admin","Pending"})
        {
            using var client=TestHost.Client(factory);await Csrf(client);var email=role=="Admin"?"admin@example.invalid":Guid.NewGuid().ToString("N")+"@example.invalid";
            if(role=="Pending")await client.PostAsJsonAsync("/api/auth/register",new RegisterRequest(email,password,"Pendiente ficticio","Teacher"));
            Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/auth/login",new LoginRequest(email,password))).StatusCode);await Csrf(client);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/ai-connections/")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/ai-connections/manual",new ManualAiRequest("Groq",Key))).StatusCode);
        }
        Assert.Equal(0,gateway.Exchanges);
    }
    [Fact]
    public async Task OwnerCannotSelectAnotherUsersCredentialOrSupplyAnOwnerInManualInput()
    {
        using var a=await User();using var b=await User();var c=await ConnectManual(a);var other=(await b.GetFromJsonAsync<UserView>("/api/me"))!;
        Assert.Equal(HttpStatusCode.NotFound,(await b.PutAsJsonAsync("/api/ai-connections/selection",new SelectAiRequest(c.Id,"openai/gpt-oss-20b",true,true))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await a.PostAsJsonAsync("/api/ai-connections/manual",new{provider="Gemini",apiKey=Key,ownerId=other.Id,userId=other.Id})).StatusCode);
        Assert.Empty((await View(b)).Connections);Assert.Equal(2,(await View(a)).Connections.Length);
    }
    [Fact]
    public async Task DurableVaultSurvivesNewInstanceAndSeparatesOwnerRevisionAndPurpose()
    {
        using var client=await User();await ConnectManual(client);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();var row=await db.PersonalAiConnections.SingleAsync();
        var fresh=new PersonalAiVault(scope.ServiceProvider.GetRequiredService<IConfiguration>(),scope.ServiceProvider.GetRequiredService<IOptions<PersonalAiOptions>>());
        Assert.Equal(Key,fresh.Unprotect(PersonalAiVault.CredentialPurpose(row.OwnerId,row.Id,row.CredentialRevision),row.ProtectedCredential));
        Assert.Throws<AppException>(()=>fresh.Unprotect(PersonalAiVault.CredentialPurpose(row.OwnerId,row.Id,row.CredentialRevision+1),row.ProtectedCredential));
        Assert.Throws<AppException>(()=>fresh.Unprotect(PersonalAiVault.CredentialPurpose(row.OwnerId,Guid.NewGuid(),row.CredentialRevision),row.ProtectedCredential));
        var expired=fresh.ProtectTemporary("qa-expiry","fictitious-temporary-value",DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Throws<AppException>(()=>fresh.UnprotectTemporary("qa-expiry",expired));
    }
    [Fact]
    public async Task MissingCustodyFailsClosedWithoutInventingAnEphemeralProtector()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["DataProtection:KeyPath"]="relative-unsafe"}).Build();
        var vault=new PersonalAiVault(config,Options.Create(new PersonalAiOptions()));Assert.False(vault.Available);
        Assert.Throws<AppException>(()=>vault.Protect("credential","fictitious-value"));
        using var client=await User();Assert.Contains("Mock",await client.GetStringAsync("/api/health"));
    }
    [Fact]
    public async Task AdditiveMigrationPreservesExistingInstitutionalReservations()
    {
        await using(var scope=factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().GetService<IMigrator>().MigrateAsync("20261023015145_OperationalResilience");
        using var client=await User();var user=(await client.GetFromJsonAsync<UserView>("/api/me"))!;var id=Guid.NewGuid();
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AiReservations\" (\"Id\",\"ActorId\",\"OrganizationId\",\"Provider\",\"Model\",\"CorrelationId\",\"CreatedAt\",\"LeaseUntil\",\"FinishedAt\",\"State\",\"EstimatedTokens\",\"DurationMs\") VALUES ({id},{user.Id},{user.OrganizationId},'Groq','existing-model','existing-operation',clock_timestamp(),clock_timestamp(),clock_timestamp(),'Succeeded',100,10)");
            await db.Database.MigrateAsync();await db.Database.MigrateAsync();
            var row=await db.AiReservations.SingleAsync();Assert.Equal(id,row.Id);Assert.Equal("Institutional",row.Scope);Assert.Equal("legacy",row.Operation);Assert.Equal(100,row.EstimatedTokens);Assert.Equal("Succeeded",row.State);Assert.Null(row.ConnectionId);
        }
    }
}
