namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The seven items ADR-0009 D2 gives the control-surface menu. Fixed membership — a
/// fixture-completeness test asserts <see cref="MenuFixtures.All"/> covers every value here,
/// so a new item added to the ADR without a matching fixture fails loudly.
/// </summary>
public enum MenuItemKind
{
    RefreshNow,
    NotificationThreshold,
    RunAtStartup,
    CheckForUpdatesAutomatically,
    CopyDiagnostics,
    ShowUsageDetails,
    Quit,
}
