using System.Net;
using System.Net.Http.Json;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AulaTutor.IntegrationTests;

public sealed partial class LessonTests
{
    private static string ImportJson()
    {
        var content=Content();var e=content.Sections[1].Exercises[0];
        content.Sections[1].Exercises[0]=e with {Rubric=e.Rubric with {ReferenceSolution="Ejemplo privado"}};
        return LearningJson.Write(new LessonImportV1(LessonImport.Format,LessonDesignPayload.FromProposal(new("Revisar",content))));
    }
    [Fact] public async Task ImportedLessonUsesNormalEditSimulationApprovalPublicationAndImmutableVersions()
    {
        using var c=await Teacher();var input=new CreateImportedLessonRequest(Guid.NewGuid(),ImportJson());
        var preview=await c.PostAsJsonAsync("/api/lessons/import/preview",new LessonImportInput(input.Json));Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        Assert.NotNull((await preview.Content.ReadFromJsonAsync<LessonImportPreview>())!.Content);Assert.Empty((await c.GetFromJsonAsync<LessonItem[]>("/api/lessons/"))!);
        var response=await c.PostAsJsonAsync("/api/lessons/import",input);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var l=(await response.Content.ReadFromJsonAsync<LessonView>())!;Assert.Equal("Draft",l.State);Assert.Empty(l.Versions);Assert.Equal(0,provider.Calls);
        var edit=await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(l.Revision,l.Content with {Title="Editada"}));l=(await edit.Content.ReadFromJsonAsync<LessonView>())!;
        var sim=await Sim(c,l);Assert.DoesNotContain("REFERENCIA_PRIVADA",LearningJson.Write(sim));sim=await SimTurn(c,sim,"Start");
        l=await Command(c,await Lesson(c,l.Id),"review");l=await Command(c,l,"approve");var version=l.Versions.Single();Assert.Null(version.PublishedAt);
        l=await Command(c,l,"publish",version.Id);l=await Command(c,l,"new-draft");Assert.Equal(version.ContentHash,l.Versions.Single().ContentHash);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/lessons/{l.Id}/draft",new SaveLessonRequest(0,l.Content))).StatusCode);
    }
    [Fact] public async Task ImportReplayIsSerializedAndCannotReuseKeyWithDifferentContent()
    {
        using var c=await Teacher();var input=new CreateImportedLessonRequest(Guid.NewGuid(),ImportJson());
        var replies=await Task.WhenAll(c.PostAsJsonAsync("/api/lessons/import",input),c.PostAsJsonAsync("/api/lessons/import",input));
        Assert.All(replies,r=>Assert.Equal(HttpStatusCode.Created,r.StatusCode));Assert.Single((await c.GetFromJsonAsync<LessonItem[]>("/api/lessons/"))!);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/lessons/import",input with {Json=input.Json.Replace("Clase SQL","Otra clase")})).StatusCode);
        Assert.Equal(0,provider.Calls);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        var audit=Assert.Single(await db.AuditEvents.Where(a=>a.Type=="LessonImported").ToArrayAsync());
        Assert.DoesNotContain("REFERENCIA_PRIVADA",LearningJson.Write(audit));Assert.Equal("Committed",audit.Result);
    }
    [Fact] public async Task ExternalEndpointsEnforceRolesCsrfAndServerValidation()
    {
        using var c=await Teacher();using var anonymous=TestHost.Client(factory);var(student,_)=await Register();using var studentClient=student;
        using var admin=await Login("admin@example.invalid");var(pending,_)=await Register("Teacher");using var pendingClient=pending;
        foreach(var route in new[]{"/api/lessons/import","/api/lessons/import/preview","/api/lessons/external-prompt"})
        {
            var data=new {requestId=Guid.NewGuid(),json=ImportJson()};
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(route,data)).StatusCode);
            foreach(var denied in new[]{student,admin,pending}) Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(route,data)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/lessons/import",new CreateImportedLessonRequest(Guid.NewGuid(),"{}"))).StatusCode);
        c.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/lessons/import",new CreateImportedLessonRequest(Guid.NewGuid(),ImportJson()))).StatusCode);
        Assert.Empty((await c.GetFromJsonAsync<LessonItem[]>("/api/lessons/"))!);
    }
    [Fact] public async Task TeacherRevokedWhileWaitingCannotCommitImportOrAudit()
    {
        using var c=await Teacher();var actor=(await c.GetFromJsonAsync<AulaTutor.Application.Identity.UserView>("/api/me"))!;
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.Id.ToString()}, 0))");
        var pending=c.PostAsJsonAsync("/api/lessons/import",new CreateImportedLessonRequest(Guid.NewGuid(),ImportJson()));
        var waiting=false;
        for(var i=0;i<500;i++)
        {
            // pg_stat_activity caches a snapshot inside this deliberately open transaction.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_stat_clear_snapshot()");
            waiting=await db.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%') AS \"Value\"").SingleAsync();
            if(waiting)break;await Task.Delay(20);
        }
        Assert.True(waiting,"Import must reach its serialized creation lock before revocation.");
        using var admin=await Login("admin@example.invalid");
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PutAsJsonAsync($"/api/admin/users/{actor.Id}/status",new AulaTutor.Application.Identity.AccountStatusRequest(false))).StatusCode);
        await tx.RollbackAsync();Assert.Equal(HttpStatusCode.Forbidden,(await pending).StatusCode);
        Assert.Empty(await db.Lessons.ToArrayAsync());Assert.Empty(await db.AuditEvents.Where(a=>a.Type=="LessonImported").ToArrayAsync());
    }
}
