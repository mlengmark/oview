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
/// </summary>
public sealed class UsagePollLoop : IDisposable
{
    private readonly IUsageProvider _provider;
    private readonly IClock _clock;
    private readonly IAppTimer _timer;

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

    private void OnTimerElapsed(object? sender, EventArgs e)
    {
        try
        {
            CurrentSnapshot = _provider.GetSnapshot(_clock.UtcNow);
        }
        catch
        {
            // D2 point 9: a failed poll keeps the previous state and never crashes the
            // process. Intentionally swallowed — there is no diagnostics bundle to log to
            // in this slice (ADR-0007 slice 8), and CompositeUsageProvider already reports
            // per-provider failures through its own Health seam (ADR-0005 D4).
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
    }
}
