using OView.App;
using OView.Core.Models;
using OView.Core.Providers;
using OView.Linux;

namespace OView.Linux.Tests;

/// <summary>
/// Proves ADR-0008 slice 8's (OVI-397) seam end to end against fakes only: composing a
/// <see cref="UsagePollLoop"/> with a <see cref="LinuxShellToSkin"/> through
/// <see cref="LinuxSkinHost.Compose"/> means every snapshot the loop produces reaches the
/// skin's <see cref="IShellToSkin.ShowSnapshot"/> — no Avalonia message loop, no real timer,
/// no real provider. Independently written from <c>TraySkinHostTests</c> (D1), the same shape
/// slice 3 proved for <c>O-view.Tray</c>.
/// </summary>
public class LinuxSkinHostTests
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
    public void Compose_pushes_the_pre_first_tick_snapshot_to_the_skin_immediately()
    {
        var (pollLoop, skin) = LinuxSkinHost.Compose(
            new FakeUsageProvider(_ => UsageSnapshot.Unavailable),
            new FakeClock(DateTimeOffset.UnixEpoch),
            new FakeAppTimer(),
            TimeSpan.FromMinutes(1));
        using var _ = pollLoop;

        Assert.Equal(UsageSnapshot.Unavailable, skin.LastSnapshot);
    }

    [Fact]
    public void A_poll_tick_reaches_the_skin_through_ShowSnapshot()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var timer = new FakeAppTimer();
        var expected = Snapshot(clock.UtcNow);

        var (pollLoop, skin) = LinuxSkinHost.Compose(
            new FakeUsageProvider(_ => expected),
            clock,
            timer,
            TimeSpan.FromMinutes(1));
        using var _ = pollLoop;

        timer.RaiseElapsed();

        Assert.Equal(expected, skin.LastSnapshot);
    }

    [Fact]
    public void A_throwing_poll_does_not_reach_the_skin()
    {
        var timer = new FakeAppTimer();

        var (pollLoop, skin) = LinuxSkinHost.Compose(
            new FakeUsageProvider(_ => throw new InvalidOperationException("simulated provider failure")),
            new FakeClock(DateTimeOffset.UnixEpoch),
            timer,
            TimeSpan.FromMinutes(1));
        using var _ = pollLoop;

        timer.RaiseElapsed();

        Assert.Equal(UsageSnapshot.Unavailable, skin.LastSnapshot);
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
