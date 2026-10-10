using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// One formatted row of <see cref="ModelUsageBreakdown.Rows"/>, pre-aggregated by Core and
/// never re-totalled here (ADR-0008 D9c) — each field is one existing-formatter call
/// (<see cref="UsageFormatter"/>) over the row Core already summed.
/// </summary>
public sealed record DetailWindowModelRow(
    string ModelId,
    string Requests,
    string InputTokens,
    string OutputTokens,
    string CacheWriteTokens,
    string CacheReadTokens,
    string EstimatedSpend);

/// <summary>
/// The 50/70 colour band a usage bar's fill falls into (ADR-0008 D10b, gate G7 parity slice P9) —
/// the Linux counterpart of <c>O-view.Tray</c>'s own <c>UsageBarBand</c>, independently declared
/// (D1) but carrying the same three boundaries, since the band is the shared contract (source
/// <c>ui-spec.md</c>: "the shared <c>UsageLevels</c> bands… same classifier drives the popup
/// bars") — only the colours and the wording are each skin's own.
/// </summary>
public enum UsageBarBand
{
    Green,
    Amber,
    Red,
}

/// <summary>
/// Which of the weekly row's three states applies (ADR-0008 D9f, gate G7 parity slice P9) — the
/// Linux counterpart of <c>O-view.Tray</c>'s own <c>WeeklyBarState</c>.
/// <see cref="UsageSnapshot.WeeklyResetAt"/>'s own status and
/// <see cref="UsageSnapshot.DataSourceKind"/> already distinguish all three — no new Core field
/// was needed (D9f).
/// </summary>
public enum WeeklyBarState
{
    Known,
    NotKnown,
    Hidden,
}

/// <summary>
/// Everything the detail window renders, built once from one <see cref="UsageDetail"/> and
/// nothing else (ADR-0008 D9c) — the window draws this record and reads no other source, so it
/// can never pair a percent from one poll with statistics from an earlier one.
/// </summary>
public sealed record DetailWindowContent(
    string Freshness,
    string AccountDisplayName,
    string AccountEmail,
    string AccountTierBadge,
    string SessionLine,
    double SessionBarFraction,
    UsageBarBand SessionBarBand,
    string WeeklyLine,
    WeeklyBarState WeeklyState,
    double WeeklyBarFraction,
    UsageBarBand WeeklyBarBand,
    string WeeklyUnknownHint,
    string ExtraUsageLine,
    string TodayLine,
    string Window31dLine,
    string Caveat,
    TokenKindBar TokenKindBarToday,
    TokenKindBar TokenKindBarWindow31d,
    IReadOnlyList<TokenKindBreakdownRow> TokenKindBreakdownRows,
    string TokenKindSectionNote,
    IReadOnlyList<DetailWindowModelRow> ModelRows,
    string ModelSectionNote);

/// <summary>
/// Builds the Linux detail window's content from a pushed <see cref="UsageDetail"/> (ADR-0008
/// slice 10, OVI-408), the Linux counterpart of <c>O-view.Tray</c>'s
/// <c>DetailWindowContentBuilder</c> (slice 6, OVI-386) — independently implemented, not shared
/// (D1). Renders only figures the detail carried — no figure here is summed, averaged or
/// re-bucketed by this skin (D9c); every per-model total is Core's own pre-aggregated
/// <see cref="ModelUsageRow"/>, formatted through the existing <see cref="UsageFormatter"/> and
/// <see cref="PanelTextFormatter"/>/<see cref="PanelStatisticsFormatter"/> the panel tile and
/// tooltip already use. Worded independently from the Windows skin throughout (ADR-0003) — the
/// same facts, a different sentence shape and a different estimated-marker convention (this
/// skin's own " (est.)" suffix, matching <see cref="TooltipFormatter"/>, not the Windows skin's
/// "~" prefix).
/// </summary>
public static class DetailWindowContentBuilder
{
    public static DetailWindowContent Build(UsageDetail detail, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var snapshot = detail.Snapshot;
        var stats = detail.Statistics;
        var weeklyState = ResolveWeeklyState(snapshot);

        return new DetailWindowContent(
            Freshness: PanelTextFormatter.Freshness(snapshot, utcNow, displayZone),
            AccountDisplayName: PanelTextFormatter.AccountDisplayName(detail.Account),
            AccountEmail: PanelTextFormatter.AccountEmail(detail.Account),
            AccountTierBadge: PanelTextFormatter.AccountTierBadge(detail.Account),
            SessionLine: "Session: " + PercentText(snapshot.SessionUtilizationPercent)
                + " — " + PanelTextFormatter.SessionReset(snapshot.SessionResetAt, utcNow, displayZone),
            SessionBarFraction: BarFraction(snapshot.SessionUtilizationPercent),
            SessionBarBand: Band(snapshot.SessionUtilizationPercent),
            WeeklyLine: WeeklyLine(weeklyState, snapshot, utcNow, displayZone),
            WeeklyState: weeklyState,
            WeeklyBarFraction: BarFraction(snapshot.WeeklyUtilizationPercent),
            WeeklyBarBand: Band(snapshot.WeeklyUtilizationPercent),
            WeeklyUnknownHint: weeklyState == WeeklyBarState.NotKnown ? WeeklyUnknownHintCardText : "",
            ExtraUsageLine: ExtraUsageLine(snapshot.ExtraUsage, displayZone),
            TodayLine: "Today so far: " + UsageFormatter.Tokens(stats.OutputTokensToday)
                + " tokens, " + UsageFormatter.Usd(stats.EstimatedSpendToday) + " estimated",
            Window31dLine: "31-day window: " + UsageFormatter.Tokens(stats.OutputTokensWindow31d)
                + " tokens, " + UsageFormatter.Usd(stats.EstimatedValueWindow31d) + " estimated value",
            Caveat: PanelTextFormatter.Caveat(stats),
            TokenKindBarToday: TokenKindBarFormatter.BuildBar("Today", detail.TokensToday),
            TokenKindBarWindow31d: TokenKindBarFormatter.BuildBar("31 days", detail.Tokens31d),
            TokenKindBreakdownRows: TokenKindBarFormatter.BuildBreakdownRows(detail.TokensToday, detail.Tokens31d),
            TokenKindSectionNote: TokenKindSectionNote(detail.TokensToday, detail.Tokens31d),
            ModelRows: BuildModelRows(detail.Models),
            ModelSectionNote: ModelSectionNote(detail.Models, stats.UnpricedModels));
    }

    /// <summary>
    /// <c>63% (est.)</c> or <c>unknown</c> for a percent Core could not establish — the same
    /// unavailable convention <see cref="UsageFormatter.Tokens"/> uses, never a fabricated 0%.
    /// The " (est.)" suffix matches <see cref="TooltipFormatter"/>'s own marker, so the tooltip
    /// and this window never disagree on which values are modelled.
    /// </summary>
    private static string PercentText(UsagePercent percent) => percent.Value is { } value
        ? string.Create(CultureInfo.InvariantCulture, $"{Round(value)}%{Marker(percent.Status)}")
        : "unknown";

    /// <summary>
    /// The weekly row's three states (ADR-0008 D9f, gate G7 parity slice P9): no plan data at
    /// all hides the row entirely; plan data with no weekly reset observed yet names the fix
    /// (<see cref="WeeklyUnknownLabel"/>/<see cref="WeeklyUnknownHintCardText"/>) rather than an
    /// indefinite wait; otherwise the existing <see cref="PanelTextFormatter.WeeklyReset"/> line,
    /// unchanged.
    /// </summary>
    private static WeeklyBarState ResolveWeeklyState(UsageSnapshot snapshot) =>
        snapshot.DataSourceKind == DataSourceKind.Unavailable
            ? WeeklyBarState.Hidden
            : snapshot.WeeklyResetAt.Status == UsageValueStatus.Unavailable
                ? WeeklyBarState.NotKnown
                : WeeklyBarState.Known;

    /// <summary>This skin's own inline label for <see cref="WeeklyBarState.NotKnown"/> — worded
    /// independently from the Windows skin's "Weekly reset time not known" (ADR-0003).</summary>
    private const string WeeklyUnknownLabel = "weekly reset not known yet";

    /// <summary>
    /// The hover card's text for the <see cref="WeeklyBarState.NotKnown"/> state: names the one
    /// step that ends it (source <c>ui-spec.md</c> §"Weekly reset" — run <c>/usage</c> in Claude
    /// Code, the only thing that refreshes the cache this reads) and says what a click does.
    /// Worded independently from the Windows skin (ADR-0003).
    /// </summary>
    private const string WeeklyUnknownHintCardText =
        "Not reported yet. Run /usage in Claude Code to learn it, or click to copy /usage.";

    private static string WeeklyLine(WeeklyBarState state, UsageSnapshot snapshot, DateTimeOffset utcNow, TimeZoneInfo zone) => state switch
    {
        WeeklyBarState.Hidden => "",
        WeeklyBarState.NotKnown => "Week: " + PercentText(snapshot.WeeklyUtilizationPercent) + " — " + WeeklyUnknownLabel,
        _ => "Week: " + PercentText(snapshot.WeeklyUtilizationPercent)
            + " — " + PanelTextFormatter.WeeklyReset(snapshot.WeeklyResetAt.Value!.Value, utcNow, zone),
    };

    /// <summary>
    /// The bar's proportional fill, clamped to <c>0..1</c> (ADR-0008 D10b: a fill width over a
    /// percent Core already handed over is presentation, not a sum). An unavailable percent
    /// renders as an empty bar rather than a fabricated reading.
    /// </summary>
    private static double BarFraction(UsagePercent percent) =>
        percent.Value is { } value ? Math.Clamp(value / 100.0, 0.0, 1.0) : 0.0;

    /// <summary>
    /// The shared 50/70 colour band (ADR-0008 D10b, gate G7 parity slice P9): green under 50%,
    /// amber from 50% up to 69%, red from 70% up — exact boundaries, not rounded ones. An
    /// unavailable percent reads as green, the same "nothing to warn about" default
    /// <see cref="UsageLevel.Green"/> uses elsewhere in this contract.
    /// </summary>
    private static UsageBarBand Band(UsagePercent percent) => percent.Value switch
    {
        { } value when value >= 70.0 => UsageBarBand.Red,
        { } value when value >= 50.0 => UsageBarBand.Amber,
        _ => UsageBarBand.Green,
    };

    /// <summary>
    /// Omitted entirely when Claude Code's own cache did not say (<see cref="UsageSnapshot.ExtraUsage"/>
    /// is <see langword="null"/>) — never a fabricated "off" for an account setting nobody read
    /// (ADR-0001, OVI-168).
    /// </summary>
    private static string ExtraUsageLine(ExtraUsageReading? extraUsage, TimeZoneInfo zone)
    {
        if (extraUsage is not { } reading)
        {
            return "";
        }

        var state = reading.State == ExtraUsageState.Enabled ? "on" : "off";
        var at = TimeZoneInfo.ConvertTime(reading.FetchedAtUtc, zone);

        return string.Create(CultureInfo.InvariantCulture, $"Extra usage {state}, as reported {at:HH:mm}");
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Marker(UsageValueStatus status) => status == UsageValueStatus.Estimated ? " (est.)" : "";

    private static IReadOnlyList<DetailWindowModelRow> BuildModelRows(ModelUsageBreakdown models)
    {
        if (models.Status != UsageValueStatus.Real)
        {
            return Array.Empty<DetailWindowModelRow>();
        }

        var rows = new List<DetailWindowModelRow>(models.Rows.Count);
        foreach (var row in models.Rows)
        {
            rows.Add(new DetailWindowModelRow(
                ModelId: row.ModelId,
                Requests: row.RequestCount.ToString(CultureInfo.InvariantCulture),
                InputTokens: UsageFormatter.Tokens(row.InputTokens),
                OutputTokens: UsageFormatter.Tokens(row.OutputTokens),
                CacheWriteTokens: UsageFormatter.Tokens(row.CacheCreationTokens),
                CacheReadTokens: UsageFormatter.Tokens(row.CacheReadTokens),
                EstimatedSpend: UsageFormatter.Usd(row.EstimatedSpend)));
        }

        return rows;
    }

    /// <summary>
    /// States the three cases <see cref="ModelUsageBreakdown"/>'s own remarks distinguish:
    /// Core could not read the ledger at all, Core read it and found no activity, or the window
    /// is a subtotal because at least one model in it could not be priced (D9a's unpriced-model
    /// pairing with <see cref="UsageStatistics.UnpricedModels"/>). Worded independently from the
    /// Windows skin (ADR-0003).
    /// </summary>
    private static string ModelSectionNote(ModelUsageBreakdown models, UnpricedModels unpriced)
    {
        if (models.Status != UsageValueStatus.Real)
        {
            return "Per-model breakdown not available.";
        }

        if (models.Rows.Count == 0)
        {
            return "No activity in this window.";
        }

        return unpriced.Status == UsageValueStatus.Real && unpriced.ModelIds.Count > 0
            ? "Partial total — " + string.Join(", ", unpriced.ModelIds) + " has no published rate."
            : "";
    }

    /// <summary>
    /// States plainly when neither window's token-kind totals could be read (gate G7 parity
    /// slice P12). Worded independently from the Windows skin (ADR-0003). Empty once either
    /// window is <see cref="UsageValueStatus.Real"/>, matching <see cref="TokenKindBar.HasData"/>'s
    /// own per-bar fallback.
    /// </summary>
    private static string TokenKindSectionNote(TokenKindTotals today, TokenKindTotals window31d) =>
        today.Status != UsageValueStatus.Real && window31d.Status != UsageValueStatus.Real
            ? "Token-kind totals not available."
            : "";
}
