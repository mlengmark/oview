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

    public AppShell(
        ShellSettingsStore settingsStore,
        ShellSettings settings,
        UsagePollLoop pollLoop,
        DetailPushCoordinator detailCoordinator,
        DiagnosticsBundleWriter diagnosticsWriter)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pollLoop);
        ArgumentNullException.ThrowIfNull(detailCoordinator);
        ArgumentNullException.ThrowIfNull(diagnosticsWriter);

        _settingsStore = settingsStore;
        Settings = settings;
        _pollLoop = pollLoop;
        _detailCoordinator = detailCoordinator;
        _diagnosticsWriter = diagnosticsWriter;
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

    /// <inheritdoc />
    public void WriteDiagnosticsBundle() => _diagnosticsWriter.Write(Settings, _pollLoop.CurrentSnapshot);

    /// <summary>
    /// Not implemented in this slice. Process-lifetime shutdown ordering (skin
    /// <c>Shutdown()</c>, then poll loop, then stores) is ADR-0009 slicing table row 4 — a
    /// separate, board-merge slice, because a wrong order here is a corrupted ledger reachable
    /// from one menu click.
    /// </summary>
    public void Quit() => throw new NotImplementedException(
        "Quit's shutdown ordering is ADR-0009 slicing table row 4, not this slice (OVI-447).");
}
