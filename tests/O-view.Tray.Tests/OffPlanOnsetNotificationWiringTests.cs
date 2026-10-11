using OView.App;
using OView.Core.Models;
using OView.Core.Statistics;
using OView.Core.Updates;
using OView.Tray.Presentation;

namespace OView.Tray.Tests;

/// <summary>
/// G7 parity P16 (OVI-701): proves the full production chain — <see cref="UsageEventDecider"/>
/// through <see cref="TrayShellToSkin.RaiseEvent"/> through <see cref="AlertToastController"/>,
/// wired exactly as <c>Program.cs</c> wires them — shows exactly one toast for one off-plan
/// onset, no matter how many poll ticks happen while it stays on. Each piece already has its
/// own unit test (<c>UsageEventDeciderTests</c>'s dedup, <c>TrayShellToSkinTests</c>'s
/// passthrough, <c>AlertToastControllerTests</c>'s one-call-one-toast rule); this is the seam
/// those tests do not cover, where a correct decider wired to a correct controller could still
/// be invoked wrong and double-fire.
/// </summary>
public sealed class OffPlanOnsetNotificationWiringTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;

    [Fact]
    public void RepeatedPollsDuringOneOnsetShowExactlyOneToast()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new TrayShellToSkin();
        var shown = new List<(string Title, string Body)>();
        var alertToast = new AlertToastController((title, body) => shown.Add((title, body)));
        skin.EventRaised += alertToast.OnEventRaised;

        for (var i = 0; i < 5; i++)
        {
            foreach (var usageEvent in decider.OnPollSucceeded(Snapshot(), [], ShellSettings.Default))
            {
                skin.RaiseEvent(usageEvent);
            }
        }

        Assert.Single(shown);
    }

    [Fact]
    public void ReturningToPlanThenDivergingAgainShowsASecondToast()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new TrayShellToSkin();
        var shown = new List<(string Title, string Body)>();
        var alertToast = new AlertToastController((title, body) => shown.Add((title, body)));
        skin.EventRaised += alertToast.OnEventRaised;

        Poll(decider, skin);
        statistics.Divergence = Consistent();
        Poll(decider, skin);
        statistics.Divergence = Diverging();
        Poll(decider, skin);

        Assert.Equal(2, shown.Count);
    }

    /// <summary>
    /// This repository has no rate card (OVI-592), so nothing can be billed — the balloon must
    /// never claim an "Est." value it cannot evidence, in every onset it fires for.
    /// </summary>
    [Fact]
    public void TheOnsetToastNeverClaimsAnEstimatedValue()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new TrayShellToSkin();
        var shown = new List<(string Title, string Body)>();
        var alertToast = new AlertToastController((title, body) => shown.Add((title, body)));
        skin.EventRaised += alertToast.OnEventRaised;

        Poll(decider, skin);

        var toast = Assert.Single(shown);
        Assert.DoesNotContain("Est.", toast.Body, StringComparison.Ordinal);
    }

    private static void Poll(UsageEventDecider decider, TrayShellToSkin skin)
    {
        foreach (var usageEvent in decider.OnPollSucceeded(Snapshot(), [], ShellSettings.Default))
        {
            skin.RaiseEvent(usageEvent);
        }
    }

    private static UsageSnapshot Snapshot() => new(
        DataSourceKind.Live,
        DateTimeOffset.UnixEpoch,
        new UsagePercent(0, UsageValueStatus.Real),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(0, UsageValueStatus.Real),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Green);

    private static DivergenceReading Diverging() => new(
        DivergenceState.Diverging,
        new TokenCount(1000, UsageValueStatus.Real),
        PlanRisePoints: 5);

    private static DivergenceReading Consistent() => new(
        DivergenceState.Consistent,
        new TokenCount(0, UsageValueStatus.Real),
        PlanRisePoints: null);

    private sealed class FakeStatisticsSource : IUsageStatisticsSource
    {
        public FakeStatisticsSource(DivergenceReading? divergence) => Divergence = divergence;

        public DivergenceReading? Divergence { get; set; }

        public UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            UsageStatistics.Unavailable with { Divergence = Divergence };

        public ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            ModelUsageBreakdown.Unavailable;

        public DailyUsageSeries GetDailySeries(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            DailyUsageSeries.Unavailable;

        public TokenKindTotals GetTokenKindTotals(DateTimeOffset utcNow, TimeZoneInfo zone, StatisticsWindow window) =>
            TokenKindTotals.Unavailable;

        public WeeklyResetBoundaries GetResetBoundaries(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            WeeklyResetBoundaries.Unavailable;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
