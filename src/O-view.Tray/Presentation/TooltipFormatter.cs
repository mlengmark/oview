using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the tray tooltip text from a <see cref="UsageSnapshot"/>. Every presentation
/// decision — the field separator, fallback copy, local time formatting, the
/// 127-character cap that is <c>NotifyIcon.Text</c>'s own OS limit, and the "~" marker
/// that distinguishes an estimated value from a real one — lives here, in the Windows
/// skin, and nowhere in O-view.Core (ADR-0001). If Windows and Linux both render a
/// tooltip, each owns its own wording; this class is not shared with O-view.Linux.
/// </summary>
public static class TooltipFormatter
{
    /// <summary><c>NotifyIcon.Text</c>'s measured limit — a Windows API fact, not a Core fact.</summary>
    public const int MaxLength = 127;

    public static string Format(UsageSnapshot snapshot, TimeZoneInfo? displayZone = null)
    {
        var zone = displayZone ?? TimeZoneInfo.Local;

        if (snapshot.DataSourceKind == DataSourceKind.Unavailable)
        {
            return Cap("O-view · no usage data");
        }

        // A percent Core flags Unavailable is absent even if it carries a value — the same
        // rule the reset clauses below follow, so a number the contract says does not
        // exist is never shown unmarked (ADR-0001, OVI-148).
        var sessionPercent = Present(snapshot.SessionUtilizationPercent);
        var weeklyPercent = Present(snapshot.WeeklyUtilizationPercent);

        // Only the Estimate confidence tier gets the "local estimate" fallback copy — a
        // snapshot merely lacking percentages (e.g. Live with nothing sampled yet) must
        // not be mislabelled as an estimate it isn't.
        if (snapshot.DataSourceKind == DataSourceKind.Estimate
            && sessionPercent is null
            && weeklyPercent is null)
        {
            return Cap("O-view · local estimate · usage % unknown");
        }

        var session = sessionPercent is { } sessionValue
            ? string.Create(CultureInfo.InvariantCulture, $"5h: {Marker(snapshot.SessionUtilizationPercent.Status)}{FormatPercent(sessionValue)}%")
            : "5h: ?";

        // A reset Core flags Unavailable is absent even if it carries a value — the same
        // reading PanelTextFormatter.SessionReset takes, so the two surfaces never disagree
        // and an unavailable instant is never shown (ADR-0001).
        var reset = snapshot.SessionResetAt is { Status: not UsageValueStatus.Unavailable, Value: { } sessionReset }
            ? string.Create(CultureInfo.InvariantCulture, $" · resets {Marker(snapshot.SessionResetAt.Status)}{ToLocal(sessionReset, zone):HH:mm}")
            : "";

        var weekly = weeklyPercent is { } weeklyValue
            ? string.Create(CultureInfo.InvariantCulture, $" · 7d: {Marker(snapshot.WeeklyUtilizationPercent.Status)}{FormatPercent(weeklyValue)}%")
            : "";

        var weeklyReset = snapshot.WeeklyResetAt is { Status: not UsageValueStatus.Unavailable, Value: { } weeklyResetAt }
            ? string.Create(CultureInfo.InvariantCulture, $" · resets {Marker(snapshot.WeeklyResetAt.Status)}{ToLocal(weeklyResetAt, zone):ddd HH:mm}")
            : "";

        return Cap(session + reset + weekly + weeklyReset);
    }

    /// <summary>Internal so the Tray test project can prove the cap is enforced without duplicating it.</summary>
    internal static string Cap(string text) => text.Length <= MaxLength ? text : text[..MaxLength];

    /// <summary>
    /// The visible marker that distinguishes a modelled value from a measured one, per
    /// each field's own <see cref="UsageValueStatus"/> (ADR-0001, ADR-0002's "labelling is
    /// mandatory" rule). A value is only ever rendered without a marker when Core itself
    /// attests it as <see cref="UsageValueStatus.Real"/>.
    /// </summary>
    private static string Marker(UsageValueStatus status) => status == UsageValueStatus.Estimated ? "~" : "";

    /// <summary>
    /// The percent's value, or null when Core flags it <see cref="UsageValueStatus.Unavailable"/>
    /// — whatever value it carries. <see cref="Marker"/> has no mark for Unavailable, so a
    /// value paired with it would otherwise render as if it were real (OVI-148). OVI-149
    /// made Core refuse that pair at construction, so this guard is now redundant; it stays,
    /// like the reset guards, as ADR-0001's OVI-146 amendment allows.
    /// </summary>
    private static double? Present(UsagePercent percent) =>
        percent.Status == UsageValueStatus.Unavailable ? null : percent.Value;

    private static int FormatPercent(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(utc, zone);
}
