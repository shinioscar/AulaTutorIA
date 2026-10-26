namespace AulaTutor.Domain.Identity;

public sealed class PersonalAiConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public string Provider { get; set; } = "";
    public string ProtectedCredential { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public long CredentialRevision { get; set; } = 1;
    public string Status { get; set; } = "Connected";
    public string Method { get; set; } = "Manual";
    public string? ExternalAccount { get; set; }
    public string? Workspace { get; set; }
    public string? PreferredModel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PersonalAiSelection
{
    public Guid OwnerId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public Guid? ConnectionId { get; set; }
    public string Mode { get; set; } = "Institutional";
    public string? Model { get; set; }
    public bool PaidAccepted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// Immutable snapshot: no credential or academic content.
public sealed class AiInferenceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public Guid ConnectionId { get; set; }
    public long CredentialRevision { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public bool PaidAccepted { get; set; }
    public bool StrictSchema { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PersonalAiOAuth
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid OrganizationId { get; set; } = PilotOrganization.Id;
    public string StateHash { get; set; } = "";
    public string BrowserHash { get; set; } = "";
    public string SessionHash { get; set; } = "";
    public string StampHash { get; set; } = "";
    public string ProtectedVerifier { get; set; } = "";
    public string? ProtectedCode { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class PersonalAiModel
{
    public int ContextWindow { get; set; }
    public int MaxOutputTokens { get; set; }
    public string Provider { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Compatible { get; set; }
    public bool Free { get; set; }
    public bool PriceKnown { get; set; }
    public bool StrictSchema { get; set; }
    public string Availability { get; set; } = "Metadata";
    public DateTimeOffset CheckedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
