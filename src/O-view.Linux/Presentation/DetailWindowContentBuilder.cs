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
/// Which of the 2x2 statistics tiles a <see cref="StatisticsTile"/> is (ADR-0008 D10b, gate G7
/// parity slice P10), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <c>StatisticsTileKind</c>. Core only aggregates the per-model breakdown over the 31-day
/// window (<see cref="ModelUsageBreakdown"/>'s own remarks) — there is no per-model split for
/// "today" — so only the two 31-day tiles can ever flip; see <see cref="StatisticsTile.CanFlip"/>.
/// </summary>
public enum StatisticsTileKind
{
    OutputTokensToday,
    EstimatedValueToday,
    OutputTokensWindow31d,
    EstimatedValueWindow31d,
}

/// <summary>
/// One model's proportional share of a flipped tile's figure (ADR-0008 D10b, gate G7 parity
/// slice P10) — a proportion of a figure Core already handed over, computed here, not a sum
/// (D10b's own boundary rule). <see cref="Fraction"/> is clamped into <c>0..1</c>. Carries no
/// colour — the slot palette and the three-slot cap are slice P11's own obligation; this slice
/// renders every segment in the same neutral tone.
/// </summary>
public sealed record StatisticsTileSegment(string ModelId, double Fraction);

/// <summary>
/// One of the detail window's 2x2 statistics tiles (ADR-0008 D10b, gate G7 parity slice P10),
/// the Linux counterpart of <c>O-view.Tray</c>'s own <c>StatisticsTile</c>.
/// <see cref="Breakdown"/> is built here, in <see cref="DetailWindowContentBuilder.Build"/> —
/// once per pushed <see cref="UsageDetail"/>, never recomputed when a tile is clicked (the "no
/// I/O on click" requirement) — so the window's own click handler only ever toggles which face of
/// an already-built tile is visible.
/// </summary>
/// <param name="Value">The tile's own figure, already run through <see cref="UsageFormatter"/>.</param>
/// <param name="CanFlip">Whether this tile has anything to flip to. <see langword="false"/> for
/// both "today" tiles (no per-model split exists for that window) and for a 31-day tile whose
/// breakdown could not be read, holds no rows, or sums to nothing to show a proportion of.</param>
/// <param name="Breakdown">Empty unless <see cref="CanFlip"/>.</param>
public sealed record StatisticsTile(
    StatisticsTileKind Kind,
    string Label,
    string Value,
    bool CanFlip,
    IReadOnlyList<StatisticsTileSegment> Breakdown);

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
    IReadOnlyList<StatisticsTile> StatisticsTiles,
    string CoverageCaption,
    string Caveat,
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
            StatisticsTiles: BuildStatisticsTiles(stats, detail.Models),
            CoverageCaption: PanelStatisticsFormatter.CoverageCaption(stats.HistoryCoverage),
            Caveat: PanelTextFormatter.Caveat(stats),
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
    /// Builds the 2x2 statistics tiles (ADR-0008 D10b, gate G7 parity slice P10), the Linux
    /// counterpart of <c>O-view.Tray</c>'s own <c>BuildStatisticsTiles</c> — independently
    /// implemented (D1), worded independently, same boundary rule. Each tile's own figure is
    /// Core's; a flippable tile's per-model breakdown is built here, up front, so the window's
    /// click handler never reads Core again (the "no I/O on click" requirement).
    /// </summary>
    private static IReadOnlyList<StatisticsTile> BuildStatisticsTiles(UsageStatistics stats, ModelUsageBreakdown models) => new[]
    {
        new StatisticsTile(
            StatisticsTileKind.OutputTokensToday,
            "Output tokens, today",
            UsageFormatter.Tokens(stats.OutputTokensToday),
            CanFlip: false,
            Breakdown: Array.Empty<StatisticsTileSegment>()),
        new StatisticsTile(
            StatisticsTileKind.EstimatedValueToday,
            "Estimated value, today",
            UsageFormatter.Usd(stats.EstimatedSpendToday),
            CanFlip: false,
            Breakdown: Array.Empty<StatisticsTileSegment>()),
        BuildWindowTile(
            StatisticsTileKind.OutputTokensWindow31d,
            "Output tokens, 31 days",
            UsageFormatter.Tokens(stats.OutputTokensWindow31d),
            models,
            row => (double)(row.OutputTokens.Value ?? 0)),
        BuildWindowTile(
            StatisticsTileKind.EstimatedValueWindow31d,
            "Estimated value, 31 days",
            UsageFormatter.Usd(stats.EstimatedValueWindow31d),
            models,
            row => (double)(row.EstimatedSpend.Value ?? 0)),
    };

    /// <summary>
    /// A 31-day tile's breakdown is a proportion of rows Core already aggregated — never a sum
    /// this skin performs over anything Core did not already total (D10b). Disabled (no glyph)
    /// when the breakdown could not be read, holds no rows, or every row's own figure for this
    /// tile is nothing to show a share of.
    /// </summary>
    private static StatisticsTile BuildWindowTile(
        StatisticsTileKind kind, string label, string value, ModelUsageBreakdown models, Func<ModelUsageRow, double> select)
    {
        if (models.Status != UsageValueStatus.Real || models.Rows.Count == 0)
        {
            return new StatisticsTile(kind, label, value, CanFlip: false, Breakdown: Array.Empty<StatisticsTileSegment>());
        }

        var amounts = models.Rows.Select(select).ToList();
        var total = amounts.Sum();
        if (total <= 0)
        {
            return new StatisticsTile(kind, label, value, CanFlip: false, Breakdown: Array.Empty<StatisticsTileSegment>());
        }

        var segments = new List<StatisticsTileSegment>(models.Rows.Count);
        for (var i = 0; i < models.Rows.Count; i++)
        {
            // A row with nothing to show (e.g. an unpriced model, for the Est. value tile) gets
            // no segment at all — a zero-width slice would read as "recorded, zero spend", a
            // different fact from "could not be priced" (D9a's own unpriced-model distinction).
            if (amounts[i] > 0)
            {
                segments.Add(new StatisticsTileSegment(models.Rows[i].ModelId, Math.Clamp(amounts[i] / total, 0.0, 1.0)));
            }
        }

        return new StatisticsTile(kind, label, value, CanFlip: true, Breakdown: segments);
    }

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
}
