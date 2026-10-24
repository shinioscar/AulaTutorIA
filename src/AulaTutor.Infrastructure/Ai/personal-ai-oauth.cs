using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class PersonalAiOAuthService(AulaTutorDbContext db, ICurrentActor current, IHttpContextAccessor accessor,
    PersonalAiVault vault, PersonalAiConnections connections, IHttpClientFactory clients, IOptions<PersonalAiOptions> options)
{
    private HttpContext Http => accessor.HttpContext ?? throw new AppException(401, "Inicia sesión nuevamente.");
    private string Origin => options.Value.PublicOrigin;
    private static string Purpose(Guid id, string part) => $"oauth/v1/{id:N}/{part}";
    private static bool ValidNonce(string? value) => value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private string? Browser => Http.Request.Cookies[PersonalAiSession.BrowserCookie] is { } value && ValidNonce(value) ? value : null;
    public async Task<OAuthStartView> StartAsync(CancellationToken ct)
    {
        if (!vault.Available) throw new AppException(503, "La conexión personal requiere configurar la custodia de credenciales.");
        var actor = await PersonalAiSession.RequireAsync(current, db, ct); var session = await PersonalAiSession.IdAsync(Http);
        var state = PersonalAiVault.Random(); var browser = PersonalAiVault.Random(); var verifier = PersonalAiVault.Random();
        var challenge = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        await PersonalAiSession.RequireAsync(current, db, ct);
        await CancelBrowserCoreAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach(var abandoned in await db.PersonalAiOAuths.Where(o => o.OwnerId == actor.Id && o.ExpiresAt <= now && (o.Status == "Pending" || o.Status == "Returned")).ToArrayAsync(ct))
            Clear(abandoned, "Expired");
        var stamp = await db.Users.Where(u => u.Id == actor.Id).Select(u => u.SecurityStamp).SingleAsync(ct);
        var row = new PersonalAiOAuth { OwnerId = actor.Id, OrganizationId = actor.OrganizationId, StateHash = PersonalAiVault.Hash(state), BrowserHash = PersonalAiVault.Hash(browser),
            SessionHash = PersonalAiVault.Hash(session), StampHash = PersonalAiVault.Hash(stamp ?? ""), CreatedAt = now, ExpiresAt = now.AddMinutes(5) };
        row.ProtectedVerifier = vault.ProtectTemporary(Purpose(row.Id, "verifier"), verifier, row.ExpiresAt);
        db.PersonalAiOAuths.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        Http.Response.Cookies.Append(PersonalAiSession.BrowserCookie, browser, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", IsEssential = true, MaxAge = TimeSpan.FromMinutes(5) });
        var url = "https://openrouter.ai/auth?callback_url=" + Uri.EscapeDataString(Origin + "/api/ai-connections/openrouter/callback") +
            "&code_challenge=" + challenge + "&code_challenge_method=S256&state=" + state + "&key_label=AulaTutor%20IA";
        return new(url, row.ExpiresAt);
    }
    public async Task<string> CallbackAsync(string? state, string? code, CancellationToken ct)
    {
        // Cross-site GET has no authority to install a connection. Main Strict cookie stays unchanged.
        if (Browser is not { } browser) return Origin + "/ai-provider/return?result=rejected";
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        var hash = PersonalAiVault.Hash(browser);
        var row = ValidNonce(state) ? await db.PersonalAiOAuths.SingleOrDefaultAsync(o => o.StateHash == PersonalAiVault.Hash(state!) && o.BrowserHash == hash, ct) : null;
        if (row is null || row.Status != "Pending" || row.ExpiresAt <= DateTimeOffset.UtcNow || code is null || code.Length is < 8 or > 4096 || code.Any(c => c < 33 || c > 126))
        {
            if (row is not null && row.Status == "Pending") Clear(row, row.ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : "Canceled");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Origin + "/ai-provider/return?result=rejected";
        }
        row.Status = "Returned"; row.ProtectedCode = vault.ProtectTemporary(Purpose(row.Id, "code"), code, row.ExpiresAt);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Origin + "/ai-provider/return";
    }
    public async Task CompleteAsync(CancellationToken ct)
    {
        var actor = await PersonalAiSession.RequireAsync(current, db, ct); var session = await PersonalAiSession.IdAsync(Http);
        if (Browser is not { } browser) throw Rejected();
        PersonalAiOAuth row; string code; string verifier;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await PersonalAiSession.LockAsync(db, ct);
            row = await db.PersonalAiOAuths.SingleOrDefaultAsync(o => o.BrowserHash == PersonalAiVault.Hash(browser) && o.Status == "Returned", ct) ?? throw Rejected();
            if (!await MatchesAsync(row, actor, session, ct)) { Clear(row, "Canceled"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); throw Rejected(); }
            code = vault.UnprotectTemporary(Purpose(row.Id, "code"), row.ProtectedCode!);
            verifier = vault.UnprotectTemporary(Purpose(row.Id, "verifier"), row.ProtectedVerifier);
            Clear(row, "Exchanging"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        try
        {
            // A single exchange, never retried after an uncertain response.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
            using var client = clients.CreateClient("AiMetadata");
            using var exchange = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/auth/keys")
                { Content = JsonContent.Create(new { code, code_verifier = verifier, code_challenge_method = "S256" }) };
            using var response = await client.SendAsync(exchange, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw Rejected();
            using var result = await PersonalModelCatalog.ReadBoundedAsync(response.Content, 16384, deadline.Token);
            var key = result.RootElement.GetProperty("key").GetString() ?? throw Rejected();
            PersonalAiConnections.ValidateKey("OpenRouter", key, null);
            db.ChangeTracker.Clear();
            using var saveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var tx = await db.Database.BeginTransactionAsync(saveDeadline.Token); await PersonalAiSession.LockAsync(db, saveDeadline.Token);
            var saved = await db.PersonalAiOAuths.SingleAsync(o => o.Id == row.Id, saveDeadline.Token);
            if (saved.Status != "Exchanging" || !await MatchesAsync(saved, actor, session, saveDeadline.Token)) throw Rejected();
            await PersonalAiSession.RequireAsync(current, db, saveDeadline.Token);
            await connections.InstallAsync(actor, "OpenRouter", key, "OAuth", null, saveDeadline.Token);
            Clear(saved, "Succeeded"); await db.SaveChangesAsync(saveDeadline.Token); await tx.CommitAsync(saveDeadline.Token);
            Http.Response.Cookies.Delete(PersonalAiSession.BrowserCookie, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            db.ChangeTracker.Clear(); using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var tx = await db.Database.BeginTransactionAsync(cleanup.Token); await PersonalAiSession.LockAsync(db, cleanup.Token);
            var saved = await db.PersonalAiOAuths.SingleAsync(o => o.Id == row.Id, cleanup.Token);
            if (saved.Status == "Exchanging") { Clear(saved, "Failed"); await db.SaveChangesAsync(cleanup.Token); }
            await tx.CommitAsync(cleanup.Token); throw Rejected();
        }
    }
    private async Task<bool> MatchesAsync(PersonalAiOAuth row, Actor actor, string session, CancellationToken ct)
    {
        if (row.OwnerId != actor.Id || row.OrganizationId != actor.OrganizationId || row.ExpiresAt <= DateTimeOffset.UtcNow || row.SessionHash != PersonalAiVault.Hash(session)) return false;
        var stamp = await db.Users.AsNoTracking().Where(u => u.Id == actor.Id && u.IsActive && u.OrganizationId == actor.OrganizationId).Select(u => u.SecurityStamp).SingleOrDefaultAsync(ct);
        return stamp is not null && PersonalAiVault.Hash(stamp) == row.StampHash;
    }
    public async Task CancelBrowserAsync(CancellationToken ct)
    {
        if (Browser is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        await CancelBrowserCoreAsync(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        Http.Response.Cookies.Delete(PersonalAiSession.BrowserCookie, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
    }
    private async Task CancelBrowserCoreAsync(CancellationToken ct)
    {
        if (Browser is not { } browser) return;
        foreach(var row in await db.PersonalAiOAuths.Where(o => o.BrowserHash == PersonalAiVault.Hash(browser) && (o.Status == "Pending" || o.Status == "Returned" || o.Status == "Exchanging")).ToArrayAsync(ct)) Clear(row, "Canceled");
    }
    private static void Clear(PersonalAiOAuth row, string status) { row.Status = status; row.ProtectedVerifier = ""; row.ProtectedCode = null; }
    private static AppException Rejected() => new(409, "No se completó la autorización. Vuelve a conectar OpenRouter.");
}
