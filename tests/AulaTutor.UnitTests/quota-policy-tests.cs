using AulaTutor.Infrastructure.Ai;

namespace AulaTutor.UnitTests;

public sealed class QuotaPolicyTests
{
    [Theory]
    [InlineData("2026-10-23T00:00:10Z", "2026-10-22T23:59:10Z")]
    [InlineData("2026-10-23T00:00:00Z", "2026-10-22T23:59:00Z")]
    [InlineData("2026-10-23T12:00:00Z", "2026-10-23T00:00:00Z")]
    public void RollingProviderWindowDoesNotResetPrematurelyAtUtcMidnight(string timestamp, string since)
    { Assert.Equal(DateTimeOffset.Parse(since), PersistentAiAdmission.WindowStart(DateTimeOffset.Parse(timestamp))); }
    [Fact] public void InvalidPoliciesCannotDisableQuotaByZeroNegativeOrUnboundedConcurrency()
    {
        var policy = new AiQuotaOptions(); Assert.True(policy.Valid());
        policy.Limits.ProviderPerMinute = 0; Assert.False(policy.Valid()); policy.Limits.ProviderPerMinute = 20;
        policy.Limits.GlobalConcurrency = 33; Assert.False(policy.Valid()); policy.Limits.GlobalConcurrency = 2;
        policy.GroupDailyLimits[Guid.Empty] = 1; Assert.False(policy.Valid());
    }
}
