using OView.Core.Models;
using OView.Core.Providers;

namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0007 D2 points 4 and 9 against fakes only — no real timer, no real clock, no
/// I/O. <see cref="FakeAppTimer"/> raises <see cref="IAppTimer.Elapsed"/> itself so a test
/// controls exactly when a "tick" happens.
/// </summary>
public class UsagePollLoopTests
{
    private static UsageSnapshot Snapshot(DateTimeOffset lastIngestAt) => new(
        DataSourceKind.Live,
        lastIngestAt,
        new UsagePercent(42, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(10, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Green);

    [Fact]
    public void Construction_starts_the_timer_at_the_given_cadence()
    {
        var timer = new FakeAppTimer();
        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(5));

        Assert.True(timer.Started);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Interval);
    }

    [Fact]
    public void CurrentSnapshot_is_Unavailable_before_the_first_tick()
    {
        var timer = new FakeAppTimer();
        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(5));

        Assert.Equal(UsageSnapshot.Unavailable, loop.CurrentSnapshot);
    }

    [Fact]
    public void A_normal_tick_replaces_the_snapshot_using_the_injected_clock()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        DateTimeOffset? observedUtcNow = null;
        var expected = Snapshot(clock.UtcNow);

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(utcNow =>
            {
                observedUtcNow = utcNow;
                return expected;
            }),
            clock,
            timer,
            TimeSpan.FromMinutes(5));

        clock.UtcNow = DateTimeOffset.UnixEpoch.AddMinutes(5);
        timer.RaiseElapsed();

        Assert.Equal(expected, loop.CurrentSnapshot);
        Assert.Equal(clock.UtcNow, observedUtcNow);
    }

    [Fact]
    public void A_throwing_provider_leaves_the_previous_snapshot_untouched_and_the_loop_keeps_running()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var firstSnapshot = Snapshot(clock.UtcNow);
        var callCount = 0;

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(utcNow =>
            {
                callCount++;
                return callCount switch
                {
                    1 => firstSnapshot,
                    2 => throw new InvalidOperationException("simulated provider failure"),
                    _ => Snapshot(utcNow),
                };
            }),
            clock,
            timer,
            TimeSpan.FromMinutes(5));

        timer.RaiseElapsed();
        Assert.Equal(firstSnapshot, loop.CurrentSnapshot);

        timer.RaiseElapsed();
        Assert.Equal(firstSnapshot, loop.CurrentSnapshot);

        clock.UtcNow = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var thirdSnapshot = Snapshot(clock.UtcNow);
        timer.RaiseElapsed();
        Assert.Equal(thirdSnapshot, loop.CurrentSnapshot);
    }

    [Fact]
    public void Dispose_stops_and_disposes_the_timer()
    {
        var timer = new FakeAppTimer();
        var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(5));

        loop.Dispose();

        Assert.False(timer.Started);
        Assert.True(timer.Disposed);
    }

    [Fact]
    public void Dispose_unsubscribes_so_a_later_tick_does_not_update_the_snapshot()
    {
        var timer = new FakeAppTimer();
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var loop = new UsagePollLoop(
            new FakeUsageProvider(utcNow => Snapshot(utcNow)),
            clock,
            timer,
            TimeSpan.FromMinutes(5));

        loop.Dispose();
        var snapshotAtDispose = loop.CurrentSnapshot;

        timer.RaiseElapsed();

        Assert.Equal(snapshotAtDispose, loop.CurrentSnapshot);
    }

    private sealed class FakeUsageProvider : IUsageProvider
    {
        private readonly Func<DateTimeOffset, UsageSnapshot> _getSnapshot;

        public FakeUsageProvider(Func<DateTimeOffset, UsageSnapshot> getSnapshot) => _getSnapshot = getSnapshot;

        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow) => _getSnapshot(utcNow);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAppTimer : IAppTimer
    {
        public event EventHandler? Elapsed;

        public TimeSpan Interval { get; set; }

        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public void Start() => Started = true;

        public void Stop() => Started = false;

        public void RaiseElapsed() => Elapsed?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Disposed = true;
    }
}
