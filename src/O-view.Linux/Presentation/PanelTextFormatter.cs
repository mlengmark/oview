using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// Builds the Linux skin's detail-panel freshness/countdown/reset text from a
/// <see cref="UsageSnapshot"/> and the raw scalars the source app's <c>PanelText.cs</c> also
/// took directly. Every wording decision here is this skin's own, per ADR-0001/ADR-0003's
/// ownership rule — it does not port <c>O-view.Tray</c>'s exact phrasing.
///
/// <para>Covers the <c>Freshness</c>/<c>Countdown</c>/<c>SessionReset</c>/
/// <c>WeeklyReset</c>/<c>WeeklyResetConflict</c> family (Phase 1 slice 3.1, OVI-29) plus
/// <c>RateLimitedNotice</c> (Phase 1 slice 3.2, OVI-80). The remaining <c>PanelText.cs</c>
/// members are separate, differently-shaped sub-slices, not yet extracted.</para>
/// </summary>
public static class PanelTextFormatter
{
    /// <summary>
    /// The freshness line: <c>Reading: now</c>, <c>Reading: 11:34</c>,
    /// <c>Local estimate, reading: 11:34</c>, <c>No data</c>.
    ///
    /// <para><see cref="DataSourceKind.Live"/>, <see cref="DataSourceKind.Stale"/>, and
    /// <see cref="DataSourceKind.JsonlFallback"/> collapse into the same age-labelled
    /// wording on purpose — see <c>O-view.Tray</c>'s <c>PanelTextFormatter.Freshness</c> doc
    /// comment for the full reasoning, which applies identically here even though the two
    /// skins word it differently.</para>
    /// </summary>
    public static string Freshness(UsageSnapshot snapshot, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (snapshot.DataSourceKind == DataSourceKind.Unavailable)
        {
            return "No usage data";
        }

        var age = AsOf(snapshot.LastIngestAt, utcNow, displayZone);

        return snapshot.DataSourceKind == DataSourceKind.Estimate
            ? string.Create(CultureInfo.InvariantCulture, $"Local estimate, reading: {age}")
            : string.Create(CultureInfo.InvariantCulture, $"Reading: {age}");
    }

    private static string AsOf(DateTimeOffset lastIngestAt, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var captured = TimeZoneInfo.ConvertTime(lastIngestAt, displayZone);
        var now = TimeZoneInfo.ConvertTime(utcNow, displayZone);

        var elapsed = utcNow - lastIngestAt;
        var withinThisMinute = elapsed < TimeSpan.FromMinutes(1)
            && (elapsed < TimeSpan.Zero || Minute(captured) == Minute(now));

        return withinThisMinute
            ? "now"
            : string.Create(CultureInfo.InvariantCulture, $"{captured:HH:mm}");
    }

    private static DateTime Minute(DateTimeOffset t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

    /// <summary>
    /// A duration as this skin says it: <c>3d, 4h</c>, <c>2h, 14m</c>, <c>14m</c>, or
    /// <c>less than a minute</c>. Deliberately worded differently from the Windows skin's
    /// space-separated units — each skin owns its own phrasing (ADR-0003).
    /// </summary>
    public static string Countdown(TimeSpan t) => t.TotalMinutes < 1
        ? "less than a minute"
        : t.TotalDays >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{t.Days}d, {t.Hours}h")
            : t.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}h, {t.Minutes}m")
                : string.Create(CultureInfo.InvariantCulture, $"{t.Minutes}m");

    /// <summary>
    /// The session-reset line. Carries a <c>(approx.)</c> suffix, this skin's own marker for
    /// a bracketed rather than exact reset instant — parallel to
    /// <see cref="TooltipFormatter"/>'s <c>(est.)</c> suffix for an estimated field.
    /// </summary>
    public static string SessionReset(
        DateTimeOffset? resetAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        TimeSpan? uncertainty = null)
    {
        if (resetAtUtc is not { } reset)
        {
            return "No reset observed yet";
        }

        var at = TimeZoneInfo.ConvertTime(reset, displayZone);
        var suffix = IsApproximate(uncertainty) ? " (approx.)" : "";

        return string.Create(CultureInfo.InvariantCulture,
            $"Resets in {Countdown(reset - utcNow)}, at {at:HH:mm}{suffix}");
    }

    /// <summary>
    /// The weekly-reset line. Never approximate — see
    /// <c>O-view.Tray</c>'s <c>PanelTextFormatter.WeeklyReset</c> doc comment for why.
    /// </summary>
    public static string WeeklyReset(DateTimeOffset resetAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var at = TimeZoneInfo.ConvertTime(resetAtUtc, displayZone);

        return string.Create(CultureInfo.InvariantCulture,
            $"Resets in {Countdown(resetAtUtc - utcNow)}, at {at:ddd HH:mm}");
    }

    /// <summary>
    /// Told to the user when an observed weekly reset disagrees with one they entered
    /// themselves.
    /// </summary>
    public static string WeeklyResetConflict(DateTimeOffset reportedUtc, TimeZoneInfo displayZone)
    {
        var at = TimeZoneInfo.ConvertTime(reportedUtc, displayZone);

        return string.Create(CultureInfo.InvariantCulture,
            $"Claude reports a different weekly reset time: {at:ddd HH:mm}. Using the reported time instead of what you entered. Re-enter it if your plan changed.");
    }

    /// <summary>The uncertainty width past which a reset instant is only bracketed, not exact.</summary>
    public static readonly TimeSpan ApproximateThreshold = TimeSpan.FromMinutes(30);

    private static bool IsApproximate(TimeSpan? uncertainty) => (uncertainty ?? TimeSpan.Zero) > ApproximateThreshold;

    /// <summary>
    /// Why an update check came back empty when GitHub throttled it (OVI-80). Worded
    /// independently from <c>O-view.Tray</c>'s notice (ADR-0003) — same two facts (the limit
    /// is per-network, not per-device, and the retry time is only stated when GitHub actually
    /// sent one), different phrasing.
    /// </summary>
    public static string RateLimitedNotice(DateTimeOffset? retryAfterUtc, TimeZoneInfo local) =>
        "GitHub throttles anonymous update checks, and the limit applies per network rather "
        + "than per device, so it can trip even if this is the only app checking. O-view "
        + "will retry "
        + (retryAfterUtc is { } at
            ? string.Create(CultureInfo.InvariantCulture, $"at {TimeZoneInfo.ConvertTime(at, local):HH:mm}.")
            : "on its next scheduled check.")
        + " Your connection and install are fine.";
}
