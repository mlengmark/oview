using OView.App;
using OView.App.Updates;
using OView.Core.Updates;

namespace OView.Tray.Presentation;

/// <summary>
/// The pure decision logic behind ADR-0009 slice 6's right-click menu (OVI-480): what each of
/// the seven items (D1/D2) calls and what state they render on open. No member here is new —
/// every action goes through <see cref="ISkinToShell"/>'s existing members or the skin's own
/// <see cref="IStartupRegistration"/> (D2's table); this type only holds the per-item wiring so
/// <see cref="TrayMenu"/>, the untested WinForms adapter, can stay a thin shell around a real
/// <c>ContextMenuStrip</c>.
/// </summary>
internal sealed class TrayMenuController
{
    /// <summary>The three fixed choices the picker offers (D2's table), matching
    /// <c>MenuFixtures.NotificationThresholdPersistedAt70/80/90</c>.</summary>
    public static readonly int[] ThresholdChoicesPercent = { 70, 80, 90 };

    private readonly ISkinToShell _skinToShell;
    private readonly Func<ShellSettings> _currentSettings;
    private readonly IStartupRegistration _startupRegistration;
    private readonly UpdateCadence _updateCadence;

    /// <param name="skinToShell">Where every action item's call goes (D2's table) and where
    /// the persisted threshold/auto-update settings ultimately come from.</param>
    /// <param name="currentSettings">Reads the shell's persisted <see cref="ShellSettings"/>
    /// back (D4 point 3) — a delegate rather than a cast of <paramref name="skinToShell"/>, so
    /// this type stays testable against a fake without depending on <c>AppShell</c>.</param>
    /// <param name="startupRegistration">The skin's own run-at-startup mechanism (D3), called
    /// directly rather than through <paramref name="skinToShell"/> per D3's rejected
    /// alternative.</param>
    /// <param name="updateCadence">ADR-0010 slicing table row 5's "Check for updates now" item
    /// calls this directly, the same "shell declares, skin calls directly" shape
    /// <paramref name="startupRegistration"/> already uses — not an <see cref="ISkinToShell"/>
    /// member, per that slice's "adds no member to either seam".</param>
    public TrayMenuController(
        ISkinToShell skinToShell,
        Func<ShellSettings> currentSettings,
        IStartupRegistration startupRegistration,
        UpdateCadence updateCadence)
    {
        ArgumentNullException.ThrowIfNull(skinToShell);
        ArgumentNullException.ThrowIfNull(currentSettings);
        ArgumentNullException.ThrowIfNull(startupRegistration);
        ArgumentNullException.ThrowIfNull(updateCadence);

        _skinToShell = skinToShell;
        _currentSettings = currentSettings;
        _startupRegistration = startupRegistration;
        _updateCadence = updateCadence;
    }

    /// <summary>
    /// Everything the menu needs to render itself, re-read every time it opens (D3 point 1:
    /// run-at-startup is read live from the OS, never cached; D4 point 3: the threshold and
    /// auto-update checks come from the shell's persisted settings, never this controller's own
    /// memory of the last click).
    /// </summary>
    public TrayMenuSnapshot BuildSnapshot()
    {
        var settings = _currentSettings();
        return new TrayMenuSnapshot(settings.AlertThresholdPercent, settings.AutoUpdateEnabled, _startupRegistration.IsEnabled());
    }

    public void OnRefreshNow() => _skinToShell.RefreshNow();

    public void OnShowUsageDetails() => _skinToShell.RequestWidget(true);

    public void OnSetThresholdPercent(int percent) => _skinToShell.SetThresholdPercent(percent);

    public void OnSetAutoUpdate(bool enabled) => _skinToShell.SetAutoUpdate(enabled);

    /// <summary>
    /// Runs the same check the background cadence runs, interactively, and always returns a
    /// real outcome (ADR-0010 D7) for <c>TrayMenu</c> to word via
    /// <c>AlertToastFormatter.FormatManualCheck</c> — never silence, because the user clicked
    /// this item on purpose.
    /// </summary>
    public Task<UpdateCheckResult> OnCheckForUpdatesNow() => _updateCadence.CheckNowAsync();

    public void OnCopyDiagnostics() => _skinToShell.WriteDiagnosticsBundle();

    public void OnQuit() => _skinToShell.Quit();

    /// <summary>
    /// Applies the requested run-at-startup state and reports what actually happened (D3
    /// points 2-3): <see cref="StartupToggleResult.Enabled"/> is <see cref="IStartupRegistration.Apply"/>'s
    /// return value, never <paramref name="requested"/>, and <see cref="StartupToggleResult.Failed"/>
    /// is true exactly when the two differ — the adapter's cue to snap the checkbox back and
    /// state, in its own words, that the change did not take effect.
    /// </summary>
    public StartupToggleResult OnToggleRunAtStartup(bool requested)
    {
        var actual = _startupRegistration.Apply(requested);
        return new StartupToggleResult(actual, Failed: actual != requested);
    }
}

/// <summary>The menu's rendered state as of its last <see cref="TrayMenuController.BuildSnapshot"/>
/// call.</summary>
internal sealed record TrayMenuSnapshot(int ThresholdPercent, bool AutoUpdateEnabled, bool StartupEnabled);

/// <summary>The outcome of one run-at-startup toggle (ADR-0009 D3).</summary>
internal sealed record StartupToggleResult(bool Enabled, bool Failed);
