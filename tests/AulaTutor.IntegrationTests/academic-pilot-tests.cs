using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private static LessonImportV2 SqlReference() => LearningJson.Read<LessonImportV2>(File.ReadAllText(
        Path.Combine(TestHost.Root, "docs", "pilot", "sql-procedures-v2.json")));
    private static LessonContent SqlContent() => PreloadedRules.Convert(SqlReference());
    private static async Task PilotArtifact(string name, object data)
    {
        Directory.CreateDirectory(Path.Combine(TestHost.Root, "artifacts"));
        await File.WriteAllTextAsync(Path.Combine(TestHost.Root, "artifacts", "iteration-9-" + name + ".json"),
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string SqlAnswer(string id, bool struggling = false) => id switch
    {
        "e-create" => "CREATE PROCEDURE", "e-parameter" => "@CategoriaId", "e-exists" => "Verdadero",
        _ => struggling ? "CREATE PROCEDURE dbo.BuscarProductos AS SELECT Nombre FROM dbo.Productos; No sé cómo incluir el parámetro."
            : SqlReference().Lesson.Exercises.Last().ReferenceSolution
    };

    [Fact]
    public async Task AcademicSqlPilotKeepsIndependentEvidenceLocalCallsAndHumanReview()
    {
        using var f = await AssignedFixture(SqlContent());
        var watch = Stopwatch.StartNew(); var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
        var timings = new List<double>(); var ordinary = 0; var helpOperations = 0;
        provider.Failure = new(AiErrorCode.Unavailable); // Local execution must work even with the provider unavailable.
        async Task<TutoringSessionView> Run(HttpClient client, UserView user, bool struggling)
        {
            var s = await ClassAct(user, await StartClass(client, f.Assignment.Id)); ordinary++;
            for (var i = 0; i < 4; i++)
            {
                var e = ApprovedCurrent(s);
                if (struggling && i < 3)
                {
                    var wrong = i == 0 ? "EXECUTE" : i == 1 ? "int" : "Falso";
                    for (var n = 0; n < (i == 0 ? 2 : 1); n++) { s = await ClassAct(user, s, "SubmitAttempt", wrong, e.Id); ordinary++; }
                }
                var hints = struggling && i < 3 ? (i == 1 ? 1 : 2) : !struggling && i == 1 ? 1 : 0;
                for (var n = 0; n < hints; n++) { s = await ClassAct(user, s, "Hint"); helpOperations++; }
                if (struggling && i == 1) { s = await ClassAct(user, s, "Simplify"); helpOperations++; }
                s = await ClassAct(user, s, "SubmitAttempt", SqlAnswer(e.ApprovedExerciseId!, struggling), e.Id); ordinary++;
                if (i == 1) { s = await ClassTransition(user, s, "pause"); var saved = await Get(client, s.Id); Assert.Equal(s.Revision, saved.Revision); s = await ClassTransition(user, saved, "resume"); }
                if (i < 3) { s = await ClassAct(user, s, "NextActivity"); ordinary++; }
            }
            return await ClassTransition(user, s, "complete");
        }
        var a = await Run(f.A, f.UserA, false); var b = await Run(f.B, f.UserB, true);
        Assert.Equal(0, provider.Calls); Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.AssignedClass!.Class.LessonVersionId, b.AssignedClass!.Class.LessonVersionId);
        Assert.Equal(a.AssignedClass.Class.ContentHash, b.AssignedClass.Class.ContentHash);
        Assert.Equal(4, a.Learning!.Attempts.Length); Assert.Equal(8, b.Learning!.Attempts.Length);
        Assert.Single(a.Learning.Attempts, x => x.Outcome == "NeedsReview"); Assert.Single(b.Learning.Attempts, x => x.Outcome == "NeedsReview");
        Assert.Equal(4, b.Learning.Attempts.Count(x => x.Outcome == "Incorrect"));
        Assert.Empty(a.Learning.Attempts.Select(x => x.Id).Intersect(b.Learning.Attempts.Select(x => x.Id)));
        var originalA = LearningJson.Write(a.Learning.Attempts); var originalB = LearningJson.Write(b.Learning.Attempts);
        provider.Failure = null;
        var key = Guid.NewGuid(); a = await ClassAct(f.UserA, a, "FinalReport", key: key);
        a = await ClassAct(f.UserA, a, "FinalReport", key: key); Assert.Equal(1, provider.Calls);
        provider.Failure = new(AiErrorCode.RateLimited, TimeSpan.FromSeconds(30)); var retryKey = Guid.NewGuid();
        b = await ClassAct(f.UserB, b, "FinalReport", key: retryKey);
        Assert.Single(b.Learning!.Reports); Assert.Equal("Completed", b.State);
        Assert.Equal(429, (await Assert.ThrowsAsync<AppException>(() => ClassAct(f.UserB, b, "FinalReport", key: retryKey, retry: true))).Status);
        clock.Offset = TimeSpan.FromMinutes(1); provider.Failure = null;
        b = await ClassAct(f.UserB, b, "FinalReport", key: retryKey, retry: true); Assert.Equal(3, provider.Calls);
        Assert.Equal(originalA, LearningJson.Write(a.Learning!.Attempts)); Assert.Equal(originalB, LearningJson.Write(b.Learning!.Attempts));
        Assert.Equal(2, a.Learning.Reports.Length); Assert.Equal(2, b.Learning.Reports.Length);
        for (var i = 0; i < provider.Requests.Count; i++)
        {
            var allowed = i == 0 ? a.Learning.Attempts : b.Learning.Attempts;
            using var context = JsonDocument.Parse(provider.Requests[i].Messages.Last().Text);
            var supplied = context.RootElement.GetProperty("evidence").EnumerateArray().ToArray();
            Assert.Equal(allowed.Length, supplied.Length);
            foreach (var row in supplied)
                Assert.Equal(allowed.Single(x => x.Id == row.GetProperty("id").GetGuid()).Answer, row.GetProperty("answer").GetString());
            Assert.Equal(SqlContent().Objectives.Select(o => o.Id), context.RootElement.GetProperty("objectives").EnumerateArray().Select(o => o.GetProperty("id").GetString()));
            Assert.DoesNotContain(f.UserA.Email, provider.Requests[i].Messages.Last().Text);
            Assert.DoesNotContain(f.UserB.Email, provider.Requests[i].Messages.Last().Text);
        }
        var observed = await Evidence(f); Assert.NotEmpty(observed.AiObservations);
        Assert.All(observed.AiObservations, o => Assert.All(o.EvidenceIds, id => Assert.Contains(a.Learning.Attempts, x => x.Id == id)));
        var reviewed = await Review(f, Decision(observed, observed.Exercises.Last().Id)); Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal(originalA, LearningJson.Write((await Get(f.A, a.Id)).Learning!.Attempts));
        Assert.NotEmpty((await f.A.GetFromJsonAsync<TeacherReviewView[]>($"/api/students/me/classes/{f.Assignment.Id}/feedback"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await f.B.GetAsync($"/api/tutoring-sessions/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Teacher.GetAsync($"/api/tutoring-sessions/{a.Id}")).StatusCode);
        var overview = (await Results(f))!; Assert.Equal(2, overview.Assignment.Counts.Completed); Assert.Equal(2, overview.Assignment.Counts.EligibleStudents);
        Assert.Equal(8, overview.Objectives.Sum(o => o.DeliveredRequired)); Assert.Equal(12, overview.Objectives.Sum(o => o.Attempts));
        for (var i = 0; i < 31; i++)
        {
            var sample = Stopwatch.StartNew(); var read = await f.Teacher.GetAsync(IndividualPath(f)); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            if (i > 0) timings.Add(sample.Elapsed.TotalMilliseconds);
        }
        timings.Sort(); double P(double q) => timings[(int)Math.Ceiling(q * timings.Count) - 1];
        await PilotArtifact("academic", new { status = "passed", transport = "TestServer HTTP + real isolated PostgreSQL; controlled Mock, zero provider HTTP", students = 2,
            requiredExercisesPerStudent = 4, attempts = new[] { 4, 8 }, incorrectAttempts = new[] { 0, 4 }, needsReview = new[] { 1, 1 },
            ordinaryOperations = ordinary, helpOperations, ordinaryProviderInvocations = 0, hintProviderInvocations = 0, evaluationProviderInvocations = 3,
            successfulEvaluations = 2, controlledProviderErrors = 1, explicitEvaluationRetries = 1, duplicateAttempts = 0, duplicateEvaluations = 0,
            readSamples = timings.Count, warmupReadsExcluded = 1, readSuccess = timings.Count, readErrors = 0, p50Ms = P(.5), p95Ms = P(.95), percentileMethod = "nearest rank",
            backendAndTestCpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds, workingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64,
            elapsedMs = watch.Elapsed.TotalMilliseconds, note = "Two scripted students, one group/version; no capacity, learning or external provider claims." });
    }

    [Fact]
    public async Task AcademicSqlConversationalPilotPreservesReinforcementClosureAndPrivacy()
    {
        using var f = await AssignedFixture(SqlContent() with { Preloaded = null });
        var watch = Stopwatch.StartNew();
        var a = await ClassAct(f.UserA, await StartClass(f.A, f.Assignment.Id));
        var b = await ClassAct(f.UserB, await StartClass(f.B, f.Assignment.Id));
        a = await ClassAct(f.UserA, a, "SubmitAttempt", "EXECUTE", ApprovedCurrent(a).Id);
        Assert.Equal("Incorrect", a.Learning!.Attempts.Single().Outcome);
        a = await ClassAct(f.UserA, a, "Hint"); a = await ClassAct(f.UserA, a, "GenerateExercise");
        Assert.Single(a.Learning!.Exercises, e => e.IsComplementary && !e.Required);
        foreach (var student in new[] { (Client: f.A, User: f.UserA, Session: a), (Client: f.B, User: f.UserB, Session: b) })
        {
            var s = student.Session;
            for (var i = 0; i < 4; i++)
            {
                var e = ApprovedCurrent(s); s = await ClassAct(student.User, s, "SubmitAttempt", SqlAnswer(e.ApprovedExerciseId!), e.Id);
                if (i == 1) { s = await ClassTransition(student.User, s, "pause"); s = await ClassTransition(student.User, await Get(student.Client, s.Id), "resume"); }
                if (i < 3) s = await ClassAct(student.User, s, "NextActivity");
            }
            s = await ClassAct(student.User, s, "FinalReport"); var calls = provider.Calls;
            s = await ClassTransition(student.User, s, "complete"); Assert.Equal(calls, provider.Calls); Assert.Equal("Completed", s.State);
            Assert.Contains(s.Learning!.Reports, r => r.ProviderId == "Application"); Assert.Contains(s.Learning.Reports, r => r.ProviderId == "Mock");
        }
        var own = await Create(f.A);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Teacher.GetAsync($"/api/tutoring-sessions/{own.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Admin.GetAsync($"/api/tutoring-sessions/{own.Id}")).StatusCode);
        var overview = (await Results(f))!; Assert.Equal(2, overview.Assignment.Counts.Completed);
        Assert.Equal(9, overview.Objectives.Sum(o => o.Attempts)); Assert.Equal(8, overview.Objectives.Sum(o => o.DeliveredRequired));
        Assert.All(provider.Requests, r => Assert.DoesNotContain("referenceSolution", r.Messages.Last().Text));
        await PilotArtifact("conversational", new { status = "passed", students = 2, completed = 2, primaryAttempts = 9, reinforcementExercises = 1,
            providerInvocations = provider.Calls, byUseCase = provider.Requests.GroupBy(r => r.UseCase).ToDictionary(g => g.Key, g => g.Count()), elapsedMs = watch.Elapsed.TotalMilliseconds,
            note = "Mock proves workflow and evidence integrity, not SQL feedback quality. All SQL remains text." });
    }

    [Fact]
    public async Task AcademicSqlCreationPathsSharePublishedVersionAndAssignmentLifecycle()
    {
        var content = SqlContent(); using var f = await AssignedFixture(content with { Preloaded = null });
        var generated = await CreateLesson(f.Teacher, content with { Preloaded = null, Title = "Preparación integrada SQL ficticia" });
        generated = await Design(f.Teacher, generated); Assert.Equal(1, provider.Calls);
        generated = await Command(f.Teacher, generated, "apply-proposal", generated.Proposals.Single().Id);
        // Mock intentionally produces a generic script; teacher review replaces it with the authored SQL reference.
        var saved = await f.Teacher.PutAsJsonAsync($"/api/lessons/{generated.Id}/draft", new SaveLessonRequest(generated.Revision, content));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); generated = (await saved.Content.ReadFromJsonAsync<LessonView>())!;
        generated = await Command(f.Teacher, generated, "review"); generated = await Command(f.Teacher, generated, "approve");
        generated = await Command(f.Teacher, generated, "publish", generated.Versions.Single().Id);
        var imported = await f.Teacher.PostAsJsonAsync("/api/lessons/import", new CreateImportedLessonRequest(Guid.NewGuid(), LearningJson.Write(SqlReference())));
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode); var external = (await imported.Content.ReadFromJsonAsync<LessonView>())!;
        external = await Command(f.Teacher, external, "review"); external = await Command(f.Teacher, external, "approve");
        external = await Command(f.Teacher, external, "publish", external.Versions.Single().Id);
        var second = await Assign(f.Teacher, new(Guid.NewGuid(), generated.Versions.Single().Id, f.Group.Id));
        var third = await Assign(f.Teacher, new(Guid.NewGuid(), external.Versions.Single().Id, f.Group.Id));
        Assert.Equal(generated.Versions.Single().ContentHash, external.Versions.Single().ContentHash);
        Assert.Null(f.Lesson.Versions.Single().Content.Preloaded); Assert.NotNull(generated.Versions.Single().Content.Preloaded);
        Assert.Equal(3, (await f.Teacher.GetFromJsonAsync<AssignmentView[]>("/api/teacher/assignments/"))!.Length);
        Assert.NotEqual(second.Id, third.Id); Assert.Equal(1, provider.Calls);
        await PilotArtifact("creation", new { status = "passed", paths = 3, publishedVersions = 3, assignedVersions = 3,
            generationProviderInvocations = 1, importProviderInvocations = 0, note = "Same lifecycle; authored SQL replaces generic Mock content after explicit teacher review." });
    }
}
