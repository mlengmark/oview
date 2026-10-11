using OView.App;
using OView.Core.Models;
using OView.Core.Statistics;
using OView.Core.Updates;
using OView.Linux.Presentation;

namespace OView.Linux.Tests;

/// <summary>
/// G7 parity P16 (OVI-701): proves the full production chain — <see cref="UsageEventDecider"/>
/// through <see cref="LinuxShellToSkin.RaiseEvent"/> through
/// <see cref="AlertNotificationController"/>, wired exactly as <c>Program.cs</c> wires them —
/// sends exactly one notification for one off-plan onset, no matter how many poll ticks happen
/// while it stays on. Each piece already has its own unit test
/// (<c>UsageEventDeciderTests</c>'s dedup, <c>LinuxShellToSkinTests</c>'s passthrough,
/// <c>AlertNotificationControllerTests</c>'s one-call-one-notification rule); this is the seam
/// those tests do not cover, where a correct decider wired to a correct controller could still
/// be invoked wrong and double-fire.
/// </summary>
public sealed class OffPlanOnsetNotificationWiringTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;

    [Fact]
    public void RepeatedPollsDuringOneOnsetSendExactlyOneNotification()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new LinuxShellToSkin();
        var sent = new List<(string Summary, string Body)>();
        var controller = new AlertNotificationController((summary, body) =>
        {
            sent.Add((summary, body));
            return Task.CompletedTask;
        });
        skin.EventRaised += controller.OnEventRaised;

        for (var i = 0; i < 5; i++)
        {
            foreach (var usageEvent in decider.OnPollSucceeded(Snapshot(), [], ShellSettings.Default))
            {
                skin.RaiseEvent(usageEvent);
            }
        }

        Assert.Single(sent);
    }

    [Fact]
    public void ReturningToPlanThenDivergingAgainSendsASecondNotification()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new LinuxShellToSkin();
        var sent = new List<(string Summary, string Body)>();
        var controller = new AlertNotificationController((summary, body) =>
        {
            sent.Add((summary, body));
            return Task.CompletedTask;
        });
        skin.EventRaised += controller.OnEventRaised;

        Poll(decider, skin);
        statistics.Divergence = Consistent();
        Poll(decider, skin);
        statistics.Divergence = Diverging();
        Poll(decider, skin);

        Assert.Equal(2, sent.Count);
    }

    /// <summary>
    /// This repository has no rate card (OVI-592), so nothing can be billed — the notification
    /// must never claim an "Est." value it cannot evidence, in every onset it fires for.
    /// </summary>
    [Fact]
    public void TheOnsetNotificationNeverClaimsAnEstimatedValue()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);
        var skin = new LinuxShellToSkin();
        var sent = new List<(string Summary, string Body)>();
        var controller = new AlertNotificationController((summary, body) =>
        {
            sent.Add((summary, body));
            return Task.CompletedTask;
        });
        skin.EventRaised += controller.OnEventRaised;

        Poll(decider, skin);

        var notification = Assert.Single(sent);
        Assert.DoesNotContain("Est.", notification.Body, StringComparison.Ordinal);
    }

    private static void Poll(UsageEventDecider decider, LinuxShellToSkin skin)
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
