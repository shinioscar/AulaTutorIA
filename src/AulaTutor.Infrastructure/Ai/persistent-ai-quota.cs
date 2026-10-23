using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class AiQuotaLimits
{
    public int UserPerMinute { get; set; } = 6;
    public int ProviderPerMinute { get; set; } = 20;
    public int UserPerDay { get; set; } = 100;
    public int ProviderPerDay { get; set; } = 1000;
    public int OrganizationPerDay { get; set; } = 1000;
    public int GroupPerDay { get; set; } = 300;
    public int GlobalConcurrency { get; set; } = 2;
    public int UserConcurrency { get; set; } = 1;
    public int DailyEstimatedTokenBudget { get; set; } = 0;
    public bool Valid() => UserPerMinute is > 0 and <= 1000 && ProviderPerMinute is > 0 and <= 10000 &&
        UserPerDay is > 0 and <= 100000 && ProviderPerDay is > 0 and <= 1000000 && OrganizationPerDay is > 0 and <= 1000000 &&
        GroupPerDay is > 0 and <= 100000 && GlobalConcurrency is > 0 and <= 32 && UserConcurrency > 0 && UserConcurrency <= GlobalConcurrency && DailyEstimatedTokenBudget >= 0;
}
public sealed class AiQuotaOptions
{
    public bool AllowExternalCalls { get; set; } = true;
    public AiQuotaLimits Limits { get; set; } = new();
    public Dictionary<string, AiQuotaLimits> Providers { get; set; } = new();
    public Dictionary<Guid, int> OrganizationDailyLimits { get; set; } = new();
    public Dictionary<Guid, int> GroupDailyLimits { get; set; } = new();
    public bool Valid() => Limits.Valid() && Providers.All(p => p.Key is "Groq" or "Gemini" or "Ollama" && p.Value.Valid()) &&
        OrganizationDailyLimits.All(p => p.Key != Guid.Empty && p.Value > 0) && GroupDailyLimits.All(p => p.Key != Guid.Empty && p.Value > 0);
}
public interface IAiCallAdmission
{
    Task<Guid> ReserveAsync(string provider, string model, int estimatedTokens, CancellationToken ct);
    Task FinishAsync(Guid reservation, string outcome, AiTokenUsage? usage, long durationMs, TimeSpan? retryAfter = null);
}

// New DbContext per reservation/finalization: never join a tutoring transaction or hold locks during HTTP.
public sealed class PersistentAiAdmission(IServiceScopeFactory scopes, IHttpContextAccessor accessor,
    IOptions<AiQuotaOptions> options, IOptions<AiProviderOptions> providerOptions, ILogger<PersistentAiAdmission> logger) : IAiCallAdmission
{
    public static DateTimeOffset WindowStart(DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return now.AddMinutes(-1) < day ? now.AddMinutes(-1) : day;
    }
    public async Task<Guid> ReserveAsync(string provider, string model, int estimatedTokens, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var actor = await scope.ServiceProvider.GetRequiredService<ICurrentActor>().GetAsync();
        var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
        Guid? groupId = null;
        // Only an owned assigned session can attribute usage to a group. HTTP input grants no access.
        if (Guid.TryParse(accessor.HttpContext?.Request.RouteValues["id"]?.ToString(), out var sessionId))
            groupId = await (from s in db.TutoringSessions join a in db.LessonAssignments on s.AssignmentId equals a.Id
                where s.Id == sessionId && s.StudentId == actor.Id && s.OrganizationId == actor.OrganizationId && s.Mode == TutoringMode.AssignedClass
                select (Guid?)a.GroupId).SingleOrDefaultAsync(ct);
        return await ReserveForAsync(db, actor, groupId, provider, model, estimatedTokens,
            accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), options.Value, providerOptions.Value.TimeoutSeconds, ct);
    }
    public static async Task<Guid> ReserveForAsync(AulaTutorDbContext db, Actor actor, Guid? groupId, string provider,
        string model, int estimatedTokens, string correlation, AiQuotaOptions policy, int timeoutSeconds, CancellationToken ct)
    {
        if (!actor.IsActive || !policy.AllowExternalCalls) throw new AiProviderException(AiErrorCode.Forbidden);
        if (!policy.Valid() || estimatedTokens < 0 || provider is not ("Groq" or "Gemini" or "Ollama") || model.Length > 120 || correlation.Length > 64 || timeoutSeconds is < 1 or > 120)
            throw new AiProviderException(AiErrorCode.Configuration);
        var limits = policy.Providers.GetValueOrDefault(provider) ?? policy.Limits;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8128001)", ct);
        if (!await db.Users.AnyAsync(u => u.Id == actor.Id && u.OrganizationId == actor.OrganizationId && u.IsActive &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId &&
                (actor.IsAdmin && r.Name == RoleNames.Admin || actor.IsStudent && r.Name == RoleNames.Student || actor.IsTeacher && u.TeacherApproval == TeacherApproval.Approved && r.Name == RoleNames.Teacher))), ct))
            throw new AiProviderException(AiErrorCode.Forbidden);
        if (groupId.HasValue && !await db.Groups.AnyAsync(g => g.Id == groupId && g.OrganizationId == actor.OrganizationId && !g.IsArchived &&
            db.GroupMemberships.Any(m => m.GroupId == g.Id && m.UserId == actor.Id && m.OrganizationId == actor.OrganizationId), ct))
            throw new AiProviderException(AiErrorCode.Forbidden);
        var now = await db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var minute = now.AddMinutes(-1);
        var since = WindowStart(now);
        var recent = await db.AiReservations.AsNoTracking().Where(r => r.CreatedAt >= since || r.LeaseUntil > now || r.RetryNotBefore > now).ToArrayAsync(ct);
        var cooldown = recent.Where(r => r.Provider == provider && r.RetryNotBefore > now).Select(r => r.RetryNotBefore).Max();
        if (cooldown.HasValue) throw new AiProviderException(AiErrorCode.RateLimited, cooldown.Value - now);
        var live = recent.Where(r => r.LeaseUntil > now && r.State is "Reserved" or "Uncertain").ToArray();
        var own = recent.Where(r => r.ActorId == actor.Id).ToArray();
        var upstream = recent.Where(r => r.Provider == provider).ToArray();
        var orgLimit = policy.OrganizationDailyLimits.GetValueOrDefault(actor.OrganizationId, limits.OrganizationPerDay);
        var groupLimit = groupId.HasValue ? policy.GroupDailyLimits.GetValueOrDefault(groupId.Value, limits.GroupPerDay) : limits.GroupPerDay;
        var daily = own.Count(r => r.CreatedAt >= day) >= limits.UserPerDay || upstream.Count(r => r.CreatedAt >= day) >= limits.ProviderPerDay ||
            recent.Count(r => r.OrganizationId == actor.OrganizationId && r.CreatedAt >= day) >= orgLimit ||
            groupId.HasValue && recent.Count(r => r.GroupId == groupId && r.CreatedAt >= day) >= groupLimit ||
            limits.DailyEstimatedTokenBudget > 0 && upstream.Where(r => r.CreatedAt >= day).Sum(r => (long)Math.Max(r.EstimatedTokens, (long)(r.InputTokens ?? 0) + (r.OutputTokens ?? 0))) + estimatedTokens > limits.DailyEstimatedTokenBudget;
        if (daily) throw new AiProviderException(AiErrorCode.RateLimited, day.AddDays(1) - now);
        var waits = new List<DateTimeOffset>();
        if (live.Length >= policy.Limits.GlobalConcurrency) waits.Add(live.Min(r => r.LeaseUntil));
        if (live.Count(r => r.ActorId == actor.Id) >= limits.UserConcurrency) waits.Add(live.Where(r => r.ActorId == actor.Id).Min(r => r.LeaseUntil));
        if (own.Count(r => r.CreatedAt > minute) >= limits.UserPerMinute) waits.Add(own.Where(r => r.CreatedAt > minute).Min(r => r.CreatedAt).AddMinutes(1));
        if (upstream.Count(r => r.CreatedAt > minute) >= limits.ProviderPerMinute) waits.Add(upstream.Where(r => r.CreatedAt > minute).Min(r => r.CreatedAt).AddMinutes(1));
        if (waits.Count > 0) throw new AiProviderException(AiErrorCode.RateLimited, waits.Max() - now);
        var reservation = new AiReservation { ActorId = actor.Id, OrganizationId = actor.OrganizationId, GroupId = groupId,
            Provider = provider, Model = model, CorrelationId = correlation, CreatedAt = now,
            LeaseUntil = now.AddSeconds(timeoutSeconds + 30), EstimatedTokens = estimatedTokens };
        db.AiReservations.Add(reservation); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return reservation.Id;
    }
    public async Task FinishAsync(Guid reservation, string outcome, AiTokenUsage? usage, long durationMs, TimeSpan? retryAfter = null)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AulaTutorDbContext>();
            var now = DateTimeOffset.UtcNow;
            var reported = usage is { IsEstimated: false, InputTokens: >= 0, OutputTokens: >= 0 };
            DateTimeOffset? retryNotBefore = retryAfter.HasValue ? now.AddSeconds(Math.Clamp(retryAfter.Value.TotalSeconds, 0, 315360000)) : null;
            await db.AiReservations.Where(r => r.Id == reservation && r.State == "Reserved")
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, outcome).SetProperty(r => r.FinishedAt, now)
                    .SetProperty(r => r.InputTokens, reported ? usage!.InputTokens : (int?)null).SetProperty(r => r.OutputTokens, reported ? usage!.OutputTokens : (int?)null)
                    .SetProperty(r => r.RetryNotBefore, retryNotBefore)
                    .SetProperty(r => r.DurationMs, durationMs), deadline.Token);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        { logger.LogWarning("AI reservation finalization unavailable {ReservationId}; remains reserved until lease expiry", reservation); }
    }
}
