using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AulaTutor.Application.Ai;
using AulaTutor.Domain.Tutoring;

namespace AulaTutor.Application.Tutoring;

public static class PreparedRules
{
    public const string Version = "prepared-learning-v1";
    public const string EvaluationVersion = "prepared-evaluation-v1";
    public static bool IsPrepared(TutoringSession s) => s.LearningModality == "Prepared";
    public static int Count(string experience, string length) => (experience, length) switch
    {
        ("Tutoring", "Short") => 3, ("Tutoring", "Medium") => 6, ("Tutoring", "Long") => 9,
        ("Diagnostic", "Short") => 4, ("Diagnostic", "Medium") => 8, ("Diagnostic", "Long") => 12,
        _ => throw new ArgumentException("Extensión no válida.")
    };
    public static int Output(string experience, string length) => (experience, length) switch
    {
        (_, "Short") => 4096, ("Tutoring", "Medium") => 8192, ("Tutoring", "Long") => 12288,
        ("Diagnostic", "Medium") => 6144, ("Diagnostic", "Long") => 8192,
        _ => throw new ArgumentException("Extensión no válida.")
    };
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Fits(string input, int output, int contextWindow, int maxOutput) =>
        output <= maxOutput && Encoding.UTF8.GetByteCount(input) + output + 512 <= contextWindow;
    private static void Require(bool ok) { if (!ok) throw new JsonException(); }
    private static void Text(string? value, int max, bool optional = false) => Require(value is not null &&
        (optional || !string.IsNullOrWhiteSpace(value)) && value.Length <= max && !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')));
    public static void Validate(PreparedContent content, string experience, string length)
    {
        Require(LearningJson.Write(content).Length <= 64000 && content.Format == Version);
        Text(content.Introduction, 600);
        Require(content.Objectives is { Length: >= 3 and <= 5 } && content.Activities?.Length == Count(experience, length));
        var objectives = new HashSet<string>(StringComparer.Ordinal); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in content.Objectives!) { Text(o.Id, 40); Text(o.Title, 100); Require(objectives.Add(o.Id)); }
        Require(content.Objectives.Select(o => o.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() == content.Objectives.Length);
        foreach (var a in content.Activities!)
        {
            Text(a.Id, 40); Require(ids.Add(a.Id) && objectives.Contains(a.ObjectiveId));
            LearningOrchestrator.ValidateExercise(a.Exercise); Text(a.ReferenceSolution, 600);
            Require(a.Criteria is { Length: >= 1 and <= 3 }); foreach (var c in a.Criteria!) Text(c, 200);
            var aid = a.Aids; Require(aid.ExerciseId == a.Id);
            Text(a.Explanation, 600, experience == "Diagnostic"); Text(a.Example, 400, experience == "Diagnostic");
            Require(aid.Hints is not null && aid.Alternatives is not null && aid.CommonErrors is not null && aid.ReinforcementSuggestions is not null);
            if (experience == "Diagnostic")
            {
                Require(a.Explanation == "" && a.Example == "" && aid.Hints!.Length == 0 && aid.Alternatives!.Length == 0 &&
                    aid.CommonErrors!.Length == 0 && aid.ReinforcementSuggestions!.Length == 0 && aid.CorrectFeedback == "" && aid.IncorrectFeedback == "" && aid.ReviewFeedback == "");
            }
            else
            {
                Require(aid.Hints!.Length is >= 1 and <= 3 && aid.Alternatives!.Length is >= 1 and <= 3 && aid.CommonErrors!.Length <= 3 && aid.ReinforcementSuggestions!.Length <= 3);
                foreach (var t in aid.Hints!.Concat(aid.Alternatives!).Concat(aid.CommonErrors!).Concat(aid.ReinforcementSuggestions!)) Text(t, 400);
                Text(aid.CorrectFeedback, 400); Text(aid.IncorrectFeedback, 400); Text(aid.ReviewFeedback, 400);
                Require(!a.Exercise.AcceptedAnswers.Any(answer => string.Equals(answer.Trim(), aid.Hints[0].Trim(), StringComparison.OrdinalIgnoreCase) ||
                    answer.Trim().Length >= 3 && aid.Hints[0].Contains(answer.Trim(), StringComparison.OrdinalIgnoreCase)));
            }
        }
        Require(objectives.All(id => content.Activities!.Any(a => a.ObjectiveId == id)));
    }
    public static void ValidateFindings(PreparedFinding[] findings, PreparedContent content, IReadOnlyDictionary<Guid, string> evidenceObjectives)
    {
        Require(findings is not null && findings.Length <= content.Objectives.Length && findings.Select(f => f.ObjectiveId).Distinct().Count() == findings.Length);
        foreach (var f in findings!)
        {
            Require(content.Objectives.Any(o => o.Id == f.ObjectiveId)); Text(f.Text, 600);
            Require(f.EvidenceIds is { Length: >= 1 and <= 8 } && f.EvidenceIds.Distinct().Count() == f.EvidenceIds.Length &&
                f.EvidenceIds.All(id => evidenceObjectives.TryGetValue(id, out var objective) && objective == f.ObjectiveId));
        }
    }
}
