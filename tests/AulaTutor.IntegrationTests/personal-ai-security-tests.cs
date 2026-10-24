using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class PersonalAiTests
{
    [Fact]
    public async Task AccountSwitchRejectsReturnedAuthorizationBeforeExchange()
    {
        using var a = await User(); using var b = await User(); var other = (await b.GetFromJsonAsync<UserView>("/api/me"))!;
        var start = await Start(a); await Return(a, start);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/auth/login", new LoginRequest(other.Email, password))).StatusCode); await Csrf(a);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode);
        Assert.Equal(0, gateway.Exchanges); Assert.Empty((await View(a)).Connections);
    }
    [Fact]
    public async Task CallbackCannotBeInstalledFromDifferentBrowserAndCannotOverrideDestination()
    {
        using var a = await User(); using var b = await User(); var start = await Start(a);
        using var stolen = await Return(b, start); Assert.Contains("https://localhost:4200/ai-provider/return?result=rejected", stolen.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await b.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode); Assert.Equal(0, gateway.Exchanges);
        using var original = await Return(a, start); Assert.Equal("localhost", original.Headers.Location!.Host);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode);
    }
    [Theory][InlineData(400)][InlineData(401)][InlineData(503)]
    public async Task FailedExchangeIsSingleUseAndClearsTemporarySecrets(int status)
    {
        using var client = await User(); var start = await Start(client); await Return(client, start); gateway.Status = (HttpStatusCode)status;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete", new { })).StatusCode); Assert.Equal(1, gateway.Exchanges);
        await using var scope = factory.Services.CreateAsyncScope(); var row = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().PersonalAiOAuths.SingleAsync();
        Assert.Equal("Failed", row.Status); Assert.Empty(row.ProtectedVerifier); Assert.Null(row.ProtectedCode); Assert.Empty((await View(client)).Connections);
    }
    [Fact]
    public async Task CredentialRotationRejectsOldSnapshotAndDoesNotResetBudget()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client);
        await client.PostAsJsonAsync("/api/ai-connections/manual", new ManualAiRequest("Groq", Key + "_rotated"));
        Assert.Equal("None", (await View(client)).Selection);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start"))).StatusCode); Assert.Equal(0, gateway.Generations);
        await using var scope = factory.Services.CreateAsyncScope(); var row = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().PersonalAiConnections.SingleAsync(); Assert.Equal(2, row.CredentialRevision);
    }
    [Fact]
    public async Task SecretEchoIsRejectedAndStoredHistoryDoesNotReceiveIt()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client);
        gateway.Text = JsonSerializer.Serialize(new { text = Key, activity = "Diagnosis", focus = "Ficticio" });
        var response = await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages", new TutoringTurnRequest(Guid.NewGuid(), session.Revision, "Start"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(Key, body); Assert.Contains("InvalidResponse", body);
        Assert.DoesNotContain(Key, new ManualAiRequest("Groq", Key).ToString());
    }
    [Fact]
    public async Task PersonalQuotaIsDistinctFromInstitutionalAndSharedAcrossRotations()
    {
        using var client = await User(); var c = await ConnectManual(client); var user = (await client.GetFromJsonAsync<UserView>("/api/me"))!;
        var actor = new Actor(user.Id, user.OrganizationId, user.Roles, TeacherApproval.NotRequested, true);
        var policy = new AiQuotaOptions { Limits = new() { GlobalConcurrency = 4 }, PersonalLimits = new() { UserPerDay = 1 } };
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(); var connection = await db.PersonalAiConnections.SingleAsync();
        var personal = new PersonalInferenceCall(user.Id, c.Id, connection.CredentialRevision, connection.Fingerprint, null, "test_personal");
        var id = await PersistentAiAdmission.ReserveForAsync(db, actor, null, "Groq", "openai/gpt-oss-20b", 10, Guid.NewGuid().ToString("N"), policy, 30, default, personal);
        await db.AiReservations.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "Failed").SetProperty(r => r.FinishedAt, DateTimeOffset.UtcNow));
        var institutional = await PersistentAiAdmission.ReserveForAsync(db, actor, null, "Groq", "institutional-test", 10, Guid.NewGuid().ToString("N"), policy, 30, default);
        Assert.NotEqual(Guid.Empty, institutional);
        connection.CredentialRevision++; connection.Fingerprint = PersonalAiVault.Hash(Key + "new");
        connection.ProtectedCredential=scope.ServiceProvider.GetRequiredService<PersonalAiVault>().Protect(PersonalAiVault.CredentialPurpose(connection.OwnerId,connection.Id,connection.CredentialRevision),Key+"new"); await db.SaveChangesAsync();
        var rotated = personal with { Revision = connection.CredentialRevision, Fingerprint = connection.Fingerprint };
        var error = await Assert.ThrowsAsync<AiProviderException>(() => PersistentAiAdmission.ReserveForAsync(db, actor, null, "Groq", "openai/gpt-oss-20b", 10, Guid.NewGuid().ToString("N"), policy, 30, default, rotated));
        Assert.Equal(AiErrorCode.RateLimited, error.Code);
    }
    [Fact]
    public async Task PersonalConcurrentAdmissionsHaveOneWinnerAcrossIndependentScopes()
    {
        using var client = await User(); var c = await ConnectManual(client); var user = (await client.GetFromJsonAsync<UserView>("/api/me"))!;
        var actor = new Actor(user.Id, user.OrganizationId, user.Roles, TeacherApproval.NotRequested, true);
        PersonalInferenceCall personal;
        await using(var scope = factory.Services.CreateAsyncScope()) { var row = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().PersonalAiConnections.SingleAsync(); personal = new(user.Id, c.Id, row.CredentialRevision, row.Fingerprint, null, "concurrent_test"); }
        async Task<bool> Admit()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            try { await PersistentAiAdmission.ReserveForAsync(scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(), actor, null, "Groq", "openai/gpt-oss-20b", 10, Guid.NewGuid().ToString("N"), new(), 30, default, personal); return true; }
            catch(AiProviderException e) when (e.Code == AiErrorCode.RateLimited) { return false; }
        }
        var result = await Task.WhenAll(Admit(), Admit()); Assert.Single(result, x => x);
    }
    [Fact]
    public async Task RevokedRoleCannotUseConnectionAndAdminHasNoPersonalAccessByRole()
    {
        using var client = await User(); await ConnectManual(client); var user = (await client.GetFromJsonAsync<UserView>("/api/me"))!;
        await using(var scope = factory.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(); var role = await db.Roles.SingleAsync(r => r.Name == RoleNames.Student); db.UserRoles.Remove(await db.UserRoles.SingleAsync(r => r.UserId == user.Id && r.RoleId == role.Id)); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ai-connections/")).StatusCode);
        Assert.Equal(0, gateway.Exchanges);
    }
    [Fact]
    public async Task SnapshotAndBudgetAttributionCannotBeChangedByDirectPersistence()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id); var session = await Create(client);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var binding = await db.AiInferenceBindings.SingleAsync(); binding.Model = "other-model";
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TutoringSessions\" SET \"AiBindingId\"=NULL WHERE \"Id\"={session.Id}"));
    }
    [Fact]
    public async Task PublicHealthNeverReadsPersonalSelectionOrCallsMetadata()
    {
        using var client = await User(); var c = await ConnectManual(client); await Activate(client, c.Id);
        var response = await client.GetStringAsync("/api/health"); Assert.Contains("Mock", response); Assert.DoesNotContain(Key, response); Assert.Equal(0, gateway.Generations); Assert.Equal(0, gateway.Catalogs);
    }
    [Fact]
    public async Task FreeModelBecomingPaidFailsBeforeInferenceWithoutChangingSnapshotOrFallback()
    {
        using var client=await User();var start=await Start(client);await Return(client,start);await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete",new{});
        var c=(await View(client)).Connections.Single();await Activate(client,c.Id,"OpenRouter","qwen/qwen3.8-27b:free",false);var session=await Create(client);
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();await db.PersonalAiModels.ExecuteUpdateAsync(s=>s.SetProperty(m=>m.ExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));}
        gateway.ModelCatalog=Catalog.Replace("\"completion\":\"0\"","\"completion\":\"0.001\"",StringComparison.Ordinal);
        var response=await client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",new TutoringTurnRequest(Guid.NewGuid(),session.Revision,"Start"));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var view=(await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;Assert.Equal("PaymentAuthorizationRequired",view.Turns.Single().ErrorCode);Assert.Equal("Failed",view.Turns.Single().State);Assert.Equal("OpenRouter",view.ProviderId);Assert.Equal(0,gateway.Generations);
    }
    [Fact]
    public async Task LogoutDuringInferenceDiscardsLatePersonalOutput()
    {
        using var client=await User();var c=await ConnectManual(client);await Activate(client,c.Id);var session=await Create(client);gateway.Hold=true;
        var pending=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",new TutoringTurnRequest(Guid.NewGuid(),session.Revision,"Start"));
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/auth/logout",new{})).StatusCode);gateway.Release.TrySetResult();
        using var response=await pending;Assert.Equal(HttpStatusCode.OK,response.StatusCode);var view=(await response.Content.ReadFromJsonAsync<TutoringSessionView>())!;Assert.DoesNotContain(view.Messages,m=>m.Role=="Tutor");Assert.Equal("Authentication",view.Turns.Single().ErrorCode);
    }
    [Fact]
    public async Task RoleRevokedWhileFinisherWaitsForSharedLockCannotCommitAcademicOutput()
    {
        using var client=await User();var c=await ConnectManual(client);await Activate(client,c.Id);var session=await Create(client);gateway.Hold=true;
        var pending=client.PostAsJsonAsync($"/api/tutoring-sessions/{session.Id}/messages",new TutoringTurnRequest(Guid.NewGuid(),session.Revision,"Start"));
        await gateway.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var gateScope=factory.Services.CreateAsyncScope();var gate=gateScope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await using var tx=await gate.Database.BeginTransactionAsync();await PersonalAiSession.LockAsync(gate,default);gateway.Release.TrySetResult();
        var waiting=false;
        for(var attempt=0;attempt<100;attempt++)
        {
            waiting=await gate.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND objid=8128001 AND NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database())) AS \"Value\"").SingleAsync();
            if(waiting)break;await Task.Delay(50);
        }
        Assert.True(waiting,"The personal finisher must wait for the shared custody/admission lock.");
        var user=(await client.GetFromJsonAsync<UserView>("/api/me"))!;
        var role=await gate.Roles.SingleAsync(r=>r.Name==RoleNames.Student);gate.UserRoles.Remove(await gate.UserRoles.SingleAsync(r=>r.UserId==user.Id&&r.RoleId==role.Id));await gate.SaveChangesAsync();await tx.CommitAsync();
        using var response=await pending;Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        var turn=await gate.TutoringTurns.AsNoTracking().SingleAsync(t=>t.SessionId==session.Id);Assert.Equal("Cancelled",turn.State.ToString());
        Assert.False(await gate.TutoringMessages.AnyAsync(m=>m.SessionId==session.Id&&m.Role==AulaTutor.Domain.Tutoring.TutoringMessageRole.Tutor));
    }
    [Fact]
    public async Task OAuthRejectsOversizedExchangeBeforeBufferingCredentialResponse()
    {
        using var client=await User();var start=await Start(client);await Return(client,start);gateway.OversizedExchange=true;
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/ai-connections/openrouter/complete",new{})).StatusCode);
        Assert.Equal(1,gateway.Exchanges);Assert.Equal(0,gateway.ExchangeBytesRead);Assert.Empty((await View(client)).Connections);
        await using var scope=factory.Services.CreateAsyncScope();var row=await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().PersonalAiOAuths.SingleAsync();
        Assert.Equal("Failed",row.Status);Assert.Empty(row.ProtectedVerifier);Assert.Null(row.ProtectedCode);
    }
}
