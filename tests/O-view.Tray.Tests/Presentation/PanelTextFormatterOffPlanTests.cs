using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>This skin's own off-plan-banner wording (OVI-168); cross-skin facts live in the golden-master harness.</summary>
public class PanelTextFormatterOffPlanTests
{
    private static readonly TokenCount Tokens = new(500_000, UsageValueStatus.Real);
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DivergingWithNullRisePointsNeverRendersAFabricatedZero()
    {
        // A Diverging reading with a null rise cannot happen from DivergenceDetector today, but
        // the type permits it, and the private DivergenceDetail helper must still not invent a
        // "0 points" clause if it ever does (ADR-0001's whole reason for making the field nullable).
        var divergence = new DivergenceReading(DivergenceState.Diverging, Tokens, null);

        var detail = PanelTextFormatter.OffPlanDetail(divergence, null, UtcNow, TimeZoneInfo.Utc);

        Assert.DoesNotContain("0 point", detail);
        Assert.Contains("500.0K", detail);
    }

    [Fact]
    public void TitleIsEmptyWhenDivergenceIsNotOffPlan()
    {
        var divergence = new DivergenceReading(DivergenceState.InsufficientActivity, Tokens, null);

        Assert.Equal("", PanelTextFormatter.OffPlanTitle(divergence, null));
        Assert.Equal("", PanelTextFormatter.OffPlanDetail(divergence, null, UtcNow, TimeZoneInfo.Utc));
    }

    [Fact]
    public void OffPlanHintNamesTheCoreOwnedCreditBilledModelIds()
    {
        var hint = PanelTextFormatter.OffPlanHint(new UsageFlag(true, UsageValueStatus.Real));

        foreach (var id in CreditBilledModelIds.All)
        {
            Assert.Contains(id, hint);
        }
    }

    /// <summary>
    /// OVI-702 review: <see cref="UsageValueStatus.Unavailable"/> ("Core could not look") must
    /// not render the same caption as <c>(false, Real)</c> ("Core looked and found none") — the
    /// former is an unknown, not a checked negative.
    /// </summary>
    [Fact]
    public void OffPlanHintStatesUnknownRatherThanACheckedNegativeWhenUnavailable()
    {
        var unavailable = PanelTextFormatter.OffPlanHint(new UsageFlag(null, UsageValueStatus.Unavailable));
        var checkedNegative = PanelTextFormatter.OffPlanHint(new UsageFlag(false, UsageValueStatus.Real));

        Assert.NotEqual(unavailable, checkedNegative);
        Assert.DoesNotContain("No credit-billed usage", unavailable);
        Assert.Contains("could not determine", unavailable);
    }

    [Fact]
    public void EstTodayLabelFlipsWithOffPlanState()
    {
        Assert.Equal("Est. value today", PanelTextFormatter.EstTodayLabel(false));
        Assert.Equal("Est. spend today", PanelTextFormatter.EstTodayLabel(true));
    }

    [Fact]
    public void OffPlanNoteIsEmptyWhenNotOffPlan()
    {
        Assert.Equal("", PanelTextFormatter.OffPlanNote(false));
        Assert.NotEmpty(PanelTextFormatter.OffPlanNote(true));
    }
}
