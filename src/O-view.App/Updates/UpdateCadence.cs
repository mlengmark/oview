using System.Runtime.InteropServices;
using OView.Core.Updates;

namespace OView.App.Updates;

/// <summary>
/// ADR-0010 slice 5 (D4, D7): decides *when* the shared <see cref="ReleaseFeed"/> check runs
/// and whether a result is worth telling the user about — never whether to download or install
/// anything, which stays out of scope for every path this type drives (D7's table: the
/// background path "Never downloads. Never installs", and the manual path only ever reports an
/// outcome). One instance per process, the same "one clock, one timer, injected" discipline
/// <see cref="UsagePollLoop"/> already applies to polling, so both skins share one cadence and
/// one dedupe decision rather than inventing their own.
///
/// <para><b>Notify-once-per-version is persisted, not held in memory</b> (D4). The last tag
/// this type has already raised <see cref="UsageEventKind.UpdateAvailable"/> for lives in
/// <see cref="ShellSettings.LastAnnouncedUpdateTag"/>, read fresh and written back through the
/// <paramref name="currentSettings"/>/<paramref name="recordAnnouncedTag"/> delegates below —
/// never an in-memory field a restart would forget. This supersedes
/// <see cref="UsageEventDecider.DecideUpdateAvailable"/>'s own in-memory dedupe for production
/// wiring: that member's own doc comment says it was proven ahead of this slice's fetch and
/// persistence, as a tested decision for "whichever later slice wires the fetch" to call into.
/// This type is that slice, and it owns the persisted version of the same rule directly rather
/// than calling a decider that cannot see <see cref="ShellSettings"/>.</para>
///
/// <para><b>The manual "Check for updates now" path never downloads either.</b>
/// <see cref="CheckNowAsync"/> runs exactly the same <see cref="ReleaseFeed.CheckAsync"/> the
/// background path does, runs regardless of <see cref="ShellSettings.AutoUpdateEnabled"/> and
/// regardless of whether a background check is already in flight, and always returns a real
/// <see cref="UpdateCheckResult"/> — up to date, a newer version, rate-limited, or
/// <see cref="UpdateOutcome.Unknown"/> ("could not tell"), never collapsing one outcome into
/// another. It also records a newly-seen tag (silently, with no event raised), so a user who
/// just checked by hand does not get the same version announced again by the next background
/// tick.</para>
///
/// <para>Adds no member to <see cref="ISkinToShell"/> or <see cref="IShellToSkin"/> (ADR-0010
/// slicing table row 5): a skin's menu calls <see cref="CheckNowAsync"/> directly on the
/// instance its composition root built, the same "shell declares, skin calls directly" shape
/// <c>IStartupRegistration</c> already uses for a menu action that is not a shell command.</para>
/// </summary>
public sealed class UpdateCadence : IDisposable
{
    private readonly ReleaseFeed _releaseFeed;
    private readonly IInstallKindSource _installKindSource;
    private readonly Architecture _architecture;
    private readonly string _currentVersion;
    private readonly Func<ShellSettings> _currentSettings;
    private readonly Action<string> _recordAnnouncedTag;
    private readonly Action<UsageEvent> _raiseEvent;
    private readonly IAppTimer _timer;

    /// <summary>Guards the background path only (<see cref="RunBackgroundCheckAsync"/>) against
    /// overlapping itself when a tick fires while a slow check from a previous tick is still
    /// in flight. <see cref="CheckNowAsync"/> is an explicit user request and is never skipped
    /// by this guard.</summary>
    private int _backgroundCheckInFlight;

    /// <param name="currentVersion">This running build's own version, compared against the
    /// feed's latest tag. Each skin's composition root sources this (e.g. its own assembly
    /// version) — not a decision this shared type makes.</param>
    /// <param name="currentSettings">Reads the shell's persisted <see cref="ShellSettings"/>
    /// back, fresh on every check, so a toggle of <see cref="ShellSettings.AutoUpdateEnabled"/>
    /// takes effect on the very next tick (same pattern <see cref="UsageEventDecider"/> uses
    /// for the threshold setting).</param>
    /// <param name="recordAnnouncedTag">Persists a newly-seen tag (<c>AppShell.RecordAnnouncedUpdateTag</c>
    /// in production). Called with the raw tag, never a formatted string.</param>
    /// <param name="raiseEvent">Raises the shell-to-skin event (<c>IShellToSkin.RaiseEvent</c>
    /// in production) — called only from the background path, never from
    /// <see cref="CheckNowAsync"/>, whose own return value is the manual path's report.</param>
    /// <param name="cadence">How often the background path checks. A constructor parameter,
    /// not a constant, the same reasoning <see cref="UsagePollLoop"/>'s own doc comment gives
    /// for its cadence: this type takes whatever its caller already decided on.</param>
    /// <param name="architecture">Defaults to the running process's architecture.</param>
    public UpdateCadence(
        ReleaseFeed releaseFeed,
        IInstallKindSource installKindSource,
        string currentVersion,
        Func<ShellSettings> currentSettings,
        Action<string> recordAnnouncedTag,
        Action<UsageEvent> raiseEvent,
        IAppTimer timer,
        TimeSpan cadence,
        Architecture? architecture = null)
    {
        ArgumentNullException.ThrowIfNull(releaseFeed);
        ArgumentNullException.ThrowIfNull(installKindSource);
        ArgumentException.ThrowIfNullOrEmpty(currentVersion);
        ArgumentNullException.ThrowIfNull(currentSettings);
        ArgumentNullException.ThrowIfNull(recordAnnouncedTag);
        ArgumentNullException.ThrowIfNull(raiseEvent);
        ArgumentNullException.ThrowIfNull(timer);

        _releaseFeed = releaseFeed;
        _installKindSource = installKindSource;
        _currentVersion = currentVersion;
        _currentSettings = currentSettings;
        _recordAnnouncedTag = recordAnnouncedTag;
        _raiseEvent = raiseEvent;
        _timer = timer;
        _architecture = architecture ?? RuntimeInformation.OSArchitecture;

        _timer.Elapsed += OnTimerElapsed;
        _timer.Interval = cadence;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, EventArgs e) => _ = RunBackgroundCheckAsync();

    /// <summary>
    /// The background path (D7): does nothing when <see cref="ShellSettings.AutoUpdateEnabled"/>
    /// is false, and otherwise checks, raising <see cref="UsageEventKind.UpdateAvailable"/> and
    /// recording the tag exactly once per distinct newer version. Internal rather than private
    /// so a test can drive one tick directly from a fake timer without waiting on a real one.
    /// </summary>
    internal async Task RunBackgroundCheckAsync(CancellationToken cancellation = default)
    {
        if (!_currentSettings().AutoUpdateEnabled)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _backgroundCheckInFlight, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var result = await RunCheckAsync(cancellation).ConfigureAwait(false);
            if (TryRecordNewVersion(result))
            {
                _raiseEvent(new UsageEvent(UsageEventKind.UpdateAvailable));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _backgroundCheckInFlight, 0);
        }
    }

    /// <summary>
    /// The manual "Check for updates now" path (D7): always runs, always returns a real
    /// outcome, and never raises <see cref="UsageEventKind.UpdateAvailable"/> itself — the
    /// caller's own menu reports the returned result directly, in its own skin's words, rather
    /// than this type raising a second, generic notification for the same click.
    /// </summary>
    public async Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellation = default)
    {
        var result = await RunCheckAsync(cancellation).ConfigureAwait(false);

        // Recorded silently: this marks the version as already told to the user (via this very
        // call's return value) so the next background tick does not announce it again, but it
        // raises no event of its own.
        TryRecordNewVersion(result);

        return result;
    }

    private async Task<UpdateCheckResult> RunCheckAsync(CancellationToken cancellation)
    {
        var asset = UpdatePolicy.DetectionAsset(_installKindSource.Current, _architecture);
        return await _releaseFeed.CheckAsync(_currentVersion, asset, cancellation).ConfigureAwait(false);
    }

    /// <summary>Records <paramref name="result"/>'s tag as announced when it names a newer
    /// version not already recorded. Returns whether it just did, so the background path knows
    /// whether to raise the event.</summary>
    private bool TryRecordNewVersion(UpdateCheckResult result)
    {
        if (result.Outcome != UpdateOutcome.UpdateAvailable || result.Available is not { } available)
        {
            return false;
        }

        if (string.Equals(available.Tag, _currentSettings().LastAnnouncedUpdateTag, StringComparison.Ordinal))
        {
            return false;
        }

        _recordAnnouncedTag(available.Tag);
        return true;
    }

    public void Dispose()
    {
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
    }
}
