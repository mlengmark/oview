using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the detail panel's freshness/countdown/reset text from a
/// <see cref="UsageSnapshot"/> and the raw scalars the source app's <c>PanelText.cs</c>
/// also took directly (a duration, a reset instant, an uncertainty span). Every wording
/// decision here — the "As of"/"Local estimate" framing, the approximate marker, the unit
/// step-down in <see cref="Countdown"/> — lives in this Windows skin, and nowhere in
/// O-view.Core (ADR-0001). This class is not shared with O-view.Linux; each skin owns its
/// own phrasing (ADR-0003).
///
/// <para>Covers only the <c>Freshness</c>/<c>Countdown</c>/<c>SessionReset</c>/
/// <c>WeeklyReset</c>/<c>WeeklyResetConflict</c> family (Phase 1 slice 3.1, OVI-29). The
/// remaining <c>PanelText.cs</c> members — the boost promo chip, the usage-tile caveat, the
/// off-plan banner, the GitHub rate-limit notice — are separate, differently-shaped
/// sub-slices with their own Core surface, not yet extracted.</para>
/// </summary>
public static class PanelTextFormatter
{
    /// <summary>
    /// The header's freshness line: <c>As of now</c>, <c>As of 11:34</c>,
    /// <c>Local estimate · as of 11:34</c>, <c>No data</c>.
    ///
    /// <para><see cref="DataSourceKind.Live"/>, <see cref="DataSourceKind.Stale"/>, and
    /// <see cref="DataSourceKind.JsonlFallback"/> collapse into the same <c>"As of {age}"</c>
    /// wording on purpose: <see cref="UsageSnapshot.LastIngestAt"/>'s age says how old a
    /// reading is more precisely than the confidence tier does, and all three tiers are
    /// observed (not modelled) data either way. Only <see cref="DataSourceKind.Estimate"/> —
    /// modelled from pricing, not observed — gets the "Local estimate" framing.</para>
    /// </summary>
    public static string Freshness(UsageSnapshot snapshot, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (snapshot.DataSourceKind == DataSourceKind.Unavailable)
        {
            return "No data";
        }

        var age = AsOf(snapshot.LastIngestAt, utcNow, displayZone);

        return snapshot.DataSourceKind == DataSourceKind.Estimate
            ? string.Create(CultureInfo.InvariantCulture, $"Local estimate · as of {age}")
            : string.Create(CultureInfo.InvariantCulture, $"As of {age}");
    }

    /// <summary>
    /// <c>now</c> for a capture in the current clock minute, otherwise its local
    /// <c>HH:mm</c>.
    /// </summary>
    private static string AsOf(DateTimeOffset lastIngestAt, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var captured = TimeZoneInfo.ConvertTime(lastIngestAt, displayZone);
        var now = TimeZoneInfo.ConvertTime(utcNow, displayZone);

        // Both conditions are needed. The elapsed check alone would call a 40-second-old
        // reading "now" across a minute boundary; the clock-minute check alone misreads the
        // DST fall-back hour, where an instant 40 minutes ago carries a later local minute
        // than now. A capture in the future is a clock adjustment, not a prediction — report
        // it as now rather than stamping the panel with a time that has not happened.
        var elapsed = utcNow - lastIngestAt;
        var withinThisMinute = elapsed < TimeSpan.FromMinutes(1)
            && (elapsed < TimeSpan.Zero || Minute(captured) == Minute(now));

        return withinThisMinute
            ? "now"
            : string.Create(CultureInfo.InvariantCulture, $"{captured:HH:mm}");
    }

    private static DateTime Minute(DateTimeOffset t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

    /// <summary>
    /// A duration as this panel says it: <c>3d 4h</c>, <c>2h 14m</c>, <c>14m</c>, or
    /// <c>under a minute</c>. Units step down with the magnitude so the line stays short and
    /// never implies precision it does not have.
    /// </summary>
    public static string Countdown(TimeSpan t) => t.TotalMinutes < 1
        ? "under a minute"
        : t.TotalDays >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{t.Days}d {t.Hours}h")
            : t.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}h {t.Minutes}m")
                : string.Create(CultureInfo.InvariantCulture, $"{t.Minutes}m");

    /// <summary>
    /// The session-reset line. Before a reset has been observed, says so rather than
    /// guessing. Carries a <c>~</c> marker when the reset instant is only bracketed
    /// (<paramref name="uncertainty"/> wider than <see cref="ApproximateThreshold"/>) — the
    /// same marker <see cref="TooltipFormatter"/> uses for an estimated field.
    /// </summary>
    public static string SessionReset(
        DateTimeOffset? resetAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        TimeSpan? uncertainty = null)
    {
        if (resetAtUtc is not { } reset)
        {
            return "Reset time unknown (no reset observed yet)";
        }

        var at = TimeZoneInfo.ConvertTime(reset, displayZone);
        var marker = IsApproximate(uncertainty) ? "~" : "";

        return string.Create(CultureInfo.InvariantCulture,
            $"Resets in {Countdown(reset - utcNow)} · {marker}{at:HH:mm}");
    }

    /// <summary>
    /// The weekly-reset line. Never approximate — the weekly reset is a reported instant,
    /// projected forward by whole weeks, so it carries zero uncertainty (unlike the
    /// five-hour session window, which rolls from first use and stays bracketed).
    /// </summary>
    public static string WeeklyReset(DateTimeOffset resetAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var at = TimeZoneInfo.ConvertTime(resetAtUtc, displayZone);

        return string.Create(CultureInfo.InvariantCulture,
            $"Resets in {Countdown(resetAtUtc - utcNow)} · {at:ddd HH:mm}");
    }

    /// <summary>
    /// Told to the user when an observed weekly reset disagrees with one they entered
    /// themselves. States what was observed and what O-view is doing about it, without
    /// deciding for them why the two disagree.
    /// </summary>
    public static string WeeklyResetConflict(DateTimeOffset reportedUtc, TimeZoneInfo displayZone)
    {
        var at = TimeZoneInfo.ConvertTime(reportedUtc, displayZone);

        return string.Create(CultureInfo.InvariantCulture,
            $"Claude reports your weekly limit resetting {at:ddd HH:mm}, which does not match the time you entered. O-view is using the reported time. Re-enter yours if your plan changed.");
    }

    /// <summary>The uncertainty width past which a reset instant is only bracketed, not exact.</summary>
    public static readonly TimeSpan ApproximateThreshold = TimeSpan.FromMinutes(30);

    private static bool IsApproximate(TimeSpan? uncertainty) => (uncertainty ?? TimeSpan.Zero) > ApproximateThreshold;
}
