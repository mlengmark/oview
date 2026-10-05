namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="MenuFixture"/>s (ADR-0003, ADR-0009 D1-D4, OVI-438).
/// One entry per plain action item, four run-at-startup scenarios covering D3's
/// requested-vs-OS-returned distinction, and three notification-threshold scenarios covering
/// D4's persisted-value distinction.
/// </summary>
public static class MenuFixtures
{
    // D1/D2 — plain action items. Nothing beyond existence to pin: each calls a single
    // ISkinToShell member and reflects no state (ADR-0009 D2's table).
    public static readonly MenuFixture RefreshNow = new(
        Name: "refresh-now",
        Item: MenuItemKind.RefreshNow,
        ContentFacts: Array.Empty<ContentFact>());

    public static readonly MenuFixture CopyDiagnostics = new(
        Name: "copy-diagnostics",
        Item: MenuItemKind.CopyDiagnostics,
        ContentFacts: Array.Empty<ContentFact>());

    public static readonly MenuFixture ShowUsageDetails = new(
        Name: "show-usage-details",
        Item: MenuItemKind.ShowUsageDetails,
        ContentFacts: Array.Empty<ContentFact>());

    public static readonly MenuFixture Quit = new(
        Name: "quit",
        Item: MenuItemKind.Quit,
        ContentFacts: Array.Empty<ContentFact>());

    // D1/D2 — the persisted boolean setting. Existence only here; its persisted value has no
    // literal digit to pin the way the threshold percent does.
    public static readonly MenuFixture CheckForUpdatesAutomatically = new(
        Name: "check-for-updates-automatically",
        Item: MenuItemKind.CheckForUpdatesAutomatically,
        ContentFacts: Array.Empty<ContentFact>());

    // D3 — run at startup. OS returns what was requested: the common case.
    public static readonly MenuFixture RunAtStartupEnabledSuccessfully = new(
        Name: "run-at-startup-enabled-successfully",
        Item: MenuItemKind.RunAtStartup,
        ContentFacts: Array.Empty<ContentFact>(),
        StartupRequestedEnabled: true,
        StartupOsReturnedEnabled: true,
        FailedToggle: false);

    public static readonly MenuFixture RunAtStartupDisabledSuccessfully = new(
        Name: "run-at-startup-disabled-successfully",
        Item: MenuItemKind.RunAtStartup,
        ContentFacts: Array.Empty<ContentFact>(),
        StartupRequestedEnabled: false,
        StartupOsReturnedEnabled: false,
        FailedToggle: false);

    // D3 — the user asked to enable it, but the OS write failed and it is still disabled.
    // The checkbox must show disabled (the OS-returned state), not enabled (the request), and
    // the skin must say the change did not take effect.
    public static readonly MenuFixture RunAtStartupEnableRequestedButFailed = new(
        Name: "run-at-startup-enable-requested-but-failed",
        Item: MenuItemKind.RunAtStartup,
        ContentFacts: Array.Empty<ContentFact>(),
        StartupRequestedEnabled: true,
        StartupOsReturnedEnabled: false,
        FailedToggle: true);

    // D3 — the user asked to disable it, but the OS write failed and it is still enabled.
    public static readonly MenuFixture RunAtStartupDisableRequestedButFailed = new(
        Name: "run-at-startup-disable-requested-but-failed",
        Item: MenuItemKind.RunAtStartup,
        ContentFacts: Array.Empty<ContentFact>(),
        StartupRequestedEnabled: false,
        StartupOsReturnedEnabled: true,
        FailedToggle: true);

    // D4 — the threshold picker shows the shell's persisted value. Three different persisted
    // percents, each pinning its own raw digit, so a menu that hardcoded one label could not
    // satisfy all three (ADR-0003: not identical labels).
    public static readonly MenuFixture NotificationThresholdPersistedAt70 = new(
        Name: "notification-threshold-persisted-at-70",
        Item: MenuItemKind.NotificationThreshold,
        ContentFacts: new[] { ContentFact.Contains("70") },
        PersistedThresholdPercent: 70);

    public static readonly MenuFixture NotificationThresholdPersistedAt80 = new(
        Name: "notification-threshold-persisted-at-80",
        Item: MenuItemKind.NotificationThreshold,
        ContentFacts: new[] { ContentFact.Contains("80") },
        PersistedThresholdPercent: 80);

    public static readonly MenuFixture NotificationThresholdPersistedAt90 = new(
        Name: "notification-threshold-persisted-at-90",
        Item: MenuItemKind.NotificationThreshold,
        ContentFacts: new[] { ContentFact.Contains("90") },
        PersistedThresholdPercent: 90);

    public static IReadOnlyList<MenuFixture> All { get; } = new[]
    {
        RefreshNow,
        CopyDiagnostics,
        ShowUsageDetails,
        Quit,
        CheckForUpdatesAutomatically,
        RunAtStartupEnabledSuccessfully,
        RunAtStartupDisabledSuccessfully,
        RunAtStartupEnableRequestedButFailed,
        RunAtStartupDisableRequestedButFailed,
        NotificationThresholdPersistedAt70,
        NotificationThresholdPersistedAt80,
        NotificationThresholdPersistedAt90,
    };
}
