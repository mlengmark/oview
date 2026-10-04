using OView.App;
using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 slice 7's formatter (OVI-391). Mirrors the content facts
/// <c>AlertFixtures</c> (tests/O-view.CrossSkin.Tests, OVI-324) already pins for each
/// <see cref="UsageEventKind"/> — this project does not reference that fixture project, so the
/// facts are restated here directly against the same event shapes.
/// </summary>
public class AlertToastFormatterTests
{
    [Fact]
    public void ThresholdCrossedNamesTheBandItCrossedInto()
    {
        var content = AlertToastFormatter.Format(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red });

        Assert.Contains("Red", content.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OffPlanEnteredIncludesThePlanRisePointsDigit()
    {
        var divergence = new DivergenceReading(
            DivergenceState.Diverging,
            new TokenCount(2_000_000, UsageValueStatus.Real),
            PlanRisePoints: 7);

        var content = AlertToastFormatter.Format(new UsageEvent(UsageEventKind.OffPlanEntered) { Divergence = divergence });

        Assert.Contains("7", content.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void OffPlanEnteredWithNoMeasurableRiseStillProducesBodyText()
    {
        var divergence = new DivergenceReading(
            DivergenceState.Diverging,
            new TokenCount(500_000, UsageValueStatus.Real),
            PlanRisePoints: null);

        var content = AlertToastFormatter.Format(new UsageEvent(UsageEventKind.OffPlanEntered) { Divergence = divergence });

        Assert.False(string.IsNullOrWhiteSpace(content.Body));
    }

    [Fact]
    public void UpdateAvailableProducesNonEmptyTitleAndBody()
    {
        var content = AlertToastFormatter.Format(new UsageEvent(UsageEventKind.UpdateAvailable));

        Assert.False(string.IsNullOrWhiteSpace(content.Title));
        Assert.False(string.IsNullOrWhiteSpace(content.Body));
    }

    [Fact]
    public void InputDegradedNamesTheProvider()
    {
        var health = new ProviderHealth("ccusage-cli", ProviderHealthOutcome.Failed, LastSuccessAt: null, ConsecutiveFailures: 3);

        var content = AlertToastFormatter.Format(new UsageEvent(UsageEventKind.InputDegraded) { Health = health });

        Assert.Contains("ccusage-cli", content.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKindProducesANonEmptyTitle()
    {
        foreach (UsageEventKind kind in Enum.GetValues<UsageEventKind>())
        {
            var content = AlertToastFormatter.Format(new UsageEvent(kind));

            Assert.False(string.IsNullOrWhiteSpace(content.Title));
        }
    }
}
