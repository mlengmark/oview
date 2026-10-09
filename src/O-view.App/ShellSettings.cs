namespace OView.App;

/// <summary>
/// The shell's own behaviour settings (ADR-0007 D4): the three values D4's table names as the
/// shell's responsibility — alert threshold percent, poll cadence, and auto-update opt-in —
/// plus <see cref="LastAnnouncedUpdateTag"/> (ADR-0010 D4, slicing table row 5), the persisted
/// record of which release the update check has already told the user about. Deliberately
/// excludes the usage ledger and weekly-reset anchor (Core's responsibility, ADR-0006 D1),
/// run-at-startup (the OS is the sole owner, never mirrored here, D4's explicit rejection), and
/// any per-skin perceptual preference such as widget position (each skin's own file, never
/// shared, D4's other explicit rejection).
/// </summary>
/// <param name="AlertThresholdPercent">The utilization percent that triggers a threshold-
/// crossed notification (<c>ISkinToShell.SetThresholdPercent</c>). 0–100.</param>
/// <param name="PollCadence">How often the shell polls for a new usage snapshot
/// (<see cref="UsagePollLoop"/>'s constructor parameter).</param>
/// <param name="AutoUpdateEnabled">Whether the shell checks for updates automatically
/// (<c>ISkinToShell.SetAutoUpdate</c>). Gates only the periodic background check
/// (<c>OView.App.Updates.UpdateCadence</c>) — never downloading or installing anything
/// (ADR-0010 D7).</param>
/// <param name="LastAnnouncedUpdateTag">The release tag the shell last raised
/// <c>UsageEventKind.UpdateAvailable</c> for, or null before any update has ever been
/// announced. ADR-0010 D4: "once per version" is persisted here, not held in an in-memory
/// field, so a restart does not re-announce a version the user has already been told about.
/// Updated by both the background cadence and the manual "Check for updates now" check — the
/// manual path records a newly-seen tag too, so a user who just checked by hand does not get
/// the same version announced again by the next background tick.</param>
public sealed record ShellSettings(
    int AlertThresholdPercent, TimeSpan PollCadence, bool AutoUpdateEnabled, string? LastAnnouncedUpdateTag = null)
{
    /// <summary>
    /// The values used when no settings file exists yet. <see cref="PollCadence"/>'s default
    /// of 60 seconds is CONFIRMED against the source app's shipping cadence (ADR-0007 D6:
    /// "already proven in a shipping app ... 60-second cadence observed", OVI-4's live run).
    /// <see cref="AlertThresholdPercent"/> and <see cref="AutoUpdateEnabled"/> have no such
    /// confirmed source-app value to carry forward, so these two are INFERRED, reasonable
    /// starting points, inert until a later slice ships the settings UI that lets a user
    /// change them — "opt-in" auto-update defaults to off. <see cref="LastAnnouncedUpdateTag"/>
    /// has no default to carry forward either: null means "nothing has ever been announced",
    /// which is simply true the first time a build ever runs.
    /// </summary>
    public static ShellSettings Default { get; } = new(
        AlertThresholdPercent: 80,
        PollCadence: TimeSpan.FromSeconds(60),
        AutoUpdateEnabled: false,
        LastAnnouncedUpdateTag: null);
}
