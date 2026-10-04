using OView.Linux.Platform;

namespace OView.Linux.Tests.Platform;

/// <summary>
/// Proves <see cref="NotificationHostMonitor"/> (ADR-0008 D6, OVI-397) against a fake
/// <see cref="ISessionBusNameWatcher"/> — no real session bus or display is reachable from
/// this test runner, so this is the whole proof D6's probe/watch orchestration gets here.
/// </summary>
public class NotificationHostMonitorTests
{
    [Fact]
    public async Task StartAsync_runs_the_probe_off_the_calling_thread()
    {
        var callingThreadId = Environment.CurrentManagedThreadId;
        int? probeThreadId = null;
        var watcher = new FakeSessionBusNameWatcher(onProbe: () => probeThreadId = Environment.CurrentManagedThreadId, observed: true);
        var monitor = new NotificationHostMonitor(watcher);

        await monitor.StartAsync(CancellationToken.None);

        Assert.NotNull(probeThreadId);
        Assert.NotEqual(callingThreadId, probeThreadId);
    }

    [Fact]
    public async Task When_the_host_is_present_ProbeCompleted_reports_true_and_the_watch_is_never_started()
    {
        var watcher = new FakeSessionBusNameWatcher(observed: true);
        var monitor = new NotificationHostMonitor(watcher);
        var probeResults = new List<bool>();
        var appeared = false;
        monitor.ProbeCompleted += probeResults.Add;
        monitor.HostAppeared += () => appeared = true;

        await monitor.StartAsync(CancellationToken.None);

        Assert.Equal([true], probeResults);
        Assert.True(monitor.LastObserved);
        Assert.False(appeared);
        Assert.Equal(0, watcher.WaitForOwnerCallCount);
    }

    [Fact]
    public async Task When_the_host_is_absent_ProbeCompleted_reports_false_and_the_monitor_then_watches_for_it()
    {
        var watchGate = new TaskCompletionSource();
        var watcher = new FakeSessionBusNameWatcher(observed: false, waitForOwner: watchGate.Task);
        var monitor = new NotificationHostMonitor(watcher);
        var probeResults = new List<bool>();
        var appeared = false;
        monitor.ProbeCompleted += probeResults.Add;
        monitor.HostAppeared += () => appeared = true;

        var started = monitor.StartAsync(CancellationToken.None);

        // The probe itself must report "absent" before the later-appearance watch completes
        // (and in this test, it never will unless we release the gate below) — proving the
        // monitor does not wait for a late appearance before reporting what it already knows.
        await WaitUntilAsync(() => probeResults.Count == 1);
        Assert.Equal([false], probeResults);
        Assert.False(monitor.LastObserved);
        Assert.False(appeared);
        Assert.Equal(1, watcher.WaitForOwnerCallCount);

        // No restart: the same monitor instance picks the name up once it appears later.
        watchGate.SetResult();
        await started;

        Assert.True(appeared);
        Assert.True(monitor.LastObserved);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    private sealed class FakeSessionBusNameWatcher : ISessionBusNameWatcher
    {
        private readonly bool _observed;
        private readonly Action? _onProbe;
        private readonly Task _waitForOwner;

        public FakeSessionBusNameWatcher(bool observed, Action? onProbe = null, Task? waitForOwner = null)
        {
            _observed = observed;
            _onProbe = onProbe;
            _waitForOwner = waitForOwner ?? Task.CompletedTask;
        }

        public int WaitForOwnerCallCount { get; private set; }

        public Task<bool> ProbeAsync(CancellationToken cancellationToken)
        {
            _onProbe?.Invoke();
            return Task.FromResult(_observed);
        }

        public Task WaitForOwnerAsync(CancellationToken cancellationToken)
        {
            WaitForOwnerCallCount++;
            return _waitForOwner;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
