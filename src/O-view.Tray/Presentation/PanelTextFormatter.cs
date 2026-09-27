using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the detail panel's freshness/countdown/reset text from a
/// <see cref="UsageSnapshot"/> and the raw scalars the source app's <c>PanelText.cs</c>
/// also took directly (a duration, a reset instant). Every wording
/// decision here — the "As of"/"Local estimate" framing, the approximate marker, the unit
/// step-down in <see cref="Countdown"/> — lives in this Windows skin, and nowhere in
/// O-view.Core (ADR-0001). This class is not shared with O-view.Linux; each skin owns its
/// own phrasing (ADR-0003).
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
    /// guessing. Carries a <c>~</c> marker exactly when Core flags the instant
    /// <see cref="UsageValueStatus.Estimated"/> — the same signal and the same marker
    /// <see cref="TooltipFormatter"/> uses, so the panel and the tooltip cannot disagree about
    /// one value. Whether a bracketed reset counts as approximate is Core's call, not this
    /// skin's: no uncertainty width or threshold crosses the contract (ADR-0001, 2026-09-25
    /// amendment, D2).
    /// </summary>
    public static string SessionReset(UsageInstant resetAt, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (resetAt.Status == UsageValueStatus.Unavailable || resetAt.Value is not { } reset)
        {
            return "Reset time unknown (no reset observed yet)";
        }

        var at = TimeZoneInfo.ConvertTime(reset, displayZone);
        var marker = resetAt.Status == UsageValueStatus.Estimated ? "~" : "";

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

    /// <summary>
    /// The boost chip on a meter's label row: <c>50% Boosted · until 31 Aug · ends in 2w 4d 14h</c>.
    /// Every part is optional and drops out silently — with neither figure parsed, the chip is
    /// just <c>Boosted</c>, which is the floor this never falls below (the message itself is
    /// relayed verbatim in <see cref="BoostCard"/> regardless of whether either figure parsed).
    ///
    /// <para><b>The 281px Windows label-row width budget named in the source app is a real
    /// constraint on this skin (ADR-0001, 2026-09-21 amendment) but is not enforced here.</b>
    /// No <c>O-view.App</c> panel window exists yet in this repository to measure a rendered
    /// row against, so there is no live pixel budget to truncate or wrap to — the source app's
    /// abbreviated-month/weeks-days-hours wording is reproduced below because it is this
    /// skin's own choice of a compact phrasing, not because a width is being enforced. Whichever
    /// future slice wires this into a real WPF panel is responsible for adding the actual
    /// measure-and-truncate-or-wrap step this ADR reserves for the skin.</para>
    /// </summary>
    /// <param name="notice">The notice to describe.</param>
    /// <param name="utcNow">Now, for the countdown.</param>
    /// <param name="displayZone">The reader's zone: a promo ends at the end of its last local day.</param>
    public static string BoostChip(BoostNotice notice, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var chip = notice.Percent is { } pct
            ? string.Create(CultureInfo.InvariantCulture, $"{pct}% Boosted")
            : "Boosted";

        if (notice.EndsOn is not { } last)
        {
            return chip;
        }

        var ends = EndOfDayUtc(last, displayZone);
        return string.Create(CultureInfo.InvariantCulture,
            $"{chip} · until {last:d MMM} · ends in {BoostRemaining(ends - utcNow)}");
    }

    /// <summary>
    /// Time left on a promo, in weeks/days/hours: <c>2w 4d 14h</c>, <c>4d 14h</c>, <c>14h</c>.
    /// Empty leading units are dropped — a promo ending tonight reads <c>14h</c>, not
    /// <c>0w 0d 14h</c>. Hours are the floor: the source end is a <i>date</i>, so the last hour
    /// of that day is the finest thing anyone knows, and minutes would imply precision the
    /// sentence never carried.
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

        return parts.Count > 0 ? string.Join(" ", parts) : "under an hour";
    }

    /// <summary>
    /// The instant a promo's last day ends, in UTC — the next local midnight after it. Built
    /// from the zone's offset rather than <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/>,
    /// which throws when the wall-clock time it is handed does not exist (midnight is skipped
    /// outright in a handful of zones on their DST transition day, and a countdown must not be
    /// the thing that takes the panel down).
    /// </summary>
    private static DateTimeOffset EndOfDayUtc(DateOnly lastDay, TimeZoneInfo displayZone)
    {
        var midnight = lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, displayZone.GetUtcOffset(midnight)).ToUniversalTime();
    }

    /// <summary>
    /// The hover card behind the chip: Claude's sentence, then when O-view read it. The
    /// message is never edited, summarised, or re-worded — see <see cref="BoostNotice"/>'s
    /// doc comment for why the panel relays rather than asserts it.
    /// </summary>
    public static string BoostCard(BoostNotice notice, DateTimeOffset fetchedAtUtc, TimeZoneInfo displayZone)
    {
        var read = TimeZoneInfo.ConvertTime(fetchedAtUtc, displayZone);
        var ends = notice.EndsOn is { } last
            ? string.Create(CultureInfo.InvariantCulture, $"Ends {last:ddd d MMM} · ")
            : "";

        return string.Create(CultureInfo.InvariantCulture,
            $"{notice.Text}\n\n{ends}reported by Claude Code, read {read:HH:mm}");
    }

    /// <summary>
    /// Why an update check came back empty when GitHub throttled it (source app issue #176,
    /// OVI-98). Takes the raw <paramref name="retryAfterUtc"/>/<paramref name="local"/>
    /// scalars directly rather than a <see cref="UsageSnapshot"/> field — this notice is not
    /// part of the usage data contract at all, so extracting it needed no new Core surface
    /// (confirmed by the OVI-29 signature survey this slice's issue cites).
    ///
    /// <para>States the limit is shared by the caller's network, not blamed on their own
    /// connection: GitHub's unauthenticated rate limit is counted per IP address, so an
    /// office or VPN exit node reaches it without this user doing anything unusual. The retry
    /// time is stated only when GitHub actually sent one — inventing "try again in an hour"
    /// would be a fabricated number (the standing no-fabrication rule).</para>
    /// </summary>
    public static string RateLimitedNotice(DateTimeOffset? retryAfterUtc, TimeZoneInfo local) =>
        "GitHub limits how often it answers without an account, and that limit is shared by "
        + "everyone on your network. O-view will try again "
        + (retryAfterUtc is { } at
            ? string.Create(CultureInfo.InvariantCulture, $"after {TimeZoneInfo.ConvertTime(at, local):HH:mm}.")
            : "on its next check.")
        + " Nothing is wrong with your connection or your install.";

    /// <summary>
    /// The usage-tile caveat: the qualifiers that apply to the 31-day figures, joined with
    /// " · ", or empty when none do (OVI-165; ADR-0001's 2026-09-23 amendment). Wording is this
    /// skin's own historical text. A condition Core could not establish is stated as unknown,
    /// never left silent, because silence would read as "nothing to qualify".
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
            parts.Add("models without a published rate unknown");
        }
        else if (stats.UnpricedModels.ModelIds.Count > 0)
        {
            parts.Add($"excludes {string.Join(", ", stats.UnpricedModels.ModelIds)} (no published rate)");
        }

        if (stats.TtlUnrecordedCacheWritesWindow31d.Status == UsageValueStatus.Unavailable)
        {
            parts.Add("cache write durations unknown");
        }
        else if (stats.TtlUnrecordedCacheWritesWindow31d.Value > 0)
        {
            parts.Add($"{UsageFormatter.Tokens(stats.TtlUnrecordedCacheWritesWindow31d)} cache writes "
                      + "with no recorded duration, priced at the 5-minute rate");
        }

        if (stats.Rates.Status == UsageValueStatus.Unavailable)
        {
            parts.Add("rates unknown");
        }
        else if (stats.Rates.IsStale)
        {
            parts.Add(RateAge(stats.Rates));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// How old the rates are and where they came from: <c>rates: bundled, as of 24 Jun 2026</c>.
    /// Names the source as well as the date. Empty for an unavailable stamp: there is no source
    /// or date to state, and <see cref="Caveat"/> says the rates are unknown instead.
    /// </summary>
    public static string RateAge(RateCardStamp rates) =>
        rates is { Source: { } source, AsOf: { } asOf }
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"rates: {(source == RateCardSource.UserFile ? "user file" : "bundled")}, as of {asOf:d MMM yyyy}")
            : "";

    /// <summary>
    /// What the token and cost tiles permanently do not cover. Always shown, unlike the
    /// conditional caveats: a caveat that appears only sometimes teaches that its absence means
    /// full coverage, which would be false every time.
    /// </summary>
    public const string TokenScopeCaveat = "chat and cloud sessions not counted";

    /// <summary>
    /// Claude's own usage settings page, which is where the off-plan banner sends the reader
    /// for the figure O-view will not state. Deliberately not a Core constant (ADR-0001's
    /// 2026-09-27 amendment): it is skin-owned and duplicated in <c>O-view.Linux</c>, and the
    /// ADR-0003 fixture family below pins that both skins use the same value.
    /// </summary>
    public const string UsageSettingsUrl = "https://claude.ai/settings/usage";

    /// <summary>Label on the link to <see cref="UsageSettingsUrl"/>.</summary>
    public const string UsageSettingsLinkLabel = "Open usage settings in Claude";

    /// <summary>
    /// The off-plan banner's heading (OVI-168; ADR-0001's 2026-09-27 amendment).
    ///
    /// <para>Empty when <paramref name="divergence"/> is not off-plan
    /// (<see cref="DivergenceReading.IsOffPlan"/>) — a skin decides whether to show the banner
    /// at all, but the entry point itself asserts nothing for a state that never diverged.</para>
    ///
    /// <para>The exhausted-window heading only asserts a charge where the account setting
    /// supports it: an account with extra usage switched off could not be billed anything, so a
    /// single alarming heading for every exhausted window would be false for that population
    /// (source app issue #259). <paramref name="extraUsage"/> is <c>null</c> when Claude Code's
    /// cache did not say, which gets the bare observation.</para>
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
                ExtraUsageState.Enabled => "Plan limit reached — further work bills as extra usage",
                ExtraUsageState.Disabled => "Plan limit reached — extra usage is switched off",
                _ => "Plan limit reached",
            }
            : "This session's usage is not drawing from your plan";
    }

    /// <summary>
    /// The line under <see cref="OffPlanTitle"/>: the observation, then what Claude Code says
    /// about extra usage on this account, then when it said it.
    ///
    /// <para>Empty when <paramref name="divergence"/> is not off-plan, same as
    /// <see cref="OffPlanTitle"/>. Relayed and stamped, never asserted in this skin's own
    /// voice — this is a cached answer from another application that can be days old while
    /// looking exactly as current as a fresh one, so the provenance clause is what makes
    /// reporting it honest rather than a guess wearing a fact's clothes.</para>
    /// </summary>
    public static string OffPlanDetail(
        DivergenceReading divergence, ExtraUsageReading? extraUsage, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        if (!divergence.IsOffPlan)
        {
            return "";
        }

        var observation = divergence.State == DivergenceState.PlanLimitReached
            ? "The 5-hour window is exhausted, so continued work is not drawing from it."
            : DivergenceDetail(divergence.OutputTokensInWindow, divergence.PlanRisePoints);

        if (extraUsage is not { } reading)
        {
            return divergence.State == DivergenceState.PlanLimitReached
                ? "The 5-hour window is exhausted, so continued work is not drawing from it. Whether "
                  + "that bills as extra usage depends on your account settings, which O-view could "
                  + "not read."
                : observation;
        }

        var setting = reading.State == ExtraUsageState.Enabled
            ? "Extra usage is switched on for this account, so work past the plan allowance "
              + "can bill beyond it."
            : "Extra usage is switched off for this account, so work past the plan allowance "
              + "should not bill beyond it.";

        return string.Create(CultureInfo.InvariantCulture,
            $"{observation} {setting} ({ReadStamp(reading.FetchedAtUtc, utcNow, displayZone)})");
    }

    /// <summary>
    /// What local activity ran past a meter that is not moving: <c>About 1.2M output tokens
    /// ran in this window while the plan meter moved 3 points.</c> States the observation and
    /// stops there — naming a cause (whose billing, whose fault) is not this skin's job when
    /// the two numbers are what a reader can actually check.
    ///
    /// <para><paramref name="risePoints"/> is <c>null</c> when Core could not measure a rise
    /// (see <see cref="DivergenceReading.PlanRisePoints"/>'s doc comment). Nothing here renders
    /// "0 points" for that case — the clause naming the meter's movement is simply dropped.</para>
    /// </summary>
    private static string DivergenceDetail(TokenCount outputTokensInWindow, int? risePoints)
    {
        var tokens = UsageFormatter.Tokens(outputTokensInWindow);

        if (risePoints is not { } rise)
        {
            return $"About {tokens} output tokens ran in this window with no measurable change in the "
                + "plan meter. Usage that a plan meter does not account for is billed some other way "
                + "— O-view cannot see your billing, so check Settings → Usage in Claude for what it "
                + "was.";
        }

        return $"About {tokens} output tokens ran in this window while the plan meter moved {rise} "
            + $"point{(rise == 1 ? "" : "s")}. Usage that a plan meter does not account for is "
            + "billed some other way — O-view cannot see your billing, so check Settings → Usage "
            + "in Claude for what it was.";
    }

    /// <summary>
    /// <c>reported by Claude Code, read 09:07</c>, or <c>… read 28 Aug 12:50</c> once the
    /// reading is no longer from the reader's own today — the one thing this clause exists to
    /// disclose, so a bare <c>HH:mm</c> on an old reading would hide it.
    /// </summary>
    private static string ReadStamp(DateTimeOffset fetchedAtUtc, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var read = TimeZoneInfo.ConvertTime(fetchedAtUtc, displayZone);
        var today = TimeZoneInfo.ConvertTime(utcNow, displayZone).Date;

        return read.Date == today
            ? string.Create(CultureInfo.InvariantCulture, $"reported by Claude Code, read {read:HH:mm}")
            : string.Create(CultureInfo.InvariantCulture, $"reported by Claude Code, read {read:d MMM HH:mm}");
    }

    /// <summary>Sub-label noting that an off-plan figure includes work billed outside the plan.</summary>
    public static string OffPlanNote(bool offPlan) => offPlan ? "incl. off-plan usage" : "";

    /// <summary>
    /// The "Est. value/spend today" tile heading, which flips when usage goes off-plan —
    /// because then it genuinely is spend, not a modelled value within a plan that already
    /// costs nothing marginal.
    /// </summary>
    public static string EstTodayLabel(bool offPlan) => offPlan ? "Est. spend today" : "Est. value today";

    /// <summary>
    /// What the off-plan section means, shown on hover rather than as standing text. Both
    /// wordings carry the caveat that the figure is not what was charged — moving it must not
    /// lose it, which is why it has one definition here rather than being duplicated at every
    /// call site.
    /// </summary>
    public static string OffPlanHint(bool hasCreditUsage)
    {
        var models = string.Join(", ", CreditBilledModelIds.All);

        return hasCreditUsage
            ? $"Estimated at published API rates for models billed as extra usage ({models}). "
              + "O-view cannot read your credit balance; check your billing page for exact figures."
            : $"No credit-billed usage ({models}) recorded in the last 31 days. Off-plan usage "
              + "while O-view wasn't running isn't captured.";
    }
}
