namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 D6 point 3's wording: states what was observed on the session bus and, only when
/// the host is absent, what to do about it — never a guess about the user's machine. This
/// skin's own text (D1 — the shell owns no wording), not shared with O-view.Tray. No status
/// icon exists yet to show this against (slice 9); until then the only consumer is
/// <c>Program.cs</c>'s own startup trace.
/// </summary>
public static class NotificationHostAdvisoryFormatter
{
    public static string Describe(bool observed) => observed
        ? "Notification-area host found on the session bus (org.kde.StatusNotifierWatcher has an owner)."
        : "No notification-area host was found on the session bus: org.kde.StatusNotifierWatcher has no "
        + "owner. On GNOME, install an AppIndicator or KStatusNotifierItem extension to get one. Until "
        + "then, O-view's status icon will not appear in any panel; this process keeps watching the bus "
        + "and will pick the host up automatically if one appears later, without needing a restart.";
}
