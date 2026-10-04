using OView.App;
using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// ADR-0008 slice 11's formatter (OVI-417). Mirrors the content facts
/// <c>AlertFixtures</c> (tests/O-view.CrossSkin.Tests, OVI-324) already pins for each
/// <see cref="UsageEventKind"/> — this project does not reference that fixture project, so the
/// facts are restated here directly against the same event shapes, the same approach
/// <c>O-view.Tray.Tests</c>'s own <c>AlertToastFormatterTests</c> (slice 7) already takes.
/// </summary>
public class AlertNotificationFormatterTests
{
    [Fact]
    public void ThresholdCrossedNamesTheBandItCrossedInto()
    {
        var content = AlertNotificationFormatter.Format(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red });

        Assert.Contains("Red", content.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OffPlanEnteredIncludesThePlanRisePointsDigit()
    {
        var divergence = new DivergenceReading(
            DivergenceState.Diverging,
            new TokenCount(2_000_000, UsageValueStatus.Real),
            PlanRisePoints: 7);

        var content = AlertNotificationFormatter.Format(new UsageEvent(UsageEventKind.OffPlanEntered) { Divergence = divergence });

        Assert.Contains("7", content.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void OffPlanEnteredWithNoMeasurableRiseStillProducesBodyText()
    {
        var divergence = new DivergenceReading(
            DivergenceState.Diverging,
            new TokenCount(500_000, UsageValueStatus.Real),
            PlanRisePoints: null);

        var content = AlertNotificationFormatter.Format(new UsageEvent(UsageEventKind.OffPlanEntered) { Divergence = divergence });

        Assert.False(string.IsNullOrWhiteSpace(content.Body));
    }

    [Fact]
    public void UpdateAvailableProducesNonEmptySummaryAndBody()
    {
        var content = AlertNotificationFormatter.Format(new UsageEvent(UsageEventKind.UpdateAvailable));

        Assert.False(string.IsNullOrWhiteSpace(content.Summary));
        Assert.False(string.IsNullOrWhiteSpace(content.Body));
    }

    [Fact]
    public void InputDegradedNamesTheProvider()
    {
        var health = new ProviderHealth("ccusage-cli", ProviderHealthOutcome.Failed, LastSuccessAt: null, ConsecutiveFailures: 3);

        var content = AlertNotificationFormatter.Format(new UsageEvent(UsageEventKind.InputDegraded) { Health = health });

        Assert.Contains("ccusage-cli", content.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKindProducesANonEmptySummary()
    {
        foreach (UsageEventKind kind in Enum.GetValues<UsageEventKind>())
        {
            var content = AlertNotificationFormatter.Format(new UsageEvent(kind));

            Assert.False(string.IsNullOrWhiteSpace(content.Summary));
        }
    }

    [Fact]
    public void WordingDoesNotMatchTheWindowsSkinsOwnPhrasing()
    {
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        var content = AlertNotificationFormatter.Format(usageEvent);

        Assert.NotEqual("Usage crossed into Red.", content.Body);
    }
}
