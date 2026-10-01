namespace OView.App;

/// <summary>
/// The shell's own behaviour settings (ADR-0007 D4): exactly the three values D4's table
/// names as the shell's responsibility — alert threshold percent, poll cadence, and
/// auto-update opt-in — and nothing else. Deliberately excludes the usage ledger and
/// weekly-reset anchor (Core's responsibility, ADR-0006 D1), run-at-startup (the OS is the
/// sole owner, never mirrored here, D4's explicit rejection), and any per-skin perceptual
/// preference such as widget position (each skin's own file, never shared, D4's other
/// explicit rejection).
/// </summary>
/// <param name="AlertThresholdPercent">The utilization percent that triggers a threshold-
/// crossed notification (<c>ISkinToShell.SetThresholdPercent</c>). 0–100.</param>
/// <param name="PollCadence">How often the shell polls for a new usage snapshot
/// (<see cref="UsagePollLoop"/>'s constructor parameter).</param>
/// <param name="AutoUpdateEnabled">Whether the shell checks for updates automatically
/// (<c>ISkinToShell.SetAutoUpdate</c>). The update-check fetch itself is slice 7 — this flag
/// is just the persisted opt-in, consulted by whichever slice adds the fetch.</param>
public sealed record ShellSettings(int AlertThresholdPercent, TimeSpan PollCadence, bool AutoUpdateEnabled)
{
    /// <summary>
    /// The values used when no settings file exists yet. <see cref="PollCadence"/>'s default
    /// of 60 seconds is CONFIRMED against the source app's shipping cadence (ADR-0007 D6:
    /// "already proven in a shipping app ... 60-second cadence observed", OVI-4's live run).
    /// <see cref="AlertThresholdPercent"/> and <see cref="AutoUpdateEnabled"/> have no such
    /// confirmed source-app value to carry forward, so these two are INFERRED, reasonable
    /// starting points, inert until a later slice ships the settings UI that lets a user
    /// change them — "opt-in" auto-update defaults to off.
    /// </summary>
    public static ShellSettings Default { get; } = new(
        AlertThresholdPercent: 80,
        PollCadence: TimeSpan.FromSeconds(60),
        AutoUpdateEnabled: false);
}
