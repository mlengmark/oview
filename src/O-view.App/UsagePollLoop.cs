using OView.Core.Models;
using OView.Core.Providers;

namespace OView.App;

/// <summary>
/// The poll loop's composition root (ADR-0007 D2 points 4 and 9): composes an
/// <see cref="IUsageProvider"/> (ADR-0005) with an injected <see cref="IClock"/> and
/// <see cref="IAppTimer"/>, polls on the given cadence, and holds the latest successfully
/// polled <see cref="UsageSnapshot"/>. This type owns that one piece of state and nothing
/// about how it is rendered — no store wiring (ADR-0007 slice 4), no settings-driven cadence
/// (slice 5), and no event/threshold decisions (D2 point 6) ship here.
///
/// <para><b>Graceful degradation (D2 point 9).</b> A poll that throws is caught and leaves
/// <see cref="CurrentSnapshot"/> untouched; the loop keeps running on the next tick.
/// <see cref="IUsageProvider"/>'s own contract says it never throws (ADR-0005 D1), so this
/// is defense in depth, not reliance on a provider violating that contract — the shell's
/// obligation not to crash the process does not get to assume every provider upholds it. A
/// poll that returns normally — including <see cref="UsageSnapshot.Unavailable"/>, which is
/// a legitimate "no data" answer, not a failure — always replaces <see cref="CurrentSnapshot"/>.</para>
///
/// <para>Cadence is a constructor parameter, not a constant: this type takes whatever cadence
/// its caller already decided on rather than inventing a default of its own. The default a
/// caller falls back to when no settings have been saved yet lives in
/// <see cref="ShellSettings.Default"/> (slice 5), not here — no composition root wires the
/// two together yet.</para>
///
/// <para><see cref="SnapshotUpdated"/> was added for ADR-0008 slice 3 (OVI-360), alongside
/// the first consumer that needs to be notified of a new snapshot rather than polling
/// <see cref="CurrentSnapshot"/> itself: a skin host composing this loop with
/// <c>IShellToSkin.ShowSnapshot</c>. This is the same kind of deliberate, documented
/// extension of an existing shape that slice 2 made to <see cref="IAppTimer"/> — a type with
/// no way to notify a consumer of a new value cannot drive a push-based seam.</para>
/// </summary>
public sealed class UsagePollLoop : IDisposable
{
    private readonly IUsageProvider _provider;
    private readonly IClock _clock;
    private readonly IAppTimer _timer;

    /// <summary>
    /// Guards <see cref="Poll"/> so <see cref="Dispose"/> (ADR-0009 D7, OVI-469) can block
    /// until a poll already in flight — started by a timer tick or <see cref="PollNow"/> on
    /// another thread — finishes, instead of racing it. <see cref="AppShell.Quit"/> relies on
    /// this: it disposes the stores only after this type's <see cref="Dispose"/> returns, and
    /// a quit path that disposed a store mid-poll would be a corrupted-ledger bug.
    /// </summary>
    private readonly object _pollGate = new();
    private bool _disposed;

    public UsagePollLoop(IUsageProvider provider, IClock clock, IAppTimer timer, TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timer);

        _provider = provider;
        _clock = clock;
        _timer = timer;

        _timer.Elapsed += OnTimerElapsed;
        _timer.Interval = cadence;
        _timer.Start();
    }

    /// <summary>
    /// The most recent snapshot a poll returned without throwing, or
    /// <see cref="UsageSnapshot.Unavailable"/> before the first tick.
    /// </summary>
    public UsageSnapshot CurrentSnapshot { get; private set; } = UsageSnapshot.Unavailable;

    /// <summary>Raised after <see cref="CurrentSnapshot"/> is replaced by a poll that
    /// returned normally — the exact same snapshot, never a copy or a transformation. Never
    /// raised for a poll that throws (D2 point 9: the previous snapshot stands, so there is
    /// nothing new to announce).</summary>
    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    /// <summary>
    /// Polls immediately instead of waiting for the next timer tick (ADR-0009
    /// <c>ISkinToShell.RefreshNow</c>'s shell-side implementation, OVI-447). Same graceful-
    /// degradation rule as a timer-driven tick (D2 point 9): a throwing poll leaves
    /// <see cref="CurrentSnapshot"/> untouched and never raises <see cref="SnapshotUpdated"/>.
    /// Does not reset the timer's own schedule — the next tick still fires on cadence.
    /// </summary>
    public void PollNow() => Poll();

    private void OnTimerElapsed(object? sender, EventArgs e) => Poll();

    private void Poll()
    {
        lock (_pollGate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                CurrentSnapshot = _provider.GetSnapshot(_clock.UtcNow);
                SnapshotUpdated?.Invoke(this, CurrentSnapshot);
            }
            catch
            {
                // D2 point 9: a failed poll keeps the previous state and never crashes the
                // process. Intentionally swallowed — there is no diagnostics bundle to log to
                // in this slice (ADR-0007 slice 8), and CompositeUsageProvider already reports
                // per-provider failures through its own Health seam (ADR-0005 D4).
            }
        }
    }

    /// <summary>
    /// Stops the timer so no new poll starts, then blocks until any poll already in flight on
    /// another thread — <see cref="_pollGate"/>'s lock holder — finishes, before disposing the
    /// timer (ADR-0009 D7, OVI-469). Idempotent: a second call returns immediately.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;

        lock (_pollGate)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }
}
