using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed record InstitutionalAiProvider(IAiProvider Provider);
public sealed record PersonalInferenceCall(Guid OwnerId, Guid ConnectionId, long Revision, string Fingerprint, string? ExternalAccount, string Operation, bool Simulated = false, string? OperationId = null);
public static class PersonalInferenceScope
{
    private static readonly AsyncLocal<PersonalInferenceCall?> active = new();
    public static PersonalInferenceCall? Current => active.Value;
    public static IDisposable Enter(PersonalInferenceCall call) { var previous = active.Value; active.Value = call; return new Exit(() => active.Value = previous); }
    private sealed class Exit(Action action) : IDisposable { public void Dispose() => action(); }
}

public sealed class PersonalAiProvider(InstitutionalAiProvider institutional, AulaTutorDbContext db, ICurrentActor current,
    PersonalAiVault vault, PersonalModelCatalog catalog, AiHttpTransport transport, IOptions<AiProviderOptions> settings, PersonalAiQaMode qa) : IAiProvider
{
    private AiInferenceBinding? binding;
    private bool bound;
    public IAiProvider Institutional => institutional.Provider;
    public string ProviderId => binding?.Provider ?? Institutional.ProviderId;
    public AiCapabilities Capabilities => binding is null ? Institutional.Capabilities : new(binding.Provider is "Groq" or "Gemini", true, binding.StrictSchema, true);
    public async Task<AiHealthResult> CheckHealthAsync(CancellationToken ct)
    {
        if (bound) return binding is null ? await Institutional.CheckHealthAsync(ct) : new(true, binding.Provider, binding.Model, binding.Id);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        var selected = await db.PersonalAiSelections.AsNoTracking().SingleOrDefaultAsync(s => s.OwnerId == actor.Id, ct);
        bound = true;
        if (selected is null || selected.Mode == "Institutional") return await Institutional.CheckHealthAsync(ct);
        if (selected.Mode != "Personal" || !vault.Available) throw new AppException(409, "Selecciona un proveedor para comenzar.");
        var connection = await db.PersonalAiConnections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == selected.ConnectionId && c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId && c.Status == "Connected", ct)
            ?? throw new AppException(409, "Vuelve a conectar el proveedor seleccionado.");
        var model = await catalog.RequireStoredAsync(connection.Provider, selected.Model, selected.PaidAccepted, ct);
        binding = new() { OwnerId = actor.Id, OrganizationId = actor.OrganizationId, ConnectionId = connection.Id,
            CredentialRevision = connection.CredentialRevision, Provider = connection.Provider, Model = model.Id, StrictSchema = model.StrictSchema,
            PaidAccepted = selected.PaidAccepted, CreatedAt = DateTimeOffset.UtcNow };
        db.AiInferenceBindings.Add(binding);
        return new(true, binding.Provider, binding.Model, binding.Id);
    }
    public async Task<AiHealthResult> BindAsync(Guid? id, CancellationToken ct)
    {
        bound = true; binding = null;
        if (id is null) return await Institutional.CheckHealthAsync(ct);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        binding = db.AiInferenceBindings.Local.SingleOrDefault(b => b.Id == id && b.OwnerId == actor.Id && b.OrganizationId == actor.OrganizationId) ??
            await db.AiInferenceBindings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id && b.OwnerId == actor.Id && b.OrganizationId == actor.OrganizationId, ct)
            ?? throw new AppException(409, "La conexión de esta sesión no está disponible.");
        if (!await db.PersonalAiConnections.AsNoTracking().AnyAsync(c => c.Id == binding.ConnectionId && c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId && c.Status == "Connected" && c.CredentialRevision == binding.CredentialRevision, ct))
            throw new AppException(409, "La conexión de esta sesión fue retirada. Inicia otra con tu proveedor actual.");
        return new(true, binding.Provider, binding.Model, binding.Id);
    }
    public async Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
    {
        if (!bound) throw new AiProviderException(AiErrorCode.Configuration);
        if (binding is null) return await Institutional.GenerateAsync(request, ct);
        var actor = await PersonalAiSession.RequireAsync(current, db, ct);
        var c = await db.PersonalAiConnections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == binding.ConnectionId && c.OwnerId == actor.Id && c.OrganizationId == actor.OrganizationId && c.Status == "Connected" && c.CredentialRevision == binding.CredentialRevision, ct)
            ?? throw new AiProviderException(AiErrorCode.Authentication);
        try
        {
            PersonalAiModel model;
            try { if (binding.Provider == "OpenRouter") await catalog.GetAsync("OpenRouter", false, ct); model = await catalog.RequireStoredAsync(binding.Provider, binding.Model, binding.PaidAccepted, ct); }
            catch (AppException e) { throw new AiProviderException(e.Status == 503 ? AiErrorCode.Unavailable : e.Status == 400 ? AiErrorCode.PaymentAuthorizationRequired : AiErrorCode.Unsupported); }
            if (request.UseCase.StartsWith("prepared_", StringComparison.Ordinal) &&
                (request.MaxOutputTokens > model.MaxOutputTokens || request.Messages.Sum(m => System.Text.Encoding.UTF8.GetByteCount(m.Text)) + request.MaxOutputTokens + 512 > model.ContextWindow))
                throw new AiProviderException(AiErrorCode.Unsupported);
            string key;
            try { key = vault.Unprotect(PersonalAiVault.CredentialPurpose(c.OwnerId, c.Id, c.CredentialRevision), c.ProtectedCredential); }
            catch (AppException) { throw new AiProviderException(AiErrorCode.Authentication); }
            var opts = new AiProviderOptions { Provider = c.Provider, TimeoutSeconds = settings.Value.TimeoutSeconds, MaxConcurrency = settings.Value.MaxConcurrency, MaxRetries = settings.Value.MaxRetries,
                MaxRetryDelaySeconds = settings.Value.MaxRetryDelaySeconds };
            IAiProvider provider;
            if (c.Provider == "Groq") { opts.Groq = new() { ApiKey = key, Model = binding.Model }; provider = new GroqAiProvider(transport, Options.Create(opts)); }
            else if (c.Provider == "Gemini") { opts.Gemini = new() { ApiKey = key, Model = binding.Model }; provider = new GeminiAiProvider(transport, Options.Create(opts)); }
            else provider = new PersonalRemoteAiProvider(transport, opts, c.Provider, binding.Model, key, model.StrictSchema, !binding.PaidAccepted, c.Workspace);
            using var scope = PersonalInferenceScope.Enter(new(actor.Id, c.Id, c.CredentialRevision, c.Fingerprint, c.ExternalAccount, request.UseCase, qa.Enabled, request.CorrelationId));
            return await provider.GenerateAsync(request, ct);
        }
        catch (AiProviderException e) when (e.Code == AiErrorCode.Authentication)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var tx = await db.Database.BeginTransactionAsync(deadline.Token); await PersonalAiSession.LockAsync(db, deadline.Token);
            var saved = await db.PersonalAiConnections.SingleAsync(x => x.Id == c.Id, deadline.Token);
            if (saved.CredentialRevision == c.CredentialRevision && saved.Status == "Connected") { saved.Status = "Invalid"; saved.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(deadline.Token); }
            await tx.CommitAsync(deadline.Token); throw;
        }
    }
    public static Task<bool> CredentialActiveAsync(AulaTutorDbContext db, Guid? id, Guid owner, CancellationToken ct) => id is null ? Task.FromResult(true) :
        (from b in db.AiInferenceBindings.AsNoTracking() join c in db.PersonalAiConnections.AsNoTracking() on b.ConnectionId equals c.Id
         join u in db.Users.AsNoTracking() on c.OwnerId equals u.Id
         where b.Id == id && b.OwnerId == owner && c.OwnerId == owner && b.OrganizationId == c.OrganizationId && c.Status == "Connected" && c.CredentialRevision == b.CredentialRevision &&
             (db.RequestSecurityStamp == null || u.SecurityStamp == db.RequestSecurityStamp)
         select c.Id).AnyAsync(ct);
}

public static class AiBinding
{
    public static Task<AiHealthResult> BindAsync(IAiProvider provider, Guid? id, CancellationToken ct) => provider is PersonalAiProvider personal
        ? personal.BindAsync(id, ct) : provider.CheckHealthAsync(ct);
}
