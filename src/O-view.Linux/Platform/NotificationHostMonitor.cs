namespace OView.Linux.Platform;

/// <summary>
/// ADR-0008 D6, this slice's own point (OVI-397): probes the session bus for a notification-
/// area host's well-known name off the calling thread (point 5 — never inline, never on a UI
/// thread), reports exactly what was observed (point 2/3), and — only when the name was
/// initially absent — watches for it to appear later and reports that too, without a restart
/// (point 4). Pure orchestration over <see cref="ISessionBusNameWatcher"/>; proven against a
/// fake, no real bus or display needed.
/// </summary>
public sealed class NotificationHostMonitor
{
    private readonly ISessionBusNameWatcher _watcher;

    public NotificationHostMonitor(ISessionBusNameWatcher watcher)
    {
        _watcher = watcher;
    }

    /// <summary>The most recent observation, or <see langword="null"/> before the first probe
    /// completes. Never set from a guess — only from what <see cref="ISessionBusNameWatcher"/>
    /// reported.</summary>
    public bool? LastObserved { get; private set; }

    /// <summary>Raised exactly once, right after the initial probe completes, with the
    /// observed fact.</summary>
    public event Action<bool>? ProbeCompleted;

    /// <summary>Raised exactly once, if and when a previously-absent host's name appears on
    /// the bus — the signal the Windows equivalent's <c>TaskbarCreated</c> re-registration
    /// gives for free and D6 point 4 asks this skin to reproduce itself.</summary>
    public event Action? HostAppeared;

    /// <summary>
    /// Starts the probe (and, if needed, the later-appearance watch) on a background thread.
    /// The returned <see cref="Task"/> represents that background work; it is not awaited by
    /// the caller on the UI thread — D6 point 5 forbids blocking the dispatcher on the D-Bus
    /// round trip, and awaiting this result on that thread would do exactly that once the
    /// absent-host branch starts waiting indefinitely for a name that may never appear.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Run(() => RunAsync(cancellationToken), cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var observed = await _watcher.ProbeAsync(cancellationToken).ConfigureAwait(false);
        LastObserved = observed;
        ProbeCompleted?.Invoke(observed);

        if (!observed)
        {
            await _watcher.WaitForOwnerAsync(cancellationToken).ConfigureAwait(false);
            LastObserved = true;
            HostAppeared?.Invoke();
        }
    }
}
