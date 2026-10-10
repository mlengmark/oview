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
/// Phase 1 slice 3.3 (OVI-92) <c>BoostChip</c>/<c>BoostCard</c>, as of Phase 1 slice 3.4
/// (OVI-98) <c>RateLimitedNotice</c>, as of Phase 1 sub-slice 4 (OVI-165) <c>Caveat</c>/
/// <c>RateAge</c>, and as of Phase 1 sub-slice 5 (OVI-168) the off-plan banner's
/// <c>OffPlanTitle</c>/<c>OffPlanDetail</c>/<c>OffPlanNote</c>/<c>EstTodayLabel</c>/
/// <c>OffPlanHint</c>. This finishes the extraction of the source app's <c>PanelText.cs</c>.</para>
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

    /// <summary>
    /// The usage-tile caveat: the qualifiers that apply to the 31-day figures, joined with
    /// " · ", or empty when none do (OVI-165; ADR-0001's 2026-09-23 amendment). This skin's own
    /// wording. A condition Core could not establish is stated as unknown, never left silent.
    /// </summary>
    public static string Caveat(UsageStatistics stats)
    {
        var parts = new List<string>(4);

        if (PanelStatisticsFormatter.CoverageNote(stats.HistoryCoverage) is { Length: > 0 } coverage)
        {
            parts.Add(coverage);
        }

        if (stats.UnpricedModels.Status == UsageValueStatus.Unavailable)
        {
            parts.Add("unpriced models: unknown");
        }
        else if (stats.UnpricedModels.ModelIds.Count > 0)
        {
            parts.Add($"estimate excludes {string.Join(", ", stats.UnpricedModels.ModelIds)}: no published rate");
        }

        if (stats.TtlUnrecordedCacheWritesWindow31d.Status == UsageValueStatus.Unavailable)
        {
            parts.Add("cache write durations: unknown");
        }
        else if (stats.TtlUnrecordedCacheWritesWindow31d.Value > 0)
        {
            parts.Add($"{UsageFormatter.Tokens(stats.TtlUnrecordedCacheWritesWindow31d)} cache-write tokens "
                      + "have no recorded duration and are priced at the 5-minute rate");
        }

        if (stats.Rates.Status == UsageValueStatus.Unavailable)
        {
            parts.Add("rate table: unknown");
        }
        else if (stats.Rates.IsStale)
        {
            parts.Add(RateAge(stats.Rates));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Where the rates came from and how old they are, e.g. <c>rate table: bundled, dated
    /// 24 Jun 2026</c>. Empty for an unavailable stamp; <see cref="Caveat"/> says unknown instead.
    /// </summary>
    public static string RateAge(RateCardStamp rates) =>
        rates is { Source: { } source, AsOf: { } asOf }
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"rate table: {(source == RateCardSource.UserFile ? "user file" : "bundled")}, dated {asOf:d MMM yyyy}")
            : "";

    /// <summary>
    /// What the token and cost tiles permanently do not cover. Always shown (see the Windows
    /// skin's note): its absence would wrongly read as full coverage.
    /// </summary>
    public const string TokenScopeCaveat = "chat and cloud sessions are not counted";

    /// <summary>
    /// Claude's own usage settings page, which is where the off-plan banner sends the reader.
    /// The value must match <c>O-view.Tray</c>'s constant of the same name (ADR-0001's
    /// 2026-09-27 amendment leaves this skin-owned and duplicated on purpose; the ADR-0003
    /// fixture family below pins the two copies against drift). The link label is wording and
    /// is worded independently.
    /// </summary>
    public const string UsageSettingsUrl = "https://claude.ai/settings/usage";

    /// <summary>Label on the link to <see cref="UsageSettingsUrl"/>. Worded independently from
    /// the Windows skin (ADR-0003) — only the URL itself is pinned equal.</summary>
    public const string UsageSettingsLinkLabel = "Open Claude's usage settings";

    /// <summary>
    /// What clicking the weekly row's "not known" hint copies to the clipboard (ADR-0008
    /// slicing-table slice P9): the exact command that refreshes the cache this row reads,
    /// typed into Claude Code. Pinned equal to <c>O-view.Tray</c>'s constant of the same name —
    /// a literal command, not wording, same reasoning as <see cref="UsageSettingsUrl"/>.
    /// </summary>
    public const string RunUsageCommand = "/usage";

    /// <summary>
    /// The off-plan banner's heading (OVI-168; ADR-0001's 2026-09-27 amendment). Worded
    /// independently from the Windows skin (ADR-0003), but tells the same three-way story: an
    /// exhausted window asserts a charge only where the account setting supports it, and a
    /// window that never diverged renders nothing at all.
    /// </summary>
    public static string OffPlanTitle(DivergenceReading divergence, ExtraUsageReading? extraUsage)
    {
        if (!divergence.IsOffPlan)
        {
            return "";
        }

        return divergence.State == DivergenceState.PlanLimitReached
            ? extraUsage?.State switch
            {
                ExtraUsageState.Enabled => "Plan limit reached: further usage bills as extra usage",
                ExtraUsageState.Disabled => "Plan limit reached: extra usage is switched off",
                _ => "Plan limit reached",
            }
            : "Usage this session is not drawing from your plan";
    }

    /// <summary>
    /// The line under <see cref="OffPlanTitle"/>. Worded independently from the Windows skin,
    /// but relays and stamps the extra-usage setting equally rather than asserting it — this is
    /// a cached answer from another application that can look current for days after it stopped
    /// being fresh.
    /// </summary>
    public static string OffPlanDetail(
        DivergenceReading divergence, ExtraUsageReading? extraUsage, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (!divergence.IsOffPlan)
        {
            return "";
        }

        var observation = divergence.State == DivergenceState.PlanLimitReached
            ? "The 5-hour window is used up, so nothing further draws from it."
            : DivergenceDetail(divergence.OutputTokensInWindow, divergence.PlanRisePoints);

        if (extraUsage is not { } reading)
        {
            return divergence.State == DivergenceState.PlanLimitReached
                ? "The 5-hour window is used up, so nothing further draws from it. Whether that "
                  + "bills as extra usage depends on account settings O-view could not read."
                : observation;
        }

        var setting = reading.State == ExtraUsageState.Enabled
            ? "This account has extra usage switched on, so work past the plan allowance can "
              + "bill beyond it."
            : "This account has extra usage switched off, so work past the plan allowance "
              + "should not bill beyond it.";

        return string.Create(CultureInfo.InvariantCulture,
            $"{observation} {setting} ({ReadStamp(reading.FetchedAtUtc, utcNow, displayZone)})");
    }

    /// <summary>
    /// What local activity ran past a meter that is not moving. Worded independently from the
    /// Windows skin, but states the same two numbers and stops there — naming a cause is not
    /// this skin's job either.
    ///
    /// <para><paramref name="risePoints"/> null (no rise measurable) drops the meter-movement
    /// clause entirely rather than rendering a fabricated zero.</para>
    /// </summary>
    private static string DivergenceDetail(TokenCount outputTokensInWindow, int? risePoints)
    {
        var tokens = UsageFormatter.Tokens(outputTokensInWindow);

        if (risePoints is not { } rise)
        {
            return $"Roughly {tokens} output tokens ran in this window with no measurable plan-meter "
                + "movement. Usage a plan meter does not account for bills some other way — "
                + "O-view cannot see billing, so check Claude's Settings → Usage for what it was.";
        }

        return $"Roughly {tokens} output tokens ran in this window while the plan meter moved {rise} "
            + $"point{(rise == 1 ? "" : "s")}. Usage a plan meter does not account for bills some "
            + "other way — O-view cannot see billing, so check Claude's Settings → Usage for what "
            + "it was.";
    }

    /// <summary>
    /// <c>from Claude Code, read 09:07</c>, or <c>… read 28 Aug 12:50</c> once the reading is
    /// no longer from today — the one thing this clause exists to disclose.
    /// </summary>
    private static string ReadStamp(DateTimeOffset fetchedAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var read = TimeZoneInfo.ConvertTime(fetchedAtUtc, displayZone);
        var today = TimeZoneInfo.ConvertTime(utcNow, displayZone).Date;

        return read.Date == today
            ? string.Create(CultureInfo.InvariantCulture, $"from Claude Code, read {read:HH:mm}")
            : string.Create(CultureInfo.InvariantCulture, $"from Claude Code, read {read:d MMM HH:mm}");
    }

    /// <summary>Sub-label noting that an off-plan figure includes work billed outside the plan.</summary>
    public static string OffPlanNote(bool offPlan) => offPlan ? "includes off-plan usage" : "";

    /// <summary>
    /// The "Est. value/spend today" tile heading, which flips when usage goes off-plan.
    /// </summary>
    public static string EstTodayLabel(bool offPlan) => offPlan ? "Est. spend today" : "Est. value today";

    /// <summary>
    /// What the off-plan section means, shown on hover. Worded independently from the Windows
    /// skin, but carries the same caveat: the figure is not what was actually charged.
    /// </summary>
    public static string OffPlanHint(bool hasCreditUsage)
    {
        var models = string.Join(", ", CreditBilledModelIds.All);

        return hasCreditUsage
            ? $"Estimated at published API rates for extra-usage-billed models ({models}). O-view "
              + "cannot read your credit balance — check your billing page for exact figures."
            : $"No usage billed to credits ({models}) in the last 31 days. Off-plan usage while "
              + "O-view was not running is not captured.";
    }
}
