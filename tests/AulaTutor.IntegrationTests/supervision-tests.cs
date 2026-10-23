using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private const string SupervisionRoot = "/api/teacher/supervision";
    private static string IndividualPath(ClassFixture f, Guid? student = null) => $"{SupervisionRoot}/assignments/{f.Assignment.Id}/students/{student ?? f.UserA.Id}";
    private static Task<ClassSupervision?> Results(ClassFixture f) => f.Teacher.GetFromJsonAsync<ClassSupervision>($"{SupervisionRoot}/assignments/{f.Assignment.Id}");
    private static async Task<IndividualSupervision> Evidence(ClassFixture f, Guid? student = null) => (await f.Teacher.GetFromJsonAsync<IndividualSupervision>(IndividualPath(f, student)))!;
    private static ReviewRequest Decision(IndividualSupervision d, Guid? exercise, string state = "Reviewed", string attention = "Closed", bool flagged = false) =>
        new(Guid.NewGuid(), d.Reviews.SingleOrDefault(r => r.ExerciseId == exercise)?.Revision ?? 0, d.EvidenceRevision, exercise, state, attention, "NeedsReinforcement", flagged,
            "Revisé la evidencia original.", "Necesita explicar el criterio con un ejemplo.", "Comparar dos casos antes de continuar.",
            d.Exercises.Where(e => !exercise.HasValue || e.Id == exercise).SelectMany(e => e.Attempts).Select(a => a.Id).TakeLast(10).ToArray());
    private static Task<HttpResponseMessage> Review(ClassFixture f, ReviewRequest r) => f.Teacher.PostAsJsonAsync(IndividualPath(f) + "/reviews", r);
    private async Task<TutoringSessionView> OpenResponse(ClassFixture f)
    {
        var s = await ClassAct(f.UserA, await StartClass(f.A, f.Assignment.Id));
        s = await ClassAct(f.UserA, s, "SubmitAttempt", "Falso", ApprovedCurrent(s).Id);
        s = await ClassAct(f.UserA, s, "NextActivity");
        return await ClassAct(f.UserA, s, "SubmitAttempt", "IF EXISTS: evidencia abierta ficticia", ApprovedCurrent(s).Id);
    }
    [Fact] public async Task SupervisionDashboardUsesCurrentTeacherGroupsAndRealCohort()
    {
        using var f = await AssignedFixture(); var d = (await f.Teacher.GetFromJsonAsync<SupervisionDashboard>(SupervisionRoot + "/"))!;
        Assert.Equal(2, Assert.Single(d.Groups).EligibleStudents); Assert.Single(d.Assignments); Assert.Equal(1, d.PublishedVersions);
        Assert.Equal(2, d.Assignments[0].Counts.NotOpened); Assert.Equal(0, d.Assignments[0].Counts.Started); Assert.Equal(0, provider.Calls);
        using var other = await Teacher(); Assert.Empty((await other.GetFromJsonAsync<SupervisionDashboard>(SupervisionRoot + "/"))!.Groups);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(SupervisionRoot + $"/?groupId={f.Group.Id}")).StatusCode);
    }
    [Fact] public async Task CoTeacherAdminAndOtherStudentCannotReadOrReviewAssignedEvidence()
    {
        using var f = await AssignedFixture(); using var other = await Teacher(); var user = (await other.GetFromJsonAsync<UserView>("/api/me"))!;
        await f.Admin.PutAsJsonAsync($"/api/groups/{f.Group.Id}/members", new MembershipRequest(user.Id,"Teacher"));
        foreach (var c in new[] { other, f.Admin, f.A, f.B })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(IndividualPath(f))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{SupervisionRoot}/assignments/{f.Assignment.Id}")).StatusCode);
        }
        await OpenResponse(f); var d = await Evidence(f); var r = Decision(d, d.Exercises[1].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(IndividualPath(f)+"/reviews", r)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Teacher.GetAsync(IndividualPath(f, Guid.NewGuid()))).StatusCode);
    }
    [Fact] public async Task ParticipationAndObjectiveMetricsDoNotDoubleCountStudentsOrExercises()
    {
        using var f = await AssignedFixture(); var a = await OpenResponse(f); a = await ClassTransition(f.UserA, a, "complete");
        var b = await ClassAct(f.UserB, await StartClass(f.B, f.Assignment.Id));
        for (var i=0;i<3;i++) b = await ClassAct(f.UserB,b,"SubmitAttempt","Verdadero",ApprovedCurrent(b).Id);
        b = await ClassAct(f.UserB,b,"Hint"); b = await ClassAct(f.UserB,b,"SubmitAttempt","Falso",ApprovedCurrent(b).Id); await ClassTransition(f.UserB,b,"pause");
        var report = (await Results(f))!; var counts = report.Assignment.Counts; var objective = Assert.Single(report.Objectives);
        Assert.Equal(2,counts.EligibleStudents);Assert.Equal(2,counts.Started);Assert.Equal(1,counts.Completed);Assert.Equal(1,counts.Paused);
        Assert.Equal(2,counts.AllRequiredDelivered);Assert.Equal(1,counts.PendingReviews);
        Assert.Equal(2,objective.WorkedStudents);Assert.Equal(3,objective.SubmittedExercises);Assert.Equal(6,objective.Attempts);
        Assert.Equal(1,objective.VerifiedWithoutHelp);Assert.Equal(1,objective.VerifiedWithHelp);Assert.Equal(3,objective.IncorrectAttempts);Assert.Equal(1,objective.NeedsReviewAttempts);
        var first=report.Exercises.Single(e=>e.Id=="e1");Assert.Equal(1,first.IncorrectStudents);Assert.Equal(3,first.IncorrectAttempts);Assert.Equal(2,first.SubmittedStudents);
        Assert.DoesNotContain(report.Cases,c=>c.StudentId==f.UserB.Id&&!c.Resolved);
    }
    [Fact] public async Task OpeningWithoutConversationDoesNotClaimParticipationOrDelivery()
    { using var f=await AssignedFixture();await StartClass(f.A,f.Assignment.Id);var c=(await Results(f))!.Assignment.Counts;Assert.Equal(1,c.Opened);Assert.Equal(0,c.Started);Assert.Equal(1,c.NotOpened);Assert.Equal(0,c.AllRequiredDelivered);Assert.Equal(2,c.WithPendingRequired); }
    [Theory] [InlineData("OpenResponse")] [InlineData("Code")]
    public async Task HumanReviewsAppendHistoryAndStudentFeedbackDoesNotChangeEvidence(string kind)
    {
        var content=Content();var section=content.Sections[1];var activity=section.Exercises[0];
        content=content with {Sections=[content.Sections[0],section with {Exercises=[activity with {Activity=activity.Activity with {Kind=kind}}]}]};
        using var f=await AssignedFixture(content);await OpenResponse(f);var d=await Evidence(f);var e=d.Exercises[1];var original=LearningJson.Write(e.Attempts);
        Assert.Equal(kind,e.Kind);
        Assert.Equal(["Verdadero","Falso"],d.Exercises[0].Options);
        var r=Decision(d,e.Id,"InReview","InProgress");Assert.Equal(HttpStatusCode.OK,(await Review(f,r)).StatusCode);
        d=await Evidence(f);Assert.Equal(1,Assert.Single(d.Reviews).Revision);Assert.Equal(1,Assert.Single(d.Cases).PendingReview?1:0);
        r=Decision(d,e.Id);Assert.Equal(HttpStatusCode.OK,(await Review(f,r)).StatusCode);d=await Evidence(f);
        Assert.Equal(2,Assert.Single(d.Reviews).History.Length);Assert.Equal(original,LearningJson.Write(d.Exercises[1].Attempts));
        Assert.Equal("NeedsReview",d.Exercises[1].Attempts[0].Outcome);Assert.False(Assert.Single(d.Cases).PendingReview);Assert.True(d.Cases[0].Resolved);
        var feedback=(await f.A.GetFromJsonAsync<TeacherReviewView[]>($"/api/students/me/classes/{f.Assignment.Id}/feedback"))!;
        Assert.Equal(f.TeacherUser.Id,Assert.Single(feedback).History[0].AuthorId);Assert.Equal(2,feedback[0].History.Length);
        Assert.Empty((await f.B.GetFromJsonAsync<TeacherReviewView[]>($"/api/students/me/classes/{f.Assignment.Id}/feedback"))!);
    }
    [Fact] public async Task ReviewRejectsForeignExerciseAndAttemptReferences()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var b=await ClassAct(f.UserB,await StartClass(f.B,f.Assignment.Id));b=await ClassAct(f.UserB,b,"SubmitAttempt","Falso",ApprovedCurrent(b).Id);
        var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await Review(f,r with {EvidenceIds=[b.Learning!.Attempts[0].Id]})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Review(f,r with {EvidenceIds=[d.Exercises[0].Attempts[0].Id]})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Review(f,r with {ExerciseId=b.Learning.Exercises[0].Id})).StatusCode);
    }
    [Fact] public async Task ReviewRequiresGroundingValidStatesAndCurrentEvidence()
    {
        using var f=await AssignedFixture();var s=await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        foreach(var invalid in new[]{r with {EvidenceIds=[]},r with {Observation=""},r with {State="2"},r with {Attention="Unknown"},r with {Comment=new string('x',1001)}})
            Assert.Equal(HttpStatusCode.BadRequest,(await Review(f,invalid)).StatusCode);
        await ClassAct(f.UserA,s,"SubmitAttempt","Otra respuesta abierta",ApprovedCurrent(s).Id);
        Assert.Equal(HttpStatusCode.Conflict,(await Review(f,r)).StatusCode);
        d=await Evidence(f);r=Decision(d,d.Exercises[1].Id);Assert.Equal(HttpStatusCode.BadRequest,(await Review(f,r with {EvidenceIds=[r.EvidenceIds[0]]})).StatusCode);
    }
    [Fact] public async Task DuplicateConcurrentReviewHasOneDurableDecisionAndRejectsKeyReuse()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        var replies=await Task.WhenAll(Review(f,r),Review(f,r));Assert.All(replies,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));
        Assert.Single(Assert.Single((await Evidence(f)).Reviews).History);
        Assert.Equal(HttpStatusCode.Conflict,(await Review(f,r with {Comment="Otra decisión con la misma clave"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Review(f,r with {RequestId=Guid.NewGuid()})).StatusCode);
    }
    [Fact] public async Task ConcurrentDifferentDecisionsRequireFreshReviewRevision()
    { using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);var responses=await Task.WhenAll(Review(f,r),Review(f,r with {RequestId=Guid.NewGuid(),Comment="Segunda decisión"}));Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict); }
    [Fact] public async Task RemovedStudentDisappearsFromCohortButHistoryReturnsWithMembership()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);await Review(f,Decision(d,d.Exercises[1].Id));
        await f.Admin.DeleteAsync($"/api/groups/{f.Group.Id}/members/{f.UserA.Id}/Student");
        Assert.Equal(1,(await Results(f))!.Assignment.Counts.EligibleStudents);Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync(IndividualPath(f))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.A.GetAsync($"/api/students/me/classes/{f.Assignment.Id}/feedback")).StatusCode);
        await f.Admin.PutAsJsonAsync($"/api/groups/{f.Group.Id}/members",new MembershipRequest(f.UserA.Id,"Student"));Assert.Single((await Evidence(f)).Reviews);
    }
    [Fact] public async Task InactiveStudentIsExcludedFromAllSupervisionAggregates()
    { using var f=await AssignedFixture();await OpenResponse(f);await f.Admin.PutAsJsonAsync($"/api/admin/users/{f.UserA.Id}/status",new AccountStatusRequest(false));var r=(await Results(f))!;Assert.Equal(1,r.Assignment.Counts.EligibleStudents);Assert.Equal(0,r.Objectives[0].Attempts);Assert.Empty(r.Cases);Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync(IndividualPath(f))).StatusCode); }
    [Theory] [InlineData("group")] [InlineData("teacher")]
    public async Task LostAcademicRelationshipHidesHistoricalReviews(string target)
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);await Review(f,Decision(d,d.Exercises[1].Id));
        await f.Admin.DeleteAsync(target=="group"?$"/api/groups/{f.Group.Id}":$"/api/groups/{f.Group.Id}/members/{f.TeacherUser.Id}/Teacher");
        Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync(IndividualPath(f))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Review(f,Decision(d,d.Exercises[1].Id))).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Single(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().TeacherReviewEntries.ToArrayAsync());
    }
    [Theory] [InlineData("deactivate",true)] [InlineData("withdraw",false)] [InlineData("schedule",true)]
    public async Task AssignmentAvailabilityAndWithdrawalHaveExplicitSupervisionPolicy(string action,bool visible)
    { using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);await AssignmentCommand(f.Teacher,f.Assignment,action,action=="schedule"?clock.GetUtcNow().AddDays(1):null);Assert.Equal(visible?HttpStatusCode.OK:HttpStatusCode.NotFound,(await f.Teacher.GetAsync(IndividualPath(f))).StatusCode);Assert.Equal(visible?HttpStatusCode.OK:HttpStatusCode.NotFound,(await Review(f,Decision(d,d.Exercises[1].Id))).StatusCode);Assert.Equal(HttpStatusCode.OK,(await f.A.GetAsync($"/api/students/me/classes/{f.Assignment.Id}/feedback")).StatusCode); }
    [Fact] public async Task ArchivedPublicationStillAllowsHistoricalReviewWithCurrentRelationship()
    { using var f=await AssignedFixture();await OpenResponse(f);await Command(f.Teacher,f.Lesson,"archive");var d=await Evidence(f);Assert.Equal(HttpStatusCode.OK,(await Review(f,Decision(d,d.Exercises[1].Id))).StatusCode); }
    [Fact] public async Task RecurrentErrorsReopenAfterNewEvidenceAndExhaustionNeverApproves()
    {
        using var f=await AssignedFixture();var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));
        for(var i=0;i<3;i++)s=await ClassAct(f.UserA,s,"SubmitAttempt","Verdadero",ApprovedCurrent(s).Id);
        var d=await Evidence(f);Assert.Contains("RepeatedErrors",Assert.Single(d.Cases).Reasons);await Review(f,Decision(d,d.Exercises[0].Id));Assert.True(Assert.Single((await Evidence(f)).Cases).Resolved);
        s=await ClassAct(f.UserA,s,"SubmitAttempt","Verdadero",ApprovedCurrent(s).Id);Assert.False(Assert.Single((await Evidence(f)).Cases).Resolved);
        for(var i=4;i<10;i++)s=await ClassAct(f.UserA,s,"SubmitAttempt","Verdadero",ApprovedCurrent(s).Id);
        d=await Evidence(f);Assert.Contains("AttemptsExhausted",Assert.Single(d.Cases).Reasons);await Review(f,Decision(d,d.Exercises[0].Id));
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"SubmitAttempt","Falso",ApprovedCurrent(s).Id))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>ClassAct(f.UserA,s,"NextActivity"))).Status);
        Assert.Equal(10,(await Evidence(f)).Exercises[0].Attempts.Length);Assert.Equal(0,(await Results(f))!.Assignment.Counts.AllRequiredDelivered);
    }
    [Fact] public async Task TeacherCanFlagSpecificEvidenceAndLeaveGeneralClassFeedback()
    {
        using var f=await AssignedFixture();var s=await ClassAct(f.UserA,await StartClass(f.A,f.Assignment.Id));s=await ClassAct(f.UserA,s,"SubmitAttempt","Falso",ApprovedCurrent(s).Id);
        var d=await Evidence(f);var r=Decision(d,d.Exercises[0].Id,"FollowUpNeeded","Pending",true);await Review(f,r);
        Assert.Contains("TeacherFlagged",Assert.Single((await Evidence(f)).Cases).Reasons);
        d=await Evidence(f);Assert.Equal(HttpStatusCode.OK,(await Review(f,Decision(d,null))).StatusCode);Assert.Equal(2,(await Evidence(f)).Reviews.Length);
    }
    [Fact] public async Task SupervisionNeverLeaksChatPrivateKeysOrAutonomousTutoring()
    {
        using var f=await AssignedFixture();var s=await OpenResponse(f);s=await ClassAct(f.UserA,s,"Message","CHAT_PRIVADO_NO_PROYECTAR");var personal=await Create(f.A);
        var json=await f.Teacher.GetStringAsync(IndividualPath(f));Assert.DoesNotContain("CHAT_PRIVADO_NO_PROYECTAR",json);Assert.DoesNotContain("REFERENCIA_PRIVADA",json);Assert.DoesNotContain("acceptedAnswers",json);Assert.DoesNotContain("contextJson",json);Assert.DoesNotContain("messages",json);
        foreach(var c in new[]{f.Teacher,f.Admin})Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/tutoring-sessions/{personal.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync($"{SupervisionRoot}/assignments/{personal.Id}")).StatusCode);
    }
    [Fact] public async Task NewPublishedVersionDoesNotMixEvidenceOrObjectives()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var l=await Command(f.Teacher,f.Lesson,"new-draft");
        l=await Command(f.Teacher,l,"review");l=await Command(f.Teacher,l,"approve");l=await Command(f.Teacher,l,"publish",l.Versions.Last().Id);
        var a=await Assign(f.Teacher,new(Guid.NewGuid(),l.Versions.Last().Id,f.Group.Id));
        var old=(await Results(f))!;var fresh=(await f.Teacher.GetFromJsonAsync<ClassSupervision>($"{SupervisionRoot}/assignments/{a.Id}"))!;
        Assert.Equal(2,old.Objectives[0].Attempts);Assert.Equal(0,fresh.Objectives[0].Attempts);Assert.Equal(2,fresh.Assignment.Counts.NotOpened);Assert.NotEqual(old.Assignment.LessonVersionId,fresh.Assignment.LessonVersionId);
    }
    [Fact] public async Task ReviewsAreImmutableAndPersistenceFailureCanBeRetriedAtomically()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        await using(var scope=factory.Services.CreateAsyncScope())
        {var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_review_fixture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled persistence failure'; END; $$; CREATE TRIGGER fail_review_fixture BEFORE INSERT ON \"TeacherReviewEntries\" FOR EACH ROW EXECUTE FUNCTION fail_review_fixture();");}
        Assert.Equal(HttpStatusCode.Conflict,(await Review(f,r)).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();Assert.Empty(await db.TeacherReviews.ToArrayAsync());await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_review_fixture ON \"TeacherReviewEntries\"; DROP FUNCTION fail_review_fixture();");}
        Assert.Equal(HttpStatusCode.OK,(await Review(f,r)).StatusCode);d=await Evidence(f);Assert.Single(Assert.Single(d.Reviews).History);
        await using(var scope=factory.Services.CreateAsyncScope())
        {var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();var entry=d.Reviews[0].History[0];await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TeacherReviewEntries\" SET \"Comment\"='Alterado' WHERE \"Id\"={entry.Id}"));await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ReviewEvidence\" WHERE \"EntryId\"={entry.Id}"));}
    }
    [Fact] public async Task SupervisionRequiresAuthenticationCsrfAndNoProviderAvailability()
    {
        using var f=await AssignedFixture();using var anonymous=TestHost.Client(factory);
        foreach(var path in new[]{SupervisionRoot+"/",IndividualPath(f),$"{SupervisionRoot}/assignments/{f.Assignment.Id}",$"/api/students/me/classes/{f.Assignment.Id}/feedback"})Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path)).StatusCode);
        await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(IndividualPath(f)+"/reviews",r)).StatusCode);
        provider.Failure=new(AulaTutor.Application.Ai.AiErrorCode.Unavailable);var calls=provider.Calls;
        Assert.Equal(HttpStatusCode.OK,(await Review(f,r)).StatusCode);await Results(f);await Evidence(f);Assert.Equal(calls,provider.Calls);
        using var openApi=System.Text.Json.JsonDocument.Parse(await anonymous.GetStringAsync("/openapi/v1.json"));
        var operation=openApi.RootElement.GetProperty("paths").GetProperty("/api/teacher/supervision/assignments/{id}/students/{studentId}/reviews").GetProperty("post");
        Assert.True(operation.GetProperty("responses").TryGetProperty("200",out _));Assert.False(operation.GetProperty("responses").TryGetProperty("204",out _));
        f.Teacher.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Assert.Equal(HttpStatusCode.BadRequest,(await Review(f,r)).StatusCode);
    }
    [Theory] [InlineData("status")] [InlineData("role")] [InlineData("approval")]
    public async Task RevokedTeacherCannotUseOldCookieToReadOrWriteReviews(string change)
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);await Review(f,Decision(d,d.Exercises[1].Id));
        if(change=="status")await f.Admin.PutAsJsonAsync($"/api/admin/users/{f.TeacherUser.Id}/status",new AccountStatusRequest(false));
        else if(change=="role")await f.Admin.PutAsJsonAsync($"/api/admin/users/{f.TeacherUser.Id}/roles",new RolesRequest([]));
        else await f.Admin.PostAsJsonAsync($"/api/admin/users/{f.TeacherUser.Id}/teacher-decision",new TeacherDecisionRequest(false));
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Teacher.GetAsync(IndividualPath(f))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Review(f,Decision(d,d.Exercises[1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.A.GetAsync($"/api/students/me/classes/{f.Assignment.Id}/feedback")).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();Assert.Single(await scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>().TeacherReviewEntries.ToArrayAsync());
    }
    [Fact] public async Task AiObservationsRequireClassEvidenceAndStaySeparateFromVerifiedFeedback()
    {
        using var f=await AssignedFixture();var s=await OpenResponse(f);var d=await Evidence(f);var id=d.Exercises[1].Attempts[0].Id;
        Assert.NotNull(d.Exercises[1].Attempts[0].AiFeedback);Assert.DoesNotContain("Interpretación IA",d.Exercises[1].Attempts[0].AutomaticFeedback);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();db.LearningReports.Add(new(){SessionId=s.Id,Version=1,EvidenceRevision=0,ProviderId="Mock",ModelId="controlled",PromptVersion="learning-v2",GeneratedAt=clock.GetUtcNow(),
                StatisticsJson=LearningJson.Write(new LearningStatistics(2,2,1,0,0,1,0,0)),ContentJson=LearningJson.Write(new ReportContent("Resumen no proyectado",[new("Observación referenciada",[id]),new("Referencia ajena no proyectada",[Guid.NewGuid()])],[],[new("Reforzar IF EXISTS",[id]),new("General sin evidencia",[])]))});await db.SaveChangesAsync();
        }
        d=await Evidence(f);Assert.Equal(2,d.AiObservations.Length);Assert.All(d.AiObservations,o=>{Assert.Equal("Mock",o.ProviderId);Assert.True(o.Outdated);Assert.Equal(id,Assert.Single(o.EvidenceIds));});
        var text=await f.Teacher.GetStringAsync(IndividualPath(f));Assert.DoesNotContain("Resumen no proyectado",text);Assert.DoesNotContain("Referencia ajena no proyectada",text);Assert.DoesNotContain("General sin evidencia",text);
    }
    [Fact] public async Task DatabaseRejectsRewritingPrimaryEvidenceAndReviewingPrivateSessions()
    {
        using var f=await AssignedFixture();await OpenResponse(f);var d=await Evidence(f);var attempt=d.Exercises[0].Attempts[0];var personal=await Create(f.A);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExerciseAttempts\" SET \"Answer\"='Alterada' WHERE \"Id\"={attempt.Id}"));
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExerciseAttempts\" SET \"Outcome\"=2 WHERE \"Id\"={attempt.Id}"));
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ExerciseAttempts\" WHERE \"Id\"={attempt.Id}"));
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TeacherReviews\" (\"Id\",\"SessionId\",\"TargetId\",\"Revision\") VALUES ({Guid.NewGuid()},{personal.Id},{personal.Id},1)"));
        Assert.Equal(attempt.Answer,(await Evidence(f)).Exercises[0].Attempts[0].Answer);
    }
    [Fact] public async Task SupervisionDoesNotReplaceConsentForPersonalReportSharing()
    {
        using var f=await AssignedFixture();var personal=await Create(f.A);personal=await ClassAct(f.UserA,personal,"DiagnosticQuestion");
        personal=await ClassAct(f.UserA,personal,"SubmitAttempt","Respuesta privada ficticia",personal.Learning!.Exercises[0].Id);personal=await ClassTransition(f.UserA,personal,"complete");personal=await ClassAct(f.UserA,personal,"FinalReport");
        var report=personal.Learning!.Reports[0];Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
        var response=await f.A.PostAsJsonAsync($"/api/tutoring-sessions/{personal.Id}/shares",new ShareReportRequest(Guid.NewGuid(),report.Id,f.TeacherUser.Id,true));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var share=(await response.Content.ReadFromJsonAsync<ShareView>())!;Assert.Equal(HttpStatusCode.OK,(await f.Teacher.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync($"/api/tutoring-sessions/{personal.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await f.A.DeleteAsync($"/api/tutoring-sessions/{personal.Id}/shares/{share.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Teacher.GetAsync($"/api/teacher/reports/{report.Id}")).StatusCode);
    }
    [Fact] public async Task MembershipIsRevalidatedAfterWaitingForTheSessionLock()
    {
        using var f=await AssignedFixture();var s=await OpenResponse(f);var d=await Evidence(f);var r=Decision(d,d.Exercises[1].Id);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT * FROM \"TutoringSessions\" WHERE \"Id\"={s.Id} FOR UPDATE");
        var holder=((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;
        var pending=Review(f,r);var waiting=false;
        for(var i=0;i<100&&!waiting;i++)
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_stat_clear_snapshot()");
            waiting=await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND {holder}=ANY(pg_blocking_pids(pid))").SingleAsync()>0;
            if(pending.IsCompleted&&!waiting)Assert.Fail("Review completed before waiting: "+(await pending).StatusCode);
            if(!waiting)await Task.Delay(50);
        }
        Assert.True(waiting,"The review must actually wait on the locked session.");
        Assert.Equal(HttpStatusCode.NoContent,(await f.Admin.DeleteAsync($"/api/groups/{f.Group.Id}/members/{f.UserA.Id}/Student")).StatusCode);
        await tx.CommitAsync();Assert.Equal(HttpStatusCode.NotFound,(await pending).StatusCode);
        Assert.Empty(await db.TeacherReviewEntries.ToArrayAsync());
    }
}
