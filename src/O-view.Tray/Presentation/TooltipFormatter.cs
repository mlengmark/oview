using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the tray tooltip text from a <see cref="UsageSnapshot"/>. Every presentation
/// decision — the field separator, fallback copy, local time formatting, and the
/// 127-character cap that is <c>NotifyIcon.Text</c>'s own OS limit — lives here, in the
/// Windows skin, and nowhere in O-view.Core (ADR-0001). If Windows and Linux both render a
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

        if (snapshot.SessionUtilizationPercent.Value is null && snapshot.WeeklyUtilizationPercent.Value is null)
        {
            return Cap("O-view · local estimate · usage % unknown");
        }

        var session = snapshot.SessionUtilizationPercent.Value is { } sessionPercent
            ? string.Create(CultureInfo.InvariantCulture, $"5h: {FormatPercent(sessionPercent)}%")
            : "5h: ?";

        var reset = snapshot.SessionResetAt.Value is { } sessionReset
            ? string.Create(CultureInfo.InvariantCulture, $" · resets {ToLocal(sessionReset, zone):HH:mm}")
            : "";

        var weekly = snapshot.WeeklyUtilizationPercent.Value is { } weeklyPercent
            ? string.Create(CultureInfo.InvariantCulture, $" · 7d: {FormatPercent(weeklyPercent)}%")
            : "";

        var weeklyReset = snapshot.WeeklyResetAt.Value is { } weeklyResetAt
            ? string.Create(CultureInfo.InvariantCulture, $" · resets {ToLocal(weeklyResetAt, zone):ddd HH:mm}")
            : "";

        return Cap(session + reset + weekly + weeklyReset);
    }

    /// <summary>Internal so the Tray test project can prove the cap is enforced without duplicating it.</summary>
    internal static string Cap(string text) => text.Length <= MaxLength ? text : text[..MaxLength];

    private static int FormatPercent(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(utc, zone);
}
