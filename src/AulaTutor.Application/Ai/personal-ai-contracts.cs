namespace AulaTutor.Application.Ai;

public sealed record PersonalConnectionView(Guid Id, string Provider, string Fingerprint, string Status,
    string Method, DateTimeOffset UpdatedAt, string? PreferredModel);
public sealed record PersonalModelView(string Id, string Name, bool Compatible, bool Free, bool PriceKnown,
    string Availability, DateTimeOffset CheckedAt, DateTimeOffset ExpiresAt);
public sealed record PersonalAiView(bool CustodyAvailable, string Selection, Guid? ConnectionId, string? Model,
    string InstitutionalProvider, string InstitutionalModel, PersonalConnectionView[] Connections);
public sealed record ManualAiRequest(string Provider, string ApiKey, string? Workspace = null)
{
    public override string ToString() => "ManualAiRequest [credential redacted]";
}
public sealed record SelectAiRequest(Guid? ConnectionId, string? Model, bool PrivacyAccepted, bool PaidAccepted);
public sealed record OAuthStartView(string AuthorizationUrl, DateTimeOffset ExpiresAt);
public interface IPersonalAiConnections
{
    Task<PersonalAiView> GetAsync(CancellationToken ct);
    Task SaveAsync(ManualAiRequest request, CancellationToken ct);
    Task SelectAsync(SelectAiRequest request, CancellationToken ct);
    Task DisconnectAsync(Guid id, CancellationToken ct);
    Task<PersonalModelView[]> ModelsAsync(string provider, bool refresh, CancellationToken ct);
}
