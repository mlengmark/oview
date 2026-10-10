using OView.Core.Models;
using OView.Core.Providers;
using OView.Core.Statistics;

namespace OView.App.Tests;

/// <summary>
/// Proves ADR-0008 D9b's shell-side rule against fakes only: assemble on widget-show and on
/// every successful poll while visible, never while hidden. The hidden-poll case is the one
/// this ADR amendment exists for — <see cref="Hidden_poll_pushes_nothing_and_reads_the_ledger_zero_times"/>
/// is the test that proves it.
/// </summary>
public sealed class DetailPushCoordinatorTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;
    private static readonly IAccountIdentitySource Identity = new FakeAccountIdentitySource(AccountIdentity.Unavailable);

    private static UsageSnapshot Snapshot(DateTimeOffset lastIngestAt) => new(
        DataSourceKind.Live,
        lastIngestAt,
        new UsagePercent(42, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(10, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Green);

    private static UsageStatistics RealStatistics() => new(
        new TokenCount(100, UsageValueStatus.Real),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new TokenCount(1000, UsageValueStatus.Real),
        new EstimatedUsd(null, UsageValueStatus.Unavailable),
        new HistoryCoverage(31, 31));

    private static ModelUsageBreakdown RealBreakdown() => new(
        DateOnly.MinValue,
        DateOnly.MinValue,
        Array.Empty<ModelUsageRow>(),
        new HistoryCoverage(31, 31),
        RateCardStamp.Unavailable,
        UsageValueStatus.Real);

    [Fact]
    public void RequestWidget_true_forwards_visibility_and_pushes_a_detail_built_from_the_last_snapshot()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
        var snapshot = Snapshot(DateTimeOffset.UnixEpoch);
        coordinator.OnPollSucceeded(snapshot);
        skin.LastDetail = null;

        coordinator.OnRequestWidget(true);

        Assert.True(skin.LastVisible);
        Assert.Equal(new UsageDetail(snapshot, RealStatistics(), RealBreakdown()), skin.LastDetail);
        Assert.Equal(6, source.CallCount);
    }

    [Fact]
    public void RequestWidget_false_forwards_visibility_and_reads_nothing()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnRequestWidget(false);

        Assert.False(skin.LastVisible);
        Assert.Null(skin.LastDetail);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void A_successful_poll_while_visible_pushes_a_fresh_detail()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
        coordinator.OnRequestWidget(true);
        source.CallCount = 0;

        var polled = Snapshot(DateTimeOffset.UnixEpoch.AddMinutes(5));
        coordinator.OnPollSucceeded(polled);

        Assert.Equal(new UsageDetail(polled, RealStatistics(), RealBreakdown()), skin.LastDetail);
        Assert.Equal(6, source.CallCount);
    }

    [Fact]
    public void Hidden_poll_pushes_nothing_and_reads_the_ledger_zero_times()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnPollSucceeded(Snapshot(DateTimeOffset.UnixEpoch));

        Assert.Null(skin.LastDetail);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void Dismissing_then_polling_stops_pushing_and_reading()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
        coordinator.OnRequestWidget(true);
        coordinator.OnRequestWidget(false);
        skin.LastDetail = null;
        source.CallCount = 0;

        coordinator.OnPollSucceeded(Snapshot(DateTimeOffset.UnixEpoch.AddMinutes(5)));

        Assert.Null(skin.LastDetail);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void A_failed_ledger_read_pushes_UsageDetail_Unavailable()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnRequestWidget(true);

        Assert.Equal(UsageDetail.Unavailable, skin.LastDetail);
    }

    /// <summary>
    /// Wires the detail window's account block (ADR-0008 D9e, gate G7 parity slice P8): the
    /// pushed <see cref="UsageDetail.Account"/> carries whatever <see cref="IAccountIdentitySource"/>
    /// returned, not a fabricated value and not always <see cref="AccountIdentity.Unavailable"/>.
    /// </summary>
    [Fact]
    public void A_known_identity_is_carried_onto_the_pushed_detail()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var identity = new FakeAccountIdentitySource(
            new AccountIdentity("Jane Doe", "jane@example.com", "claude_max", UsageValueStatus.Real));
        var coordinator = new DetailPushCoordinator(source, identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnRequestWidget(true);

        Assert.Equal("Jane Doe", skin.LastDetail?.Account.DisplayName);
    }

    /// <summary>
    /// The identity read is attempted even when the ledger read failed — the two sources are
    /// independent, so a missing statistics file must not also blank out an identity Core could
    /// read.
    /// </summary>
    [Fact]
    public void A_known_identity_is_carried_even_when_statistics_are_unavailable()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);
        var identity = new FakeAccountIdentitySource(
            new AccountIdentity("Jane Doe", "jane@example.com", "claude_max", UsageValueStatus.Real));
        var coordinator = new DetailPushCoordinator(source, identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnRequestWidget(true);

        Assert.Equal("Jane Doe", skin.LastDetail?.Account.DisplayName);
    }

    [Fact]
    public void OnIconActivated_opens_a_closed_widget()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, Identity, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnIconActivated();

        Assert.True(skin.LastVisible);
    }

    [Fact]
    public void OnIconActivated_closes_an_open_widget()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var coordinator = new DetailPushCoordinator(source, Identity, skin, clock, Zone);
        coordinator.OnIconActivated();

        coordinator.OnIconActivated();

        Assert.False(skin.LastVisible);
    }

    [Fact]
    public void A_click_399ms_after_a_focus_loss_close_is_absorbed_and_the_widget_stays_closed()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var coordinator = new DetailPushCoordinator(source, Identity, skin, clock, Zone);
        coordinator.OnRequestWidget(true);
        coordinator.OnRequestWidget(false); // the window's own Deactivated handler
        clock.UtcNow = clock.UtcNow.AddMilliseconds(399);
        var callsBeforeClick = skin.SetVisibleCallCount;

        coordinator.OnIconActivated();

        Assert.Equal(callsBeforeClick, skin.SetVisibleCallCount); // no SetVisible call at all — the click was absorbed
    }

    [Fact]
    public void A_click_401ms_after_a_focus_loss_close_reopens_the_widget()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var coordinator = new DetailPushCoordinator(source, Identity, skin, clock, Zone);
        coordinator.OnRequestWidget(true);
        coordinator.OnRequestWidget(false); // the window's own Deactivated handler
        clock.UtcNow = clock.UtcNow.AddMilliseconds(401);

        coordinator.OnIconActivated();

        Assert.True(skin.LastVisible);
    }

    private sealed class FakeStatisticsSource : IUsageStatisticsSource
    {
        private readonly UsageStatistics _statistics;
        private readonly ModelUsageBreakdown _breakdown;

        public FakeStatisticsSource(UsageStatistics statistics, ModelUsageBreakdown breakdown)
        {
            _statistics = statistics;
            _breakdown = breakdown;
        }

        public int CallCount { get; set; }

        public UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone)
        {
            CallCount++;
            return _statistics;
        }

        public ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone)
        {
            CallCount++;
            return _breakdown;
        }

        public DailyUsageSeries GetDailySeries(DateTimeOffset utcNow, TimeZoneInfo zone)
        {
            CallCount++;
            return DailyUsageSeries.Unavailable;
        }

        public TokenKindTotals GetTokenKindTotals(DateTimeOffset utcNow, TimeZoneInfo zone, StatisticsWindow window)
        {
            CallCount++;
            return TokenKindTotals.Unavailable;
        }

        public WeeklyResetBoundaries GetResetBoundaries(DateTimeOffset utcNow, TimeZoneInfo zone)
        {
            CallCount++;
            return WeeklyResetBoundaries.Unavailable;
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAccountIdentitySource : IAccountIdentitySource
    {
        private readonly AccountIdentity _identity;

        public FakeAccountIdentitySource(AccountIdentity identity) => _identity = identity;

        public AccountIdentity GetIdentity() => _identity;
    }

    private sealed class FakeSkin : IShellToSkin
    {
        public UsageDetail? LastDetail { get; set; }

        public bool? LastVisible { get; private set; }

        public int SetVisibleCallCount { get; private set; }

        public void ShowSnapshot(UsageSnapshot snapshot)
        {
        }

        public void RaiseEvent(UsageEvent usageEvent)
        {
        }

        public void ShowDetail(UsageDetail detail) => LastDetail = detail;

        public void SetVisible(bool visible)
        {
            LastVisible = visible;
            SetVisibleCallCount++;
        }

        public void Shutdown()
        {
        }
    }
}
