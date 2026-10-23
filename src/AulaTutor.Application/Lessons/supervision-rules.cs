using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;

namespace AulaTutor.Application.Lessons;

public static class SupervisionRules
{
    public static bool Verified(AttemptOutcome outcome) => outcome is AttemptOutcome.CorrectWithoutHelp or AttemptOutcome.CorrectWithHelp;
    public static bool Delivered(IEnumerable<ExerciseAttempt> attempts) => attempts.Any(a => Verified(a.Outcome) || a.Outcome == AttemptOutcome.NeedsReview);
    public static string[] Reasons(IReadOnlyList<ExerciseAttempt> attempts, bool flagged)
    {
        var reasons = new List<string>();
        var last = attempts.OrderBy(a => a.Number).LastOrDefault();
        if (last?.Outcome == AttemptOutcome.NeedsReview) reasons.Add("RequiresHumanReview");
        if (last?.Outcome == AttemptOutcome.NotEvaluable) reasons.Add("NotEvaluable");
        if (!attempts.Any(a => Verified(a.Outcome)))
        {
            if (attempts.Count(a => a.Outcome == AttemptOutcome.Incorrect) >= 3) reasons.Add("RepeatedErrors");
            if (attempts.Count >= 10) reasons.Add("AttemptsExhausted");
        }
        if (flagged) reasons.Add("TeacherFlagged");
        return reasons.ToArray();
    }
    // New answers invalidate the coverage of an earlier human decision.
    public static bool CoversLatest(TeacherReviewEntry? entry, IEnumerable<Guid> examined, IReadOnlyList<ExerciseAttempt> attempts)
    {
        var last = attempts.OrderBy(a => a.Number).LastOrDefault();
        return entry is not null && (last is null || examined.Contains(last.Id));
    }
}
