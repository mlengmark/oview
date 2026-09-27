using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>ADR-0001's 2026-09-27 (OVI-168) off-plan-banner fields: defaults are unavailable/null, never fabricated.</summary>
public class OffPlanContractTests
{
    [Fact]
    public void OffPlanFieldsDefaultToUnavailable()
    {
        var stats = UsageStatistics.Unavailable;

        Assert.Null(stats.Divergence);
        Assert.Equal(UsageValueStatus.Unavailable, stats.HasCreditUsage.Status);
        Assert.Null(stats.HasCreditUsage.Value);
        Assert.Equal(UsageValueStatus.Unavailable, stats.OffPlanUsageAmount.Status);
        Assert.Null(stats.OffPlanUsageAmount.Value);
    }

    [Fact]
    public void ExtraUsageDefaultsToNullOnAFreshSnapshot()
    {
        Assert.Null(UsageSnapshot.Unavailable.ExtraUsage);
    }

    [Theory]
    [InlineData(DivergenceState.Diverging, true)]
    [InlineData(DivergenceState.PlanLimitReached, true)]
    [InlineData(DivergenceState.Consistent, false)]
    [InlineData(DivergenceState.InsufficientActivity, false)]
    [InlineData(DivergenceState.MeterNotReporting, false)]
    [InlineData(DivergenceState.RiseNotMeasurable, false)]
    public void IsOffPlanIsTrueOnlyForDivergingOrPlanLimitReached(DivergenceState state, bool expected)
    {
        var reading = new DivergenceReading(state, new TokenCount(1, UsageValueStatus.Real), 1);

        Assert.Equal(expected, reading.IsOffPlan);
    }

    [Fact]
    public void HasCreditUsageFalseWithRealIsADifferentFactFromUnavailable()
    {
        var found = new UsageFlag(false, UsageValueStatus.Real);
        var unknown = new UsageFlag(null, UsageValueStatus.Unavailable);

        Assert.Equal(UsageValueStatus.Real, found.Status);
        Assert.False(found.Value);
        Assert.Equal(UsageValueStatus.Unavailable, unknown.Status);
        Assert.Null(unknown.Value);
    }

    [Fact]
    public void UsageFlagThrowsWhenUnavailableCarriesAValue() =>
        Assert.Throws<ArgumentException>(() => new UsageFlag(true, UsageValueStatus.Unavailable));

    [Fact]
    public void ExtraUsageStateHasNoUnknownMember() =>
        Assert.Equal(new[] { "Disabled", "Enabled" }, Enum.GetNames<ExtraUsageState>());

    [Fact]
    public void CreditBilledModelIdsAreVerbatimVendorIdsNotDisplayNames()
    {
        Assert.Contains("claude-fable-5", CreditBilledModelIds.All);
        Assert.All(CreditBilledModelIds.All, id => Assert.DoesNotContain(" ", id));
    }
}
