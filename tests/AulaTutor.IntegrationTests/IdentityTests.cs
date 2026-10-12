using System.Net;
using System.Net.Http.Json;
using AulaTutor.Api.Identity;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AulaTutor.IntegrationTests;

public sealed class IdentityTests : IAsyncLifetime
{
    private string databaseName = "";
    private string maintenanceConnection = "";
    private WebApplicationFactory<Program> factory = null!;
    private readonly string password = "Aa1!" + Guid.NewGuid().ToString("N");
    private const string AdminEmail = "admin@example.invalid";

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AULATUTOR_TEST_CONNECTION")
            ?? throw new InvalidOperationException("AULATUTOR_TEST_CONNECTION is required. Load scripts/Load-LocalConfiguration.ps1; real PostgreSQL tests must not be skipped.");
        databaseName = "aulatutor_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", IncludeErrorDetail = false };
        maintenanceConnection = connection.ConnectionString;
        await using var admin = new NpgsqlConnection(maintenanceConnection);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin).ExecuteNonQueryAsync();
        connection.Database = databaseName;
        factory = TestHost.Create(connection.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync(); // Idempotent schema application.
        await scope.ServiceProvider.GetRequiredService<AdminBootstrap>().CreateAsync(AdminEmail, password, "Admin ficticio");
    }
    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        if (!databaseName.StartsWith("aulatutor_test_", StringComparison.Ordinal) || databaseName.Length != 47) return;
        await using var connection = new NpgsqlConnection(maintenanceConnection);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }
    private async Task<HttpClient> AnonymousAsync()
    {
        var client = TestHost.Client(factory);
        await CsrfAsync(client);
        return client;
    }
    private static async Task CsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf!.RequestToken);
    }
    private async Task<HttpClient> AdminAsync()
    {
        var client = await AnonymousAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminEmail, password))).StatusCode);
        await CsrfAsync(client); return client;
    }
    private async Task<(HttpClient Client, UserView User)> RegisterAsync(string role = "Student")
    {
        var client = await AnonymousAsync();
        var email = $"{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, "Persona ficticia", role))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password))).StatusCode);
        await CsrfAsync(client);
        return (client, (await client.GetFromJsonAsync<UserView>("/api/me"))!);
    }

    [Fact]
    public async Task RegistrationLoginCookiesAndRealPersistence()
    {
        var (client, user) = await RegisterAsync(); using var _ = client;
        Assert.Contains(RoleNames.Student, user.Roles);
        Assert.Equal(TeacherApproval.NotRequested.ToString(), user.TeacherApproval);
        using var duplicate = await AnonymousAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await duplicate.PostAsJsonAsync("/api/auth/register", new RegisterRequest(user.Email, password, "Otro nombre", "Student"))).StatusCode);
        var response = await duplicate.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, password));
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-AulaTutor.Session="));
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var persisted = await db.Users.SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(password, persisted.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(persisted, persisted.PasswordHash!, password));
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == user.Email));
    }

    [Fact]
    public async Task WrongCredentialsAndLockoutAreGeneric()
    {
        var (registered, user) = await RegisterAsync(); registered.Dispose();
        using var client = await AnonymousAsync();
        string? errorBody = null;
        for (var i = 0; i < 5; i++)
        {
            var failure = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Wrong-Fictitious!123"));
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode); errorBody = await failure.Content.ReadAsStringAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, password))).StatusCode);
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("unknown@example.invalid", password));
        using var knownError = System.Text.Json.JsonDocument.Parse(errorBody!);
        using var unknownError = System.Text.Json.JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        Assert.Equal(knownError.RootElement.GetProperty("title").GetString(), unknownError.RootElement.GetProperty("title").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id.ToString()))!));
    }

    [Fact]
    public async Task PendingTeacherDeniedUntilAdminApprovalThenOnlyAssignedGroups()
    {
        var (teacher, pending) = await RegisterAsync("Teacher"); using var _ = teacher;
        Assert.Equal("Pending", pending.TeacherApproval); Assert.Empty(pending.Roles);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/groups/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync("/api/groups/", new GroupRequest("No autorizado"))).StatusCode);
        using var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/users/{pending.Id}/roles", new RolesRequest(["Teacher"]))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/users/{pending.Id}/teacher-decision", new TeacherDecisionRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await teacher.GetAsync("/api/me")).StatusCode); // Old stamp revoked.
        await CsrfAsync(teacher);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsJsonAsync("/api/auth/login", new LoginRequest(pending.Email,password))).StatusCode);
        await CsrfAsync(teacher);
        var group = (await (await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("SQL-01"))).Content.ReadFromJsonAsync<GroupView>())!;
        var foreign = (await (await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("SQL-02"))).Content.ReadFromJsonAsync<GroupView>())!;
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(pending.Id,"Teacher"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await teacher.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await teacher.GetAsync($"/api/groups/{group.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/groups/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/groups/{foreign.Id}/members")).StatusCode);
        Assert.Single((await teacher.GetFromJsonAsync<GroupView[]>("/api/groups/"))!);
        Assert.True((await teacher.GetFromJsonAsync<GroupView>($"/api/groups/{group.Id}"))!.CanViewMembers);
        await admin.PutAsJsonAsync($"/api/admin/users/{pending.Id}/roles",new RolesRequest(["Teacher","Student"]));
        await CsrfAsync(teacher);
        await teacher.PostAsJsonAsync("/api/auth/login",new LoginRequest(pending.Email,password));
        await CsrfAsync(teacher);
        await admin.PutAsJsonAsync($"/api/groups/{foreign.Id}/members",new MembershipRequest(pending.Id,"Student"));
        Assert.False((await teacher.GetFromJsonAsync<GroupView>($"/api/groups/{foreign.Id}"))!.CanViewMembers);
        Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/groups/{foreign.Id}/members")).StatusCode);
        await admin.DeleteAsync($"/api/groups/{group.Id}/members/{pending.Id}/Teacher");
        Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/groups/{group.Id}")).StatusCode);
    }

    [Fact]
    public async Task StudentsCannotAccessOtherProfilesRostersOrGroups()
    {
        var (studentA, a) = await RegisterAsync(); using var _a = studentA;
        var (studentB, b) = await RegisterAsync(); using var _b = studentB;
        Assert.Equal(HttpStatusCode.OK,(await studentA.GetAsync($"/api/users/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await studentA.GetAsync($"/api/users/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await studentA.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await studentA.GetAsync("/api/admin/users")).StatusCode);
        using var admin = await AdminAsync();
        var group = (await (await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Privado-01"))).Content.ReadFromJsonAsync<GroupView>())!;
        await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(a.Id,"Student"));
        Assert.Equal(HttpStatusCode.OK,(await studentA.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await studentB.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await studentA.GetAsync($"/api/groups/{group.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.GetAsync("/api/tutoring-sessions")).StatusCode);
    }

    [Fact]
    public async Task CsrfRequiredForLoginRegistrationAndAuthenticatedWrites()
    {
        using var anonymous = TestHost.Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/auth/login",new LoginRequest(AdminEmail,password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/auth/register",new RegisterRequest("csrf@example.invalid",password,"Ficticio","Student"))).StatusCode);
        using var admin = await AdminAsync();
        admin.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Sin-CSRF"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/auth/logout",new {})).StatusCode);
        var (student, _) = await RegisterAsync(); using var _s = student;
        admin.DefaultRequestHeaders.Add("X-XSRF-TOKEN",student.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single());
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("CSRF-ajeno"))).StatusCode);
        await CsrfAsync(admin);
        Assert.Equal(HttpStatusCode.Created,(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Con-CSRF"))).StatusCode);
    }

    [Fact]
    public async Task LogoutAndDisablingAccountInvalidateSessions()
    {
        var (student, user) = await RegisterAsync(); using var _s = student;
        Assert.Equal(HttpStatusCode.NoContent,(await student.PostAsJsonAsync("/api/auth/logout",new {})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await student.GetAsync("/api/me")).StatusCode);
        await CsrfAsync(student);
        var login = await student.PostAsJsonAsync("/api/auth/login",new LoginRequest(user.Email,password));
        var sessionCookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-AulaTutor.Session=")).Split(';')[0];
        await CsrfAsync(student);
        await student.PostAsJsonAsync("/api/auth/logout",new {});
        using var replay = TestHost.Client(factory); replay.DefaultRequestHeaders.Add("Cookie",sessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized,(await replay.GetAsync("/api/me")).StatusCode);
        await CsrfAsync(student); await student.PostAsJsonAsync("/api/auth/login",new LoginRequest(user.Email,password));
        using var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/admin/users/{user.Id}/status",new AccountStatusRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await student.GetAsync("/api/me")).StatusCode);
        await CsrfAsync(student);
        Assert.Equal(HttpStatusCode.Unauthorized,(await student.PostAsJsonAsync("/api/auth/login",new LoginRequest(user.Email,password))).StatusCode);
    }

    [Fact]
    public async Task AdminBootstrapIsExplicitIdempotentAndDoesNotResetPassword()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<AdminBootstrap>();
        Assert.False(await bootstrap.CreateAsync(AdminEmail,"Other-Fictitious!12345","Otro admin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.CreateAsync("other@example.invalid",password,"Otro admin"));
        using var admin = await AdminAsync();
        var me = (await admin.GetFromJsonAsync<UserView>("/api/me"))!;
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/admin/users/{me.Id}/roles",new RolesRequest([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/admin/users/{me.Id}/status",new AccountStatusRequest(false))).StatusCode);
    }

    [Fact]
    public async Task AllProtectedEndpointsRejectAnonymousClients()
    {
        using var anonymous = TestHost.Client(factory);
        foreach (var route in new[]{"/api/me","/api/admin/users","/api/groups/",$"/api/users/{Guid.NewGuid()}",$"/api/groups/{Guid.NewGuid()}",$"/api/groups/{Guid.NewGuid()}/members"})
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/auth/logout",new {})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/groups/",new GroupRequest("Anónimo"))).StatusCode);
        var id = Guid.NewGuid();
        foreach (var request in new[]
        {
            new HttpRequestMessage(HttpMethod.Post,$"/api/admin/users/{id}/teacher-decision") {Content=JsonContent.Create(new TeacherDecisionRequest(true))},
            new HttpRequestMessage(HttpMethod.Put,$"/api/admin/users/{id}/roles") {Content=JsonContent.Create(new RolesRequest(["Admin"]))},
            new HttpRequestMessage(HttpMethod.Put,$"/api/admin/users/{id}/status") {Content=JsonContent.Create(new AccountStatusRequest(false))},
            new HttpRequestMessage(HttpMethod.Put,$"/api/groups/{id}") {Content=JsonContent.Create(new GroupRequest("Grupo"))},
            new HttpRequestMessage(HttpMethod.Delete,$"/api/groups/{id}"),
            new HttpRequestMessage(HttpMethod.Put,$"/api/groups/{id}/members") {Content=JsonContent.Create(new MembershipRequest(id,"Student"))},
            new HttpRequestMessage(HttpMethod.Delete,$"/api/groups/{id}/members/{id}/Student")
        })
        {
            using (request) Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task PublicRegistrationAndStudentsCannotGrantRoles()
    {
        using var anonymous = await AnonymousAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/register",new RegisterRequest("elevation@example.invalid",password,"Ficticio","Admin"))).StatusCode);
        var (student,user) = await RegisterAsync(); using var _ = student;
        Assert.Equal(HttpStatusCode.Forbidden,(await student.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",new RolesRequest(["Admin"]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await student.PutAsJsonAsync($"/api/admin/users/{user.Id}/status",new AccountStatusRequest(false))).StatusCode);
    }

    [Fact]
    public async Task GroupLifecycleRetainsMembershipsAndRejectsInvalidRelations()
    {
        using var admin = await AdminAsync();
        var (student,user) = await RegisterAsync(); using var _s = student;
        var (pending,teacher) = await RegisterAsync("Teacher"); using var _t = pending;
        var group = (await (await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Grupo ciclo"))).Content.ReadFromJsonAsync<GroupView>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync("/api/groups/",new GroupRequest("Grupo ciclo"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(teacher.Id,"Teacher"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(user.Id,"Student"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(user.Id,"Student"))).StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<MembershipView[]>($"/api/groups/{group.Id}/members"))!);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}",new GroupRequest("Grupo renombrado"))).StatusCode);
        Assert.Equal("Grupo renombrado",(await student.GetFromJsonAsync<GroupView>($"/api/groups/{group.Id}"))!.Name);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.DeleteAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await student.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/groups/{group.Id}/members",new MembershipRequest(user.Id,"Student"))).StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<MembershipView[]>($"/api/groups/{group.Id}/members"))!);
    }
}
