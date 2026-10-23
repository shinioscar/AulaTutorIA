using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Lessons;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
    }
    [Fact] public async Task DashboardQueryCountDoesNotGrowWithAssignmentsOrGroups()
    {
        using var f = await AssignedFixture();
        await using var scope = factory.Services.CreateAsyncScope(); var original = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var counter = new QueryCounter();
        var options = new DbContextOptionsBuilder<AulaTutorDbContext>().UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(counter).Options;
        async Task<(int Count, SupervisionDashboard Data)> Read()
        {
            await using var db = new AulaTutorDbContext(options); counter.Count = 0;
            var result = await new SupervisionService(db, new ClassActor(f.TeacherUser), clock, new AssignedClassAccess(db, clock)).DashboardAsync(null, CancellationToken.None);
            return (counter.Count, result);
        }
        var one = await Read();
        var g = (await (await f.Admin.PostAsJsonAsync("/api/groups/", new GroupRequest("Segundo grupo ficticio"))).Content.ReadFromJsonAsync<GroupView>())!;
        await f.Admin.PutAsJsonAsync($"/api/groups/{g.Id}/members", new MembershipRequest(f.TeacherUser.Id, "Teacher"));
        await f.Admin.PutAsJsonAsync($"/api/groups/{g.Id}/members", new MembershipRequest(f.UserB.Id, "Student"));
        var assignment = await Assign(f.Teacher, new(Guid.NewGuid(), f.Lesson.Versions[0].Id, g.Id));
        var two = await Read();
        Assert.Equal(one.Count, two.Count); Assert.Equal(2, two.Data.Assignments.Length); Assert.Equal(2, two.Data.Groups.Length);
        Assert.Equal(3, two.Data.Groups.Sum(x => x.EligibleStudents));
        Directory.CreateDirectory(Path.Combine(TestHost.Root, "artifacts"));
        await File.WriteAllTextAsync(Path.Combine(TestHost.Root, "artifacts", "iteration-8-dashboard-queries.json"), JsonSerializer.Serialize(new { oneAssignment = one.Count, twoAssignmentsTwoGroups = two.Count, invariant = "same cohort and permission rules" }));
    }
    [Fact] public async Task BoundedMockLoadMeasuresConcurrentStartsDuplicateReplaysAndIndependentStudents()
    {
        using var f = await AssignedFixture();
        var clients = new List<(HttpClient Client, UserView User)> { (f.A, f.UserA), (f.B, f.UserB) };
        for (var i = 0; i < 3; i++)
        {
            var extra = await Register(); clients.Add(extra);
            Assert.Equal(HttpStatusCode.NoContent, (await f.Admin.PutAsJsonAsync($"/api/groups/{f.Group.Id}/members", new MembershipRequest(extra.User.Id, "Student"))).StatusCode);
        }
        var process = Process.GetCurrentProcess(); var beforeCpu = process.TotalProcessorTime;
        var results = new List<object>();
        try
        {
            foreach (var size in new[] { 1, 3, 5 })
            {
                var start = Stopwatch.StartNew();
                var requests = await Task.WhenAll(clients.Take(size).SelectMany(pair => Enumerable.Range(0, 2).Select(async _ => {
                    var watch = Stopwatch.StartNew();
                    using var response = await pair.Client.PostAsJsonAsync($"/api/students/me/classes/{f.Assignment.Id}/start", new StartAssignedClassRequest(Guid.NewGuid()));
                    var text = await response.Content.ReadAsStringAsync();
                    return new { Ms = watch.Elapsed.TotalMilliseconds, Status = (int)response.StatusCode, Body = text };
                })));
                Assert.All(requests, r => Assert.Equal(200, r.Status));
                var times = requests.Select(r => r.Ms).Order().ToArray();
                double P(double q) => times[Math.Clamp((int)Math.Ceiling(q * times.Length) - 1, 0, times.Length - 1)];
                await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
                var sessions = await db.TutoringSessions.Where(s => s.AssignmentId == f.Assignment.Id).ToArrayAsync();
                Assert.Equal(size, sessions.Length); Assert.Equal(size, sessions.Select(s => s.StudentId).Distinct().Count());
                var lockWaiters = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'").SingleAsync();
                results.Add(new { users = size, requests = requests.Length, success = requests.Length, failed = 0,
                    p50Ms = P(.50), p95Ms = P(.95), elapsedMs = start.Elapsed.TotalMilliseconds,
                    duplicateSessions = sessions.Length - sessions.Select(s => s.StudentId).Distinct().Count(), providerCalls = provider.Calls,
                    errorsByType = new Dictionary<string,int>(), backendCpuMs = (process.TotalProcessorTime - beforeCpu).TotalMilliseconds,
                    databaseLockWaitersAtStageEnd = lockWaiters,
                    backendWorkingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64 });
            }
            // Explicit Mock generation/answers: independent actors, same approved version.
            var sessionsAfter = await Task.WhenAll(clients.Select(async pair => {
                var session = await ClassAct(pair.User, await StartClass(pair.Client, f.Assignment.Id));
                session = await ClassAct(pair.User, session, "SubmitAttempt", "Falso", ApprovedCurrent(session).Id);
                return session;
            }));
            Assert.Equal(5, sessionsAfter.Select(s => s.Id).Distinct().Count());
            Assert.All(sessionsAfter, s => Assert.Single(s.Learning!.Attempts));
            var overview = (await Results(f))!; Assert.Equal(5, overview.Assignment.Counts.Started);
            Directory.CreateDirectory(Path.Combine(TestHost.Root, "artifacts"));
            await File.WriteAllTextAsync(Path.Combine(TestHost.Root, "artifacts", "iteration-8-load.json"), JsonSerializer.Serialize(new {
                transport = "ASP.NET TestServer HTTP in-process + real isolated PostgreSQL; Mock; no external network",
                stages = results, note = "Small bounded scenario, not capacity certification or Groq performance. PostgreSQL lock wait is bounded/tested separately; CPU includes in-process test host.",
                independentStudentsWithOneAttempt = sessionsAfter.Length, incorrectDuplicates = 0 }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { foreach (var pair in clients.Skip(2)) pair.Client.Dispose(); }
    }
}
