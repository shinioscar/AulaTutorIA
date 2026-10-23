using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private async Task<Guid> Reserve(UserView user, AiQuotaOptions? policy = null, int estimate = 10, Guid? group = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await PersistentAiAdmission.ReserveForAsync(scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>(),
            new(user.Id, PilotOrganization.Id, user.Roles, Enum.Parse<TeacherApproval>(user.TeacherApproval), true), group, "Groq", "fictitious-model", estimate,
            Guid.NewGuid().ToString("N"), policy ?? new(), 1, CancellationToken.None);
    }
    private Task Finish(Guid id, string state = "Succeeded", AiTokenUsage? usage = null, TimeSpan? retryAfter = null) =>
        factory.Services.GetRequiredService<IAiCallAdmission>().FinishAsync(id, state, usage, 12, retryAfter);

    [Fact] public async Task PersistentQuotaSerializesTwoTabsAcrossIndependentContexts()
    {
        var (client, user) = await Register(); using var c = client;
        var admissions = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            try { return (Id: await Reserve(user), Error: (AiErrorCode?)null); }
            catch (AiProviderException e) { return (Id: Guid.Empty, Error: (AiErrorCode?)e.Code); }
        }));
        var accepted = Assert.Single(admissions, x => x.Id != Guid.Empty);
        Assert.All(admissions.Where(x => x.Id == Guid.Empty), x => Assert.Equal(AiErrorCode.RateLimited, x.Error));
        await Finish(accepted.Id, "Uncertain");
        Assert.Equal(AiErrorCode.RateLimited, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(user))).Code);
        await using var scope = factory.Services.CreateAsyncScope(); var row = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.SingleAsync();
        Assert.Equal("Uncertain", row.State); Assert.Null(row.InputTokens); Assert.True(row.LeaseUntil > DateTimeOffset.UtcNow);
        Assert.Equal(0, provider.Calls);
    }
    [Fact] public async Task QuotaRevalidatesRevokedMembershipAndAccountBeforeAdmitting()
    {
        using var f = await AssignedFixture();
        Assert.Equal(HttpStatusCode.NoContent, (await f.Admin.DeleteAsync($"/api/groups/{f.Group.Id}/members/{f.UserA.Id}/Student")).StatusCode);
        Assert.Equal(AiErrorCode.Forbidden, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(f.UserA, group: f.Group.Id))).Code);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Admin.PutAsJsonAsync($"/api/admin/users/{f.UserA.Id}/status", new AccountStatusRequest(false))).StatusCode);
        Assert.Equal(AiErrorCode.Forbidden, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(f.UserA))).Code);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.ToArrayAsync());
    }
    [Theory]
    [InlineData("UserPerDay")][InlineData("ProviderPerDay")][InlineData("OrganizationPerDay")][InlineData("UserPerMinute")][InlineData("ProviderPerMinute")][InlineData("DailyEstimatedTokenBudget")]
    public async Task PersistentQuotaBudgetsRemainChargedAfterCompletion(string property)
    {
        var (client, user) = await Register(); using var c = client;
        var p = new AiQuotaOptions(); typeof(AiQuotaLimits).GetProperty(property)!.SetValue(p.Limits, property == "DailyEstimatedTokenBudget" ? 15 : 1);
        var id = await Reserve(user, p); await Finish(id, "Failed");
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Reserve(user, p)); Assert.Equal(AiErrorCode.RateLimited, error.Code); Assert.True(error.RetryAfter > TimeSpan.Zero);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.CountAsync());
    }
    [Fact] public async Task ProviderCooldownIsSharedAndUsageNeverBecomesAnEstimate()
    {
        var (a, ua) = await Register(); using var ac = a; var (b, ub) = await Register(); using var bc = b;
        var id = await Reserve(ua); await Finish(id, "Failed", retryAfter: TimeSpan.FromSeconds(90));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Reserve(ub)); Assert.Equal(AiErrorCode.RateLimited, error.Code); Assert.True(error.RetryAfter > TimeSpan.FromSeconds(60));
        await using var scope = factory.Services.CreateAsyncScope(); var row = await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().AiReservations.SingleAsync();
        Assert.Null(row.InputTokens); Assert.Equal(10, row.EstimatedTokens);
    }
    [Fact] public async Task GlobalConcurrencyAndGroupAndOrganizationOverridesArePersistent()
    {
        using var f = await AssignedFixture(); var a = await Reserve(f.UserA); var b = await Reserve(f.UserB);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Reserve(f.TeacherUser)); Assert.Equal(AiErrorCode.RateLimited, error.Code);
        await Finish(a); await Finish(b);
        var p = new AiQuotaOptions(); p.GroupDailyLimits[f.Group.Id] = 1;
        a = await Reserve(f.UserA, p, group: f.Group.Id); await Finish(a);
        Assert.Equal(AiErrorCode.RateLimited, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(f.UserB, p, group: f.Group.Id))).Code);
        p.OrganizationDailyLimits[PilotOrganization.Id] = 1;
        Assert.Equal(AiErrorCode.RateLimited, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(f.UserB, p))).Code);
    }
    [Fact] public async Task ReportedUsageAndKillSwitchSurviveServiceRecreationAndCannotBeRewritten()
    {
        var (client, user) = await Register(); using var c = client;
        var id = await Reserve(user); await Finish(id, usage: new(123, 45, false));
        await Finish(id, usage: new(0, 0, true)); // terminal state is fenced
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var row = await db.AiReservations.SingleAsync(); Assert.Equal(123, row.InputTokens); Assert.Equal(45, row.OutputTokens); Assert.Equal(10, row.EstimatedTokens);
        Assert.Equal(AiErrorCode.Forbidden, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(user, new() { AllowExternalCalls = false }))).Code);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"AiReservations\""));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"AiReservations\" SET \"EstimatedTokens\"=0"));
    }
    [Fact] public async Task ExpiredReservationRecoversConcurrencyButKeepsDailyChargeAfterRestart()
    {
        var (client, user) = await Register(); using var c = client;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
            db.AiReservations.Add(new() { ActorId = user.Id, OrganizationId = PilotOrganization.Id, Provider = "Groq", Model = "fictitious-model", CorrelationId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2), LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(-1), EstimatedTokens = 10 }); await db.SaveChangesAsync();
        }
        var id = await Reserve(user); await Finish(id);
        var p = new AiQuotaOptions(); p.Limits.UserPerDay = 2;
        Assert.Equal(AiErrorCode.RateLimited, (await Assert.ThrowsAsync<AiProviderException>(() => Reserve(user, p))).Code);
    }
    [Fact] public async Task OperationalAuditIsAtomicMinimalImmutableAndAdminOnly()
    {
        using var f = await AssignedFixture(); await OpenResponse(f); var detail = await Evidence(f); var request = Decision(detail, detail.Exercises[1].Id);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Admin.DeleteAsync($"/api/groups/{f.Group.Id}/members/{f.UserB.Id}/Student")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Review(f, request)).StatusCode);
        var response = await f.Admin.GetAsync("/api/admin/operations/audit"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(request.Comment, body); Assert.DoesNotContain(password, body); Assert.DoesNotContain("REFERENCIA_PRIVADA", body);
        foreach (var client in new[] { f.A, f.Teacher }) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/operations/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Admin.GetAsync("/api/admin/operations/audit?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.GetAsync("/api/admin/operations/usage")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var events = await db.AuditEvents.ToArrayAsync(); Assert.Contains(events, e => e.Type == "TeacherReviewRecorded" && e.ActorId == f.TeacherUser.Id); Assert.Contains(events, e => e.Type == "LessonAssignmentChanged"); Assert.Contains(events, e => e.Type == "MembershipChanged"); Assert.Contains(events, e => e.Type == "LessonPublicationChanged");
        Assert.Contains(events, e => e.Type == "MembershipChanged" && e.Change == "Deleted" && e.ResourceId.Contains(f.UserB.Id.ToString()));
        var count = events.Length;
        await using (var tx = await db.Database.BeginTransactionAsync())
        { db.Groups.Add(new() { Name = "Rolled back fictitious group", OrganizationId = PilotOrganization.Id }); await db.SaveChangesAsync(); await tx.RollbackAsync(); }
        Assert.Equal(count, await db.AuditEvents.CountAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditEvents\""));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"AuditEvents\" SET \"Result\"='changed'"));
    }
    [Fact] public async Task AdministrativePermissionIsRevalidatedAfterWaitingForTheMutationLock()
    {
        using var f = await AssignedFixture(); var (client, user) = await Register(); client.Dispose();
        Assert.Equal(HttpStatusCode.NoContent, (await f.Admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles", new RolesRequest(["Student", "Admin"]))).StatusCode);
        using var delegated = await Login(user.Email);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(841901)");
        var role = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).SingleAsync();
        db.UserRoles.Remove(await db.UserRoles.SingleAsync(r => r.UserId == user.Id && r.RoleId == role)); await db.SaveChangesAsync();
        var holder = ((Npgsql.NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;
        var pending = delegated.DeleteAsync($"/api/groups/{f.Group.Id}/members/{f.UserB.Id}/Student"); var waiting = false;
        for (var i = 0; i < 100 && !waiting; i++)
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_stat_clear_snapshot()");
            waiting = await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND {holder}=ANY(pg_blocking_pids(pid))").SingleAsync() > 0;
            if (pending.IsCompleted && !waiting) Assert.Fail("Administrative mutation completed before waiting.");
            if (!waiting) await Task.Delay(50);
        }
        Assert.True(waiting); await tx.CommitAsync(); Assert.Equal(HttpStatusCode.Forbidden, (await pending).StatusCode);
        Assert.True(await db.GroupMemberships.AnyAsync(m => m.GroupId == f.Group.Id && m.UserId == f.UserB.Id));
        Assert.DoesNotContain(await db.AuditEvents.ToArrayAsync(), e => e.Type == "MembershipChanged" && e.Change == "Deleted" && e.ResourceId.Contains(f.UserB.Id.ToString()));
    }
}
