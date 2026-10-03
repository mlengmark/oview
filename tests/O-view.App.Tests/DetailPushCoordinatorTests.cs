using OView.Core.Models;
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
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
        var snapshot = Snapshot(DateTimeOffset.UnixEpoch);
        coordinator.OnPollSucceeded(snapshot);
        skin.LastDetail = null;

        coordinator.OnRequestWidget(true);

        Assert.True(skin.LastVisible);
        Assert.Equal(new UsageDetail(snapshot, RealStatistics(), RealBreakdown()), skin.LastDetail);
        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public void RequestWidget_false_forwards_visibility_and_reads_nothing()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

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
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
        coordinator.OnRequestWidget(true);
        source.CallCount = 0;

        var polled = Snapshot(DateTimeOffset.UnixEpoch.AddMinutes(5));
        coordinator.OnPollSucceeded(polled);

        Assert.Equal(new UsageDetail(polled, RealStatistics(), RealBreakdown()), skin.LastDetail);
        Assert.Equal(2, source.CallCount);
    }

    [Fact]
    public void Hidden_poll_pushes_nothing_and_reads_the_ledger_zero_times()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnPollSucceeded(Snapshot(DateTimeOffset.UnixEpoch));

        Assert.Null(skin.LastDetail);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void Dismissing_then_polling_stops_pushing_and_reading()
    {
        var skin = new FakeSkin();
        var source = new FakeStatisticsSource(RealStatistics(), RealBreakdown());
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);
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
        var coordinator = new DetailPushCoordinator(source, skin, new FakeClock(DateTimeOffset.UnixEpoch), Zone);

        coordinator.OnRequestWidget(true);

        Assert.Equal(UsageDetail.Unavailable, skin.LastDetail);
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
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeSkin : IShellToSkin
    {
        public UsageDetail? LastDetail { get; set; }

        public bool? LastVisible { get; private set; }

        public void ShowSnapshot(UsageSnapshot snapshot)
        {
        }

        public void RaiseEvent(UsageEvent usageEvent)
        {
        }

        public void ShowDetail(UsageDetail detail) => LastDetail = detail;

        public void SetVisible(bool visible) => LastVisible = visible;

        public void Shutdown()
        {
        }
    }
}
