using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// Builds the Linux skin's tooltip/status text from a <see cref="UsageSnapshot"/>. Every
/// presentation decision here — wording, separators, fallback copy, and local time
/// formatting — is this skin's own, per ADR-0001/ADR-0003's ownership rule: it does not
/// port <c>O-view.Tray</c>'s exact phrasing, and it defines no length cap, because the
/// 127-character <c>NotifyIcon.Text</c> limit is a Windows API fact (ADR-0001, ADR-0002)
/// that must not travel to this skin. StatusNotifierItem tooltip support is itself
/// desktop-environment-dependent and unverified on real hardware (ADR-0002); this class
/// only proves the string-construction surface OVI-25's cross-skin harness needs.
/// </summary>
public static class TooltipFormatter
{
    public static string Format(UsageSnapshot snapshot, TimeZoneInfo? displayZone = null)
    {
        var zone = displayZone ?? TimeZoneInfo.Local;

        if (snapshot.DataSourceKind == DataSourceKind.Unavailable)
        {
            return "O-view: usage data unavailable";
        }

        // Only the Estimate confidence tier gets the "estimated reading" fallback copy — a
        // snapshot merely lacking percentages (e.g. Live with nothing sampled yet) must not
        // be mislabelled as an estimate it isn't.
        if (snapshot.DataSourceKind == DataSourceKind.Estimate
            && snapshot.SessionUtilizationPercent.Value is null
            && snapshot.WeeklyUtilizationPercent.Value is null)
        {
            return "O-view: estimated reading, percentages not yet known";
        }

        var session = snapshot.SessionUtilizationPercent.Value is { } sessionPercent
            ? string.Create(CultureInfo.InvariantCulture, $"Session {FormatPercent(sessionPercent)}%{Marker(snapshot.SessionUtilizationPercent.Status)}")
            : "Session unknown";

        // A reset Core flags Unavailable is absent even if it carries a value — the same
        // reading PanelTextFormatter.SessionReset takes, so the two surfaces never disagree
        // and an unavailable instant is never shown (ADR-0001).
        var sessionReset = snapshot.SessionResetAt is { Status: not UsageValueStatus.Unavailable, Value: { } sessionResetAt }
            ? string.Create(CultureInfo.InvariantCulture, $", resets {ToLocal(sessionResetAt, zone):HH:mm}{Marker(snapshot.SessionResetAt.Status)}")
            : "";

        var weekly = snapshot.WeeklyUtilizationPercent.Value is { } weeklyPercent
            ? string.Create(CultureInfo.InvariantCulture, $" / Week {FormatPercent(weeklyPercent)}%{Marker(snapshot.WeeklyUtilizationPercent.Status)}")
            : "";

        var weeklyReset = snapshot.WeeklyResetAt is { Status: not UsageValueStatus.Unavailable, Value: { } weeklyResetAt }
            ? string.Create(CultureInfo.InvariantCulture, $", resets {ToLocal(weeklyResetAt, zone):ddd HH:mm}{Marker(snapshot.WeeklyResetAt.Status)}")
            : "";

        return session + sessionReset + weekly + weeklyReset;
    }

    /// <summary>
    /// The visible marker that distinguishes a modelled value from a measured one, per each
    /// field's own <see cref="UsageValueStatus"/> (ADR-0001, ADR-0002's "labelling is
    /// mandatory" rule). Deliberately worded differently from the Windows skin's "~" prefix
    /// — each skin owns its own wording (ADR-0003).
    /// </summary>
    private static string Marker(UsageValueStatus status) => status == UsageValueStatus.Estimated ? " (est.)" : "";

    private static int FormatPercent(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(utc, zone);
}
