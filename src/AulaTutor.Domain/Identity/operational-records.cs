namespace AulaTutor.Domain.Identity;

public sealed class AiReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? GroupId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? RetryNotBefore { get; set; }
    public string State { get; set; } = "Reserved";
    public int EstimatedTokens { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public long DurationMs { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Result { get; set; } = "Committed";
    public string CorrelationId { get; set; } = "";
    public string Change { get; set; } = "";
}
