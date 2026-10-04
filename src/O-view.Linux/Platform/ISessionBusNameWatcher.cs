namespace OView.Linux.Platform;

/// <summary>
/// The mockable seam over a D-Bus well-known name's ownership (ADR-0008 D6): a probe for
/// whether the name currently has an owner, and a way to wait for one to appear later. This
/// is the type <see cref="NotificationHostMonitor"/> is tested against with a fake — no real
/// session bus or display is needed to prove the monitor's own logic.
/// </summary>
public interface ISessionBusNameWatcher : IAsyncDisposable
{
    /// <summary>
    /// Observes whether the well-known name currently has an owner. Returns the observed
    /// fact — never a guess — and never completes synchronously on the calling thread (D6
    /// point 5).
    /// </summary>
    Task<bool> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Completes once the well-known name gains an owner. Only called after
    /// <see cref="ProbeAsync"/> observed no owner (D6 point 4): the caller watches for the
    /// name to appear later instead of polling.
    /// </summary>
    Task WaitForOwnerAsync(CancellationToken cancellationToken);
}
