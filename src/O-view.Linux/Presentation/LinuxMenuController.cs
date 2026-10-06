using OView.App;

namespace OView.Linux.Presentation;

/// <summary>
/// The decision logic behind ADR-0009 slice 9's tray menu (OVI-484): which
/// <see cref="ISkinToShell"/> member each item calls, and what the menu should show the next
/// time it opens. Independently written against ADR-0009 D1-D4/D7 rather than ported from
/// <c>O-view.Tray.Presentation.TrayMenuController</c> (ADR-0008 D1 — this skin owns its own
/// structure and wording); it answers the same contract because the contract, not the code, is
/// shared.
///
/// <para>Nothing here is new API surface — every call already shipped in <see cref="ISkinToShell"/>
/// (PR #52) or this skin's own <see cref="IStartupRegistration"/> implementation
/// (<see cref="OView.Linux.Platform.XdgAutostartRegistration"/>). This type exists only so
/// <c>LinuxTrayMenu</c>, the untested <c>NativeMenu</c> adapter, has something testable to
/// delegate every click to.</para>
/// </summary>
internal sealed class LinuxMenuController
{
    /// <summary>The three fixed picks the threshold item offers, matching
    /// <c>MenuFixtures.NotificationThresholdPersistedAt70/80/90</c> — defined again here, not
    /// referenced from <c>O-view.Tray</c>, per the no-shared-code rule.</summary>
    public static readonly int[] ThresholdChoicesPercent = { 70, 80, 90 };

    private readonly ISkinToShell _shell;
    private readonly Func<ShellSettings> _loadCurrentSettings;
    private readonly IStartupRegistration _startup;

    /// <param name="shell">Where every item's action goes, and the source of
    /// <see cref="ShellSettings"/> indirectly via <paramref name="loadCurrentSettings"/>.</param>
    /// <param name="loadCurrentSettings">Reads the shell's persisted settings back for render
    /// (D4 point 3). A delegate, not a cast of <paramref name="shell"/>, so this type is
    /// testable without <c>AppShell</c>.</param>
    /// <param name="startup">This skin's own run-at-startup mechanism (D3). Called directly,
    /// never through <paramref name="shell"/>, per D3's rejected alternative.</param>
    public LinuxMenuController(ISkinToShell shell, Func<ShellSettings> loadCurrentSettings, IStartupRegistration startup)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(loadCurrentSettings);
        ArgumentNullException.ThrowIfNull(startup);

        _shell = shell;
        _loadCurrentSettings = loadCurrentSettings;
        _startup = startup;
    }

    /// <summary>
    /// Everything the menu renders, re-read on every open rather than cached (D3 point 1: the
    /// run-at-startup checkbox must track whatever the OS currently reports, not the skin's
    /// memory of the last click; D4 point 3: the threshold and auto-update checks come from the
    /// shell's current settings, not the menu's own state).
    /// </summary>
    public LinuxMenuSnapshot Snapshot()
    {
        var settings = _loadCurrentSettings();
        return new LinuxMenuSnapshot(settings.AlertThresholdPercent, settings.AutoUpdateEnabled, _startup.IsEnabled());
    }

    public void RefreshNow() => _shell.RefreshNow();

    public void ShowUsageDetails() => _shell.RequestWidget(true);

    public void SetThresholdPercent(int percent) => _shell.SetThresholdPercent(percent);

    public void SetAutoUpdate(bool enabled) => _shell.SetAutoUpdate(enabled);

    public void CopyDiagnostics() => _shell.WriteDiagnosticsBundle();

    public void Quit() => _shell.Quit();

    /// <summary>
    /// Asks the OS to apply the requested run-at-startup state and reports what actually
    /// happened (D3 points 2-3): <see cref="StartupToggleOutcome.Enabled"/> is always
    /// <see cref="IStartupRegistration.Apply"/>'s return value, never <paramref name="requestedEnabled"/>,
    /// because reporting the request regardless would be a fabricated fact about the user's
    /// machine. <see cref="StartupToggleOutcome.Failed"/> is true exactly when the two disagree
    /// — the adapter's cue to snap the checkbox back and say, in its own words, that nothing
    /// changed.
    /// </summary>
    public StartupToggleOutcome ToggleRunAtStartup(bool requestedEnabled)
    {
        var actual = _startup.Apply(requestedEnabled);
        return new StartupToggleOutcome(actual, Failed: actual != requestedEnabled);
    }
}

/// <summary>The menu's rendered state as of its last <see cref="LinuxMenuController.Snapshot"/> call.</summary>
internal sealed record LinuxMenuSnapshot(int ThresholdPercent, bool AutoUpdateEnabled, bool StartupEnabled);

/// <summary>The result of one run-at-startup toggle (ADR-0009 D3).</summary>
internal sealed record StartupToggleOutcome(bool Enabled, bool Failed);
