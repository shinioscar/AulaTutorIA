using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Ai;

public sealed class PersonalAiConnections(AulaTutorDbContext db, ICurrentActor current, PersonalAiVault vault,
    PersonalModelCatalog catalog, InstitutionalAiProvider institutional) : IPersonalAiConnections
{
    public async Task<PersonalAiView> GetAsync(CancellationToken ct)
    {
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        var rows = await db.PersonalAiConnections.AsNoTracking().Where(c => c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId).OrderBy(c => c.Provider).ToArrayAsync(ct);
        var selection = await db.PersonalAiSelections.AsNoTracking().SingleOrDefaultAsync(s => s.OwnerId == actor.Id, ct);
        var health = await institutional.Provider.CheckHealthAsync(ct);
        return new(vault.Available, selection?.Mode ?? "Institutional", selection?.ConnectionId, selection?.Model, health.ProviderId, health.ModelId,
            rows.Select(c => new PersonalConnectionView(c.Id, c.Provider, c.Fingerprint[..Math.Min(12, c.Fingerprint.Length)], c.Status, c.Method, c.UpdatedAt, c.PreferredModel)).ToArray());
    }
    public static void ValidateKey(string provider, string? key, string? workspace)
    {
        if (provider is not ("Groq" or "Gemini" or "Anthropic" or "OpenRouter") || key is null || key.Length is < 8 or > 4096 || key.Any(c => c < 33 || c > 126) ||
            workspace is not null && (provider != "Anthropic" || workspace.Length is < 5 or > 100 || workspace.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))))
            throw new AppException(400, "Revisa el proveedor y el formato de la clave.");
    }
    public async Task SaveAsync(ManualAiRequest request, CancellationToken ct)
    {
        if (request.Provider == "OpenRouter") throw new AppException(400, "Conecta OpenRouter mediante su autorización.");
        ValidateKey(request.Provider, request.ApiKey, request.Workspace);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        await InstallAsync(actor, request.Provider, request.ApiKey, "Manual", request.Workspace, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    internal async Task InstallAsync(Actor actor, string provider, string key, string method, string? workspace, CancellationToken ct)
    {
        ValidateKey(provider, key, workspace);
        var row = await db.PersonalAiConnections.SingleOrDefaultAsync(c => c.OwnerId == actor.Id && c.Provider == provider, ct);
        if (row is null) { row = new() { OwnerId = actor.Id, OrganizationId = actor.OrganizationId, Provider = provider, CreatedAt = DateTimeOffset.UtcNow }; db.PersonalAiConnections.Add(row); }
        else { row.CredentialRevision++; await ClearSelectionAsync(actor.Id, row.Id, ct); }
        row.ProtectedCredential = vault.Protect(PersonalAiVault.CredentialPurpose(actor.Id, row.Id, row.CredentialRevision), key);
        row.Fingerprint = PersonalAiVault.Hash(key); row.Method = method; row.Status = "Connected"; row.Workspace = workspace;
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }
    public async Task SelectAsync(SelectAiRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        var selection = await db.PersonalAiSelections.SingleOrDefaultAsync(s => s.OwnerId == actor.Id, ct);
        if (selection is null) { selection = new() { OwnerId = actor.Id, OrganizationId = actor.OrganizationId }; db.PersonalAiSelections.Add(selection); }
        if (request.ConnectionId is { } id)
        {
            if (!vault.Available || !request.PrivacyAccepted) throw new AppException(400, "Acepta el envío de consultas a tu proveedor.");
            var connection = await db.PersonalAiConnections.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId, ct) ?? throw new AppException(404, "La conexión no está disponible.");
            if (connection.Status != "Connected") throw new AppException(409, "Vuelve a conectar este proveedor.");
            var model = await catalog.RequireStoredAsync(connection.Provider, request.Model, request.PaidAccepted, ct);
            selection.ConnectionId = id; selection.Model = model.Id; selection.Mode = "Personal"; selection.PaidAccepted = request.PaidAccepted;
            connection.PreferredModel = model.Id;
        }
        else { selection.ConnectionId = null; selection.Model = null; selection.Mode = "Institutional"; selection.PaidAccepted = false; }
        selection.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DisconnectAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        var row = await db.PersonalAiConnections.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId, ct) ?? throw new AppException(404, "La conexión no está disponible.");
        if (row.Status != "Disconnected") { row.CredentialRevision++; row.Status = "Disconnected"; row.ProtectedCredential = ""; row.UpdatedAt = DateTimeOffset.UtcNow; }
        await ClearSelectionAsync(actor.Id, id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task ClearSelectionAsync(Guid owner, Guid connection, CancellationToken ct)
    {
        var selection = await db.PersonalAiSelections.SingleOrDefaultAsync(s => s.OwnerId == owner && s.ConnectionId == connection, ct);
        if (selection is not null) { selection.Mode = "None"; selection.ConnectionId = null; selection.Model = null; selection.PaidAccepted = false; selection.UpdatedAt = DateTimeOffset.UtcNow; }
    }
    public async Task<PersonalModelView[]> ModelsAsync(string provider, bool refresh, CancellationToken ct)
    {
        await PersonalAiSession.RequireAsync(current, db, ct);
        return (await catalog.GetAsync(provider, refresh, ct)).Select(m => new PersonalModelView(m.Id, m.Name, m.Compatible, m.Free, m.PriceKnown, m.Availability, m.CheckedAt, m.ExpiresAt)).ToArray();
    }
}
