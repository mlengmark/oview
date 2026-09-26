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
/// <c>WeeklyReset</c>/<c>WeeklyResetConflict</c> family (Phase 1 slice 3.1, OVI-29), as of
/// Phase 1 slice 3.3 (OVI-92) <c>BoostChip</c>/<c>BoostCard</c>, and as of Phase 1 slice 3.4
/// (OVI-98) <c>RateLimitedNotice</c>. The remaining <c>PanelText.cs</c> members are separate,
/// differently-shaped sub-slices, not yet extracted.</para>
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
    /// The session-reset line. Carries a <c>(approx.)</c> suffix, this skin's own marker,
    /// exactly when Core flags the instant <see cref="UsageValueStatus.Estimated"/> — the same
    /// signal <see cref="TooltipFormatter"/>'s <c>(est.)</c> suffix reads, so the panel and the
    /// tooltip mark the same values even though they word the mark differently. No uncertainty
    /// width or threshold crosses the contract (ADR-0001, 2026-09-25 amendment, D2).
    /// </summary>
    public static string SessionReset(UsageInstant resetAt, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (resetAt.Status == UsageValueStatus.Unavailable || resetAt.Value is not { } reset)
        {
            return "No reset observed yet";
        }

        var at = TimeZoneInfo.ConvertTime(reset, displayZone);
        var suffix = resetAt.Status == UsageValueStatus.Estimated ? " (approx.)" : "";

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

    /// <summary>
    /// The boost chip on a meter's label row: <c>Boosted 50%, until 31 Aug, ends in 2w, 4d, 14h</c>.
    /// Worded independently from the Windows skin's <c>·</c>-joined phrase (ADR-0003), but the
    /// same facts either way: the percentage (when parsed), the end date, and the
    /// weeks/days/hours remaining. Every part is optional and drops out silently — with neither
    /// figure parsed, the chip is just <c>Boosted</c>.
    ///
    /// <para>The source app's 281px Windows label-row width budget (ADR-0001, 2026-09-21
    /// amendment) has no equivalent here — this skin has no reason to share a WPF pixel
    /// measurement, and no Avalonia panel window exists yet in this repository to measure a
    /// rendered row against either. Whichever future slice wires this into a real Avalonia
    /// panel picks this skin's own width budget and truncate-or-wrap rule then.</para>
    /// </summary>
    public static string BoostChip(BoostNotice notice, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var chip = notice.Percent is { } pct
            ? string.Create(CultureInfo.InvariantCulture, $"Boosted {pct}%")
            : "Boosted";

        if (notice.EndsOn is not { } last)
        {
            return chip;
        }

        var ends = EndOfDayUtc(last, displayZone);
        return string.Create(CultureInfo.InvariantCulture,
            $"{chip}, until {last:d MMM}, ends in {BoostRemaining(ends - utcNow)}");
    }

    /// <summary>
    /// Time left on a promo, in weeks/days/hours: <c>2w, 4d, 14h</c>, <c>4d, 14h</c>,
    /// <c>14h</c>. Empty leading units are dropped. Hours are the floor: the source end is a
    /// <i>date</i>, so the last hour of that day is the finest thing anyone knows.
    /// </summary>
    private static string BoostRemaining(TimeSpan left)
    {
        if (left <= TimeSpan.Zero)
        {
            return "under an hour";
        }

        var weeks = left.Days / 7;
        var days = left.Days % 7;
        var hours = left.Hours;

        var parts = new List<string>(3);
        if (weeks > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{weeks}w"));
        }

        if (days > 0 || parts.Count > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{days}d"));
        }

        if (hours > 0 || parts.Count > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{hours}h"));
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "under an hour";
    }

    /// <summary>
    /// The instant a promo's last day ends, in UTC — the next local midnight after it. Built
    /// from the zone's offset rather than <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>,
    /// which throws when the wall-clock time it is handed does not exist.
    /// </summary>
    private static DateTimeOffset EndOfDayUtc(DateOnly lastDay, TimeZoneInfo displayZone)
    {
        var midnight = lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, displayZone.GetUtcOffset(midnight)).ToUniversalTime();
    }

    /// <summary>
    /// The hover card behind the chip: Claude's sentence, then when O-view read it. Worded
    /// independently from the Windows skin, but relays <see cref="BoostNotice.Text"/> equally
    /// verbatim — see that type's doc comment for why.
    /// </summary>
    public static string BoostCard(BoostNotice notice, DateTimeOffset fetchedAtUtc, TimeZoneInfo displayZone)
    {
        var read = TimeZoneInfo.ConvertTime(fetchedAtUtc, displayZone);
        var ends = notice.EndsOn is { } last
            ? string.Create(CultureInfo.InvariantCulture, $"Ends {last:ddd d MMM}. ")
            : "";

        return string.Create(CultureInfo.InvariantCulture,
            $"{notice.Text}\n\n{ends}From Claude Code, read at {read:HH:mm}");
    }

    /// <summary>
    /// Why an update check came back empty when GitHub throttled it (OVI-98). Worded
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
