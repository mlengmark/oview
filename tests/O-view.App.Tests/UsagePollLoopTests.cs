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
    public void A_normal_tick_raises_SnapshotUpdated_with_the_exact_new_snapshot()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var expected = Snapshot(clock.UtcNow);
        UsageSnapshot? observed = null;

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => expected),
            clock,
            timer,
            TimeSpan.FromMinutes(5));
        loop.SnapshotUpdated += (_, snapshot) => observed = snapshot;

        timer.RaiseElapsed();

        Assert.Equal(expected, observed);
    }

    [Fact]
    public void A_throwing_provider_does_not_raise_SnapshotUpdated()
    {
        var timer = new FakeAppTimer();
        var raised = false;

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => throw new InvalidOperationException("simulated provider failure")),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(5));
        loop.SnapshotUpdated += (_, _) => raised = true;

        timer.RaiseElapsed();

        Assert.False(raised);
    }

    [Fact]
    public void PollNow_polls_immediately_without_waiting_for_the_timer()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var expected = Snapshot(clock.UtcNow);

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => expected),
            clock,
            timer,
            TimeSpan.FromMinutes(5));

        loop.PollNow();

        Assert.Equal(expected, loop.CurrentSnapshot);
    }

    [Fact]
    public void PollNow_raises_SnapshotUpdated_with_the_exact_new_snapshot()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var expected = Snapshot(clock.UtcNow);
        UsageSnapshot? observed = null;

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(_ => expected),
            clock,
            timer,
            TimeSpan.FromMinutes(5));
        loop.SnapshotUpdated += (_, snapshot) => observed = snapshot;

        loop.PollNow();

        Assert.Equal(expected, observed);
    }

    [Fact]
    public void A_throwing_PollNow_leaves_the_previous_snapshot_untouched_and_does_not_raise_SnapshotUpdated()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var firstSnapshot = Snapshot(clock.UtcNow);
        var callCount = 0;
        var raised = false;

        using var loop = new UsagePollLoop(
            new FakeUsageProvider(utcNow =>
            {
                callCount++;
                return callCount == 1
                    ? firstSnapshot
                    : throw new InvalidOperationException("simulated provider failure");
            }),
            clock,
            timer,
            TimeSpan.FromMinutes(5));
        timer.RaiseElapsed();
        loop.SnapshotUpdated += (_, _) => raised = true;

        loop.PollNow();

        Assert.Equal(firstSnapshot, loop.CurrentSnapshot);
        Assert.False(raised);
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
    public async Task Dispose_waits_for_an_in_flight_poll_to_finish_before_returning()
    {
        // ADR-0009 D7 (OVI-469): AppShell.Quit disposes the stores only after this type's
        // Dispose returns, so Dispose must not return while another thread is still inside
        // Poll — a quit that let it return early could let the shell dispose a store while
        // that poll is still reading from (or about to write to) it.
        var pollEntered = new ManualResetEventSlim(false);
        var releasePoll = new ManualResetEventSlim(false);
        var timer = new FakeAppTimer();
        var loop = new UsagePollLoop(
            new FakeUsageProvider(_ =>
            {
                pollEntered.Set();
                releasePoll.Wait(TimeSpan.FromSeconds(5));
                return UsageSnapshot.Unavailable;
            }),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(5));

        var pollTask = Task.Run(loop.PollNow);
        Assert.True(pollEntered.Wait(TimeSpan.FromSeconds(5)), "the poll never started");

        var disposeTask = Task.Run(loop.Dispose);
        var disposeFinishedEarly = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromMilliseconds(200))) == disposeTask;
        Assert.False(disposeFinishedEarly, "Dispose returned while the poll was still in flight");

        releasePoll.Set();

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await pollTask.WaitAsync(TimeSpan.FromSeconds(5));
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
