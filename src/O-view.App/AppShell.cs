namespace OView.App;

/// <summary>
/// The real <see cref="ISkinToShell"/> (ADR-0009 slicing table row 2, OVI-447), replacing both
/// composition roots' local <c>PendingSkinToShell</c> stub. Wraps pieces each already built and
/// tested on its own — a <see cref="UsagePollLoop"/>, a <see cref="DetailPushCoordinator"/>, a
/// <see cref="ShellSettingsStore"/>, a <see cref="DiagnosticsBundleWriter"/> — behind the one
/// seam a skin calls through.
///
/// <para><b>Settings load order (ADR-0009 D4 point 1).</b> The caller loads
/// <see cref="ShellSettings"/> from <paramref name="settings"/>'s store <i>before</i>
/// constructing the <see cref="UsagePollLoop"/> passed in here, so the loop starts on the
/// loaded cadence rather than <see cref="ShellSettings.Default"/>'s. This type does not load
/// settings itself — by the time it exists, the loop already has; it only holds the result so
/// later menu-driven changes (<see cref="SetThresholdPercent"/>, <see cref="SetAutoUpdate"/>)
/// have something to read back and persist against (D4 point 2/3).</para>
/// </summary>
public sealed class AppShell : ISkinToShell
{
    private readonly ShellSettingsStore _settingsStore;
    private readonly UsagePollLoop _pollLoop;
    private readonly DetailPushCoordinator _detailCoordinator;
    private readonly DiagnosticsBundleWriter _diagnosticsWriter;
    private readonly IShellToSkin _skin;
    private readonly IDisposable _storeLifetime;

    public AppShell(
        ShellSettingsStore settingsStore,
        ShellSettings settings,
        UsagePollLoop pollLoop,
        DetailPushCoordinator detailCoordinator,
        DiagnosticsBundleWriter diagnosticsWriter,
        IShellToSkin skin,
        IDisposable storeLifetime)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pollLoop);
        ArgumentNullException.ThrowIfNull(detailCoordinator);
        ArgumentNullException.ThrowIfNull(diagnosticsWriter);
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(storeLifetime);

        _settingsStore = settingsStore;
        Settings = settings;
        _pollLoop = pollLoop;
        _detailCoordinator = detailCoordinator;
        _diagnosticsWriter = diagnosticsWriter;
        _skin = skin;
        _storeLifetime = storeLifetime;
    }

    /// <summary>
    /// The shell's current behaviour settings (ADR-0007 D4): the value loaded at composition,
    /// updated in place by <see cref="SetThresholdPercent"/>/<see cref="SetAutoUpdate"/> and
    /// persisted on every such change. A menu reading its own rendered state back (D4 point 3)
    /// reads this, not its own memory of the last click.
    /// </summary>
    public ShellSettings Settings { get; private set; }

    /// <inheritdoc />
    public void RefreshNow() => _pollLoop.PollNow();

    /// <inheritdoc />
    public void RequestWidget(bool visible) => _detailCoordinator.OnRequestWidget(visible);

    /// <summary>
    /// Clamps <paramref name="percent"/> to <see cref="ShellSettings"/>'s documented 0–100
    /// range (ADR-0009 D4's validation rule — the shell validates, not cosmetic), then persists
    /// the result. A skin that only ever offers in-range choices still goes through the same
    /// clamp; the seam is public and the next skin is not this type's to predict.
    /// </summary>
    public void SetThresholdPercent(int percent)
    {
        Settings = Settings with { AlertThresholdPercent = Math.Clamp(percent, 0, 100) };
        _settingsStore.Save(Settings);
    }

    /// <inheritdoc />
    public void SetAutoUpdate(bool enabled)
    {
        Settings = Settings with { AutoUpdateEnabled = enabled };
        _settingsStore.Save(Settings);
    }

    /// <summary>
    /// Persists the tag of a release the update check just told the user about (ADR-0010 D4,
    /// slicing table row 5), so a later check — background or manual — does not re-announce
    /// the same version. Not an <see cref="ISkinToShell"/> member: the caller is
    /// <c>OView.App.Updates.UpdateCadence</c>, composed alongside this shell, never a skin —
    /// the seam this record names stays unchanged by this slice.
    /// </summary>
    public void RecordAnnouncedUpdateTag(string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        Settings = Settings with { LastAnnouncedUpdateTag = tag };
        _settingsStore.Save(Settings);
    }

    /// <inheritdoc />
    public void WriteDiagnosticsBundle() => _diagnosticsWriter.Write(Settings, _pollLoop.CurrentSnapshot);

    /// <summary>
    /// The shell's process-lifetime shutdown order (ADR-0009 D7, slicing table row 4,
    /// OVI-469): the skin tears down its OS integration first (<see cref="IShellToSkin.Shutdown"/>),
    /// then the poll loop stops and — <see cref="UsagePollLoop.Dispose"/>'s own guarantee —
    /// blocks until any poll already in flight finishes, then the stores release ownership.
    /// Only after this method returns does the skin let its platform loop exit; that mechanism
    /// is the skin's own (WPF's <c>Application.Shutdown</c>, Avalonia's classic-desktop
    /// equivalent) and is not called from here.
    /// </summary>
    public void Quit()
    {
        _skin.Shutdown();
        _pollLoop.Dispose();
        _storeLifetime.Dispose();
    }
}
