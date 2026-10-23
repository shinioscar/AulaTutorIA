using AulaTutor.Application.Lessons;
using AulaTutor.Domain.Lessons;
using AulaTutor.Domain.Tutoring;

namespace AulaTutor.UnitTests;

public sealed class SupervisionTests
{
    [Theory]
    [InlineData(AttemptOutcome.CorrectWithoutHelp, true, true)]
    [InlineData(AttemptOutcome.CorrectWithHelp, true, true)]
    [InlineData(AttemptOutcome.NeedsReview, false, true)]
    [InlineData(AttemptOutcome.NotEvaluable, false, false)]
    [InlineData(AttemptOutcome.Incorrect, false, false)]
    public void DeliveryAndRuleVerificationRemainDifferent(AttemptOutcome outcome, bool verified, bool delivered)
    { Assert.Equal(verified, SupervisionRules.Verified(outcome)); Assert.Equal(delivered, SupervisionRules.Delivered([new() { Outcome = outcome }])); }
    [Fact] public void RepeatedErrorsAndExhaustionHaveExplicitThresholds()
    {
        ExerciseAttempt[] attempts = Enumerable.Range(1, 10).Select(n => new ExerciseAttempt { Number = n, Outcome = AttemptOutcome.Incorrect }).ToArray();
        Assert.Empty(SupervisionRules.Reasons(attempts[..2], false));
        Assert.Equal(["RepeatedErrors"], SupervisionRules.Reasons(attempts[..3], false));
        Assert.Equal(["RepeatedErrors", "AttemptsExhausted"], SupervisionRules.Reasons(attempts, false));
        attempts[9].Outcome = AttemptOutcome.CorrectWithHelp; Assert.Empty(SupervisionRules.Reasons(attempts, false));
    }
    [Theory] [InlineData(AttemptOutcome.NeedsReview,"RequiresHumanReview")] [InlineData(AttemptOutcome.NotEvaluable,"NotEvaluable")]
    public void UnverifiedAnswersRequireHumanAttention(AttemptOutcome outcome, string reason)
    { Assert.Equal([reason], SupervisionRules.Reasons([new() { Outcome = outcome }], false)); }
    [Fact] public void TeacherCanFlagWithoutInventingAnAutomaticError()
    { Assert.Equal(["TeacherFlagged"], SupervisionRules.Reasons([], true)); }
    [Fact] public void ANewAttemptReopensCoverageOfAnOlderReview()
    {
        var first = new ExerciseAttempt { Number = 1 }; var second = new ExerciseAttempt { Number = 2 };
        var entry = new TeacherReviewEntry { State = TeacherReviewState.Reviewed, Attention = AttentionState.Closed };
        Assert.True(SupervisionRules.CoversLatest(entry, [first.Id], [first]));
        Assert.False(SupervisionRules.CoversLatest(entry, [first.Id], [first, second]));
        Assert.True(SupervisionRules.CoversLatest(entry, [first.Id, second.Id], [second, first]));
        Assert.False(SupervisionRules.CoversLatest(null, [first.Id], [first]));
    }
}
