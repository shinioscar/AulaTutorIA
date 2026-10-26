namespace AulaTutor.Domain.Tutoring;

// Private Student material. It is never a teacher LessonVersion.
public sealed class PreparedLearning
{
    public Guid SessionId { get; set; }
    public string Length { get; set; } = "Short";
    public string ContractVersion { get; set; } = "prepared-learning-v1";
    public string? ContentJson { get; set; }
    public string? ContentHash { get; set; }
    public int CurrentActivity { get; set; }
    public int ContextWindow { get; set; }
    public int MaxOutputTokens { get; set; }
    public string? EvaluationJson { get; set; }
    public string? EvaluationEvidenceHash { get; set; }
}
