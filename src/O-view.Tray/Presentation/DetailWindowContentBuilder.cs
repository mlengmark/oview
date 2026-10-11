using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

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
/// The 50/70 colour band a usage bar's fill falls into (ADR-0008 D10b, gate G7 parity slice P9):
/// the percent and its status are Core's; the band boundaries, like the fill width itself, are
/// this skin's own presentation decision over a figure Core already handed over. Green below
/// 50%, amber from 50% up to (not including) 70%, red from 70% up — so 49 is green, 50 and 69
/// are amber, 70 is red.
/// </summary>
public enum UsageBarBand
{
    Green,
    Amber,
    Red,
}

/// <summary>
/// Which of the weekly row's three states applies (ADR-0008 D9f, gate G7 parity slice P9, source
/// <c>ui-spec.md</c> §"Usage bars"): a reported reset instant, plan data with no reset observed
/// yet, or no plan data at all. <see cref="UsageSnapshot.WeeklyResetAt"/>'s own status and
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
/// parity slice P10). Core only aggregates the per-model breakdown over the 31-day window
/// (<see cref="ModelUsageBreakdown"/>'s own remarks) — there is no per-model split for "today" —
/// so only the two 31-day tiles can ever flip; see <see cref="StatisticsTile.CanFlip"/>.
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
/// slices P10/P11) — a proportion of a figure Core already handed over, computed here, not a sum
/// (D10b's own boundary rule). <see cref="Fraction"/> is clamped into <c>0..1</c> and the set of
/// fractions for one tile never exceeds 1 in total.
/// </summary>
/// <param name="ModelId">The model this segment identifies — the raw vendor id, verbatim, for a
/// named slot or a single folded leftover; the literal <c>"Other"</c> sentinel once two or more
/// models are folded (source <c>ui-spec.md</c> §2). Never a fabricated friendly name.</param>
/// <param name="ColourSlot">The index into the panel's shared <see cref="ModelColourOrder"/>
/// (0..<see cref="ModelColourOrder.MaxChromaticSlots"/>-1), or -1 for the neutral "Other"/single-
/// leftover slot — <see cref="WindowThemePalette"/> resolves this to an actual colour per
/// theme.</param>
/// <param name="LegendLabel">What the legend names this segment — the model id, or
/// <c>"Other"</c>. The legend shows names only; exact figures are hover-only
/// (<paramref name="HoverFigure"/>/<paramref name="HoverCaption"/>), the same relief the source
/// documents as a known limitation and does not try to fix here.</param>
/// <param name="HoverFigure">The hover card's headline (<see cref="HoverCard.Figure"/>) — this
/// segment's own figure, already formatted by <see cref="UsageFormatter"/>, or
/// <c>"unknown"</c> when an "Other" bucket contains an unpriced model (never a fabricated
/// partial sum presented as the whole).</param>
/// <param name="HoverCaption">The hover card's caption — the raw model id, or
/// <c>"N more models"</c> for a folded bucket of two or more, so "Other" is never an unexplained
/// slice (source <c>ui-spec.md</c> §2).</param>
public sealed record StatisticsTileSegment(
    string ModelId,
    double Fraction,
    int ColourSlot,
    string LegendLabel,
    string HoverFigure,
    string HoverCaption);

/// <summary>
/// One of the detail window's 2x2 statistics tiles (ADR-0008 D10b, gate G7 parity slice P10).
/// <see cref="Breakdown"/> is built here, in <see cref="DetailWindowContentBuilder.Build"/> —
/// once per pushed <see cref="UsageDetail"/>, never recomputed when a tile is clicked (the "no
/// I/O on click" requirement) — so the window's own click handler only ever toggles which face of
/// an already-built tile is visible.
/// </summary>
/// <param name="Value">The tile's own figure, already run through <see cref="UsageFormatter"/>.</param>
/// <param name="Note">Empty, or <c>"excl. N unpriced"</c> (gate G7 parity slice P11) when at
/// least one model in this window could not be priced and was therefore excluded from
/// <see cref="Breakdown"/> entirely — never a silent gap. Only ever populated on the "Est. value"
/// tile; the token tile has no "unpriced" concept.</param>
/// <param name="CanFlip">Whether this tile has anything to flip to. <see langword="false"/> for
/// both "today" tiles (no per-model split exists for that window) and for a 31-day tile whose
/// breakdown could not be read, holds no rows, or sums to nothing to show a proportion of.</param>
/// <param name="Breakdown">Empty unless <see cref="CanFlip"/>.</param>
public sealed record StatisticsTile(
    StatisticsTileKind Kind,
    string Label,
    string Value,
    string Note,
    bool CanFlip,
    IReadOnlyList<StatisticsTileSegment> Breakdown);

/// <summary>
/// Everything the detail window renders, built once from one <see cref="UsageDetail"/> and
/// nothing else (ADR-0008 D9c) — the window draws this record and reads no other source, so it
/// can never pair a percent from one poll with statistics from an earlier one.
/// </summary>
/// <param name="OffPlanBannerTitle">The off-plan banner's heading (gate G7 parity slice P15,
/// <see cref="PanelTextFormatter.OffPlanTitle"/>) — empty whenever
/// <see cref="UsageStatistics.Divergence"/> is <see langword="null"/> or not off-plan, which is
/// this window's own signal to hide the banner above the usage bars entirely.</param>
/// <param name="OffPlanBannerDetail">The banner's body (<see cref="PanelTextFormatter.OffPlanDetail"/>):
/// the observation, the account's extra-usage setting if Claude Code's cache said, and the
/// provenance stamp. Empty exactly when <see cref="OffPlanBannerTitle"/> is.</param>
/// <param name="OffPlanUsageTitle">The standing section's heading (gate G7 parity slice P17,
/// OVI-702, source <c>ui-spec.md</c> §"Off-plan usage"). Always shown — unlike
/// <see cref="OffPlanBannerTitle"/>'s live banner, this section stands regardless of whether the
/// current session is off-plan (ui-spec: "the 31-day figure shows even when the current session
/// is on-plan").</param>
/// <param name="OffPlanUsageValue">Estimated credit spend over the 31-day window
/// (<see cref="UsageStatistics.OffPlanUsageAmount"/>), through the existing
/// <see cref="UsageFormatter.Usd"/>. No priced rate card exists yet (OVI-592), so Core's own
/// value is always <see cref="UsageValueStatus.Unavailable"/> today and this renders
/// <c>"unknown"</c> — never a guessed dollar figure.</param>
/// <param name="OffPlanUsageCaption">The two-clause caveat (<see cref="PanelTextFormatter.OffPlanHint"/>):
/// published API rates, an unreadable credit balance, and where to check for the exact figure —
/// the clauses that stop this figure being read as money charged.</param>
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
    string OffPlanBannerTitle,
    string OffPlanBannerDetail,
    IReadOnlyList<StatisticsTile> StatisticsTiles,
    string CoverageCaption,
    string Caveat,
    TokenKindBar TokenKindBarToday,
    TokenKindBar TokenKindBarWindow31d,
    IReadOnlyList<TokenKindBreakdownRow> TokenKindBreakdownRows,
    string TokenKindSectionNote,
    string OffPlanUsageTitle,
    string OffPlanUsageValue,
    string OffPlanUsageCaption,
    IReadOnlyList<DetailWindowModelRow> ModelRows,
    string ModelSectionNote,
    IReadOnlyList<UsageGraphColumn> GraphColumns,
    IReadOnlyList<UsageGraphGridline> GraphGridlines);

/// <summary>
/// Builds the detail window's content from a pushed <see cref="UsageDetail"/> (ADR-0008 slice
/// 6, OVI-386). Renders only figures the detail carried — no figure here is summed, averaged or
/// re-bucketed by this skin (D9c); every per-model total is Core's own pre-aggregated
/// <see cref="ModelUsageRow"/>, formatted through the existing <see cref="UsageFormatter"/> and
/// <see cref="PanelTextFormatter"/>/<see cref="PanelStatisticsFormatter"/> the panel tile and
/// tooltip already use. The percent lines are the one piece of wording none of those three
/// formatters already covers (they format tokens, USD and durations, not a bare percent), so
/// this skin writes that one small sentence itself, following the same "~" estimated-marker
/// convention <see cref="TooltipFormatter"/> uses.
/// </summary>
public static class DetailWindowContentBuilder
{
    public static DetailWindowContent Build(UsageDetail detail, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var snapshot = detail.Snapshot;
        var stats = detail.Statistics;
        var weeklyState = ResolveWeeklyState(snapshot);
        var graphColumns = UsageGraphContentBuilder.Build(detail.History, detail.ResetBoundaries, displayZone);
        var divergence = stats.Divergence;
        var offPlanTitle = divergence is { } reading ? PanelTextFormatter.OffPlanTitle(reading, snapshot.ExtraUsage) : "";
        var offPlanDetail = divergence is { } reading2
            ? PanelTextFormatter.OffPlanDetail(reading2, snapshot.ExtraUsage, utcNow, displayZone)
            : "";

        return new DetailWindowContent(
            Freshness: PanelTextFormatter.Freshness(snapshot, utcNow, displayZone),
            AccountDisplayName: PanelTextFormatter.AccountDisplayName(detail.Account),
            AccountEmail: PanelTextFormatter.AccountEmail(detail.Account),
            AccountTierBadge: PanelTextFormatter.AccountTierBadge(detail.Account),
            SessionLine: PercentLine("5h", snapshot.SessionUtilizationPercent)
                + " · " + PanelTextFormatter.SessionReset(snapshot.SessionResetAt, utcNow, displayZone),
            SessionBarFraction: BarFraction(snapshot.SessionUtilizationPercent),
            SessionBarBand: Band(snapshot.SessionUtilizationPercent),
            WeeklyLine: WeeklyLine(weeklyState, snapshot, utcNow, displayZone),
            WeeklyState: weeklyState,
            WeeklyBarFraction: BarFraction(snapshot.WeeklyUtilizationPercent),
            WeeklyBarBand: Band(snapshot.WeeklyUtilizationPercent),
            WeeklyUnknownHint: weeklyState == WeeklyBarState.NotKnown ? WeeklyUnknownHintCardText : "",
            ExtraUsageLine: ExtraUsageLine(snapshot.ExtraUsage, utcNow, displayZone),
            OffPlanBannerTitle: offPlanTitle,
            OffPlanBannerDetail: offPlanDetail,
            StatisticsTiles: BuildStatisticsTiles(stats, detail.Models),
            CoverageCaption: PanelStatisticsFormatter.CoverageCaption(stats.HistoryCoverage),
            Caveat: PanelTextFormatter.Caveat(stats),
            TokenKindBarToday: TokenKindBarFormatter.BuildBar("Today", detail.TokensToday),
            TokenKindBarWindow31d: TokenKindBarFormatter.BuildBar("31 days", detail.Tokens31d),
            TokenKindBreakdownRows: TokenKindBarFormatter.BuildBreakdownRows(detail.TokensToday, detail.Tokens31d),
            TokenKindSectionNote: TokenKindSectionNote(detail.TokensToday, detail.Tokens31d),
            OffPlanUsageTitle: OffPlanUsageSectionTitle,
            OffPlanUsageValue: UsageFormatter.Usd(stats.OffPlanUsageAmount),
            OffPlanUsageCaption: PanelTextFormatter.OffPlanHint(stats.HasCreditUsage),
            ModelRows: BuildModelRows(detail.Models),
            ModelSectionNote: ModelSectionNote(detail.Models, stats.UnpricedModels),
            GraphColumns: graphColumns,
            GraphGridlines: UsageGraphGridlineBuilder.Build(graphColumns, detail.ResetBoundaries, displayZone));
    }

    /// <summary>
    /// <c>5h: 63%</c> or <c>5h: ?</c> for a percent Core could not establish — the same
    /// unavailable convention <see cref="UsageFormatter.Tokens"/> uses, not a fabricated 0%.
    /// </summary>
    private static string PercentLine(string label, UsagePercent percent) => percent.Value is { } value
        ? string.Create(CultureInfo.InvariantCulture, $"{label}: {Marker(percent.Status)}{Round(value)}%")
        : string.Create(CultureInfo.InvariantCulture, $"{label}: ?");

    /// <summary>
    /// The weekly row's three states (ADR-0008 D9f, gate G7 parity slice P9): no plan data at all
    /// hides the row entirely — the no-data explanation above already covers it; plan data with
    /// no weekly reset observed yet names the one fix rather than describing an indefinite wait
    /// (source <c>ui-spec.md</c> §"Weekly reset": "a setup step, not a wait"); otherwise the
    /// existing <see cref="PanelTextFormatter.WeeklyReset"/> line, unchanged.
    /// </summary>
    private static WeeklyBarState ResolveWeeklyState(UsageSnapshot snapshot) =>
        snapshot.DataSourceKind == DataSourceKind.Unavailable
            ? WeeklyBarState.Hidden
            : snapshot.WeeklyResetAt.Status == UsageValueStatus.Unavailable
                ? WeeklyBarState.NotKnown
                : WeeklyBarState.Known;

    /// <summary>The weekly row's inline label when the reset is <see cref="WeeklyBarState.NotKnown"/> —
    /// states the fact; the longer hover card (<see cref="WeeklyUnknownHintCardText"/>) carries the fix.</summary>
    private const string WeeklyUnknownLabel = "Weekly reset time not known";

    /// <summary>
    /// The hover card's text for the <see cref="WeeklyBarState.NotKnown"/> state: names the one
    /// step that ends it (source <c>ui-spec.md</c> §"Weekly reset" — run <c>/usage</c> in Claude
    /// Code, the only thing that refreshes the cache this reads) and says what a click does.
    /// </summary>
    private const string WeeklyUnknownHintCardText =
        "Run /usage in Claude Code to set this. Click to copy /usage to your clipboard.";

    /// <summary>The standing off-plan usage section's heading (gate G7 parity slice P17, OVI-702,
    /// source <c>ui-spec.md</c> §"Off-plan usage"). A fixed label, not a formatted value — it
    /// names what the section below it is, same convention as "By model".</summary>
    private const string OffPlanUsageSectionTitle = "Off-plan usage · last 31 days";

    private static string WeeklyLine(WeeklyBarState state, UsageSnapshot snapshot, DateTimeOffset utcNow, TimeZoneInfo zone) => state switch
    {
        WeeklyBarState.Hidden => "",
        WeeklyBarState.NotKnown => PercentLine("7d", snapshot.WeeklyUtilizationPercent) + " · " + WeeklyUnknownLabel,
        _ => PercentLine("7d", snapshot.WeeklyUtilizationPercent)
            + " · " + PanelTextFormatter.WeeklyReset(snapshot.WeeklyResetAt.Value!.Value, utcNow, zone),
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
    /// amber from 50% up to 69%, red from 70% up — exact boundaries, not rounded ones, so a
    /// value of 49.9 stays green and 50.0 is already amber. An unavailable percent reads as
    /// green, the same "nothing to warn about" default <see cref="UsageLevel.Green"/> uses
    /// elsewhere in this contract.
    /// </summary>
    private static UsageBarBand Band(UsagePercent percent) => percent.Value switch
    {
        { } value when value >= 70.0 => UsageBarBand.Red,
        { } value when value >= 50.0 => UsageBarBand.Amber,
        _ => UsageBarBand.Green,
    };

    /// <summary>
    /// Builds the 2x2 statistics tiles (ADR-0008 D10b, gate G7 parity slices P10/P11). Each
    /// tile's own figure is Core's; a flippable tile's per-model breakdown is built here, up
    /// front, so the window's click handler never reads Core again (the "no I/O on click"
    /// requirement). <see cref="ModelColourOrder.ColourOrder"/> is computed once, from the
    /// widest window (<paramref name="models"/>'s own 31-day rows), and handed to both 31-day
    /// tiles so a model wears the same colour on every tile it appears on.
    /// </summary>
    private static IReadOnlyList<StatisticsTile> BuildStatisticsTiles(UsageStatistics stats, ModelUsageBreakdown models)
    {
        var colourOrder = models.Status == UsageValueStatus.Real
            ? ModelColourOrder.ColourOrder(models.Rows)
            : Array.Empty<string>();

        return new[]
        {
            new StatisticsTile(
                StatisticsTileKind.OutputTokensToday,
                "Output tokens today",
                UsageFormatter.Tokens(stats.OutputTokensToday),
                Note: "",
                CanFlip: false,
                Breakdown: Array.Empty<StatisticsTileSegment>()),
            new StatisticsTile(
                StatisticsTileKind.EstimatedValueToday,
                "Est. value today",
                UsageFormatter.Usd(stats.EstimatedSpendToday),
                Note: "",
                CanFlip: false,
                Breakdown: Array.Empty<StatisticsTileSegment>()),
            BuildWindowTile(
                StatisticsTileKind.OutputTokensWindow31d,
                "Output tokens · 31 days",
                UsageFormatter.Tokens(stats.OutputTokensWindow31d),
                models,
                select: row => (double)(row.OutputTokens.Value ?? 0),
                formatRow: row => UsageFormatter.Tokens(row.OutputTokens),
                formatOther: rows => UsageFormatter.Tokens(new TokenCount(rows.Sum(r => r.OutputTokens.Value ?? 0), UsageValueStatus.Real)),
                total: stats.OutputTokensWindow31d.Value is { } outputTotal ? (double)outputTotal : null,
                colourOrder: colourOrder),
            BuildWindowTile(
                StatisticsTileKind.EstimatedValueWindow31d,
                "Est. value · 31 days",
                UsageFormatter.Usd(stats.EstimatedValueWindow31d),
                models,
                select: row => row.EstimatedSpend.Value is { } v ? (double)v : (double?)null,
                formatRow: row => UsageFormatter.Usd(row.EstimatedSpend),
                formatOther: rows => rows.Any(r => r.EstimatedSpend.Value is null)
                    ? UsageFormatter.Usd(new EstimatedUsd(null, UsageValueStatus.Unavailable))
                    : UsageFormatter.Usd(new EstimatedUsd(rows.Sum(r => r.EstimatedSpend.Value ?? 0m), UsageValueStatus.Estimated)),
                total: stats.EstimatedValueWindow31d.Value is { } spendTotal ? (double)spendTotal : null,
                colourOrder: colourOrder),
        };
    }

    /// <summary>
    /// A 31-day tile's breakdown is each row's share of the window total Core already handed
    /// over (<paramref name="total"/>) — never a sum this skin performs over the rows itself
    /// (ADR-0008 D10b). Disabled (no glyph, per the window) when the breakdown could not be
    /// read, holds no rows, or the window total is unavailable or nothing to show a share of.
    ///
    /// <para>Gate G7 parity slice P11: models are coloured by the panel's shared
    /// <paramref name="colourOrder"/>, never by this tile's own ranking (<see
    /// cref="ModelColourOrder"/>'s own remarks). A model outside the colour order folds into a
    /// neutral "Other" segment, except a single leftover, which keeps its own name (source
    /// <c>ui-spec.md</c> §2 — "Other · 1 more models" would be a clumsy way to say the one model
    /// it names). A model <paramref name="select"/> cannot price (a null amount) is excluded from
    /// the breakdown entirely and counted into this tile's own <c>Note</c> — "excl. N unpriced" —
    /// rather than silently vanishing; an "Other" bucket that still has something priced to show
    /// states that sum via <paramref name="formatOther"/>, which itself reports "unknown" rather
    /// than a partial total whenever any model folded into it could not be priced (rule 6).</para>
    /// </summary>
    private static StatisticsTile BuildWindowTile(
        StatisticsTileKind kind,
        string label,
        string value,
        ModelUsageBreakdown models,
        Func<ModelUsageRow, double?> select,
        Func<ModelUsageRow, string> formatRow,
        Func<IReadOnlyList<ModelUsageRow>, string> formatOther,
        double? total,
        IReadOnlyList<string> colourOrder)
    {
        if (models.Status != UsageValueStatus.Real || models.Rows.Count == 0 || total is not { } windowTotal || windowTotal <= 0)
        {
            return new StatisticsTile(kind, label, value, Note: "", CanFlip: false, Breakdown: Array.Empty<StatisticsTileSegment>());
        }

        var namedRows = new List<ModelUsageRow>();
        var restRows = new List<ModelUsageRow>();
        foreach (var row in models.Rows)
        {
            (ModelColourOrder.SlotFor(row.ModelId, colourOrder) >= 0 ? namedRows : restRows).Add(row);
        }

        var unpricedCount = 0;
        var segments = new List<StatisticsTileSegment>(colourOrder.Count + 1);

        foreach (var row in namedRows)
        {
            var amount = select(row);
            if (amount is null)
            {
                unpricedCount++;
                continue;
            }

            // A row with nothing to show (e.g. a model with zero output tokens on this window)
            // gets no segment at all — a zero-width slice would read as "recorded, zero spend",
            // a different fact from "could not be priced".
            if (amount <= 0)
            {
                continue;
            }

            var slot = ModelColourOrder.SlotFor(row.ModelId, colourOrder);
            segments.Add(new StatisticsTileSegment(
                row.ModelId, Math.Clamp(amount.Value / windowTotal, 0.0, 1.0), slot, row.ModelId, formatRow(row), row.ModelId));
        }

        if (restRows.Count > 0)
        {
            var restAmounts = restRows.Select(r => (Row: r, Amount: select(r))).ToList();
            unpricedCount += restAmounts.Count(x => x.Amount is null);

            // Whatever is left to actually show on THIS tile, after dropping anything unpriced
            // or zero — a model can be in the global "rest" group yet have nothing to show on
            // one particular tile (e.g. unpriced on the value tile), so whether this folds into
            // "Other" is decided per tile, not from the rest group's own raw size.
            var visibleRest = restAmounts.Where(x => x.Amount is { } a && a > 0).ToList();
            if (visibleRest.Count == 1)
            {
                // Still takes the neutral colour — it is not one of the panel's coloured
                // models — but keeps its own name rather than the "Other" label, since there
                // is only one model left to name (source ui-spec.md §2: "Other · 1 more models"
                // would be a clumsy way to say the one model it names).
                var (row, amount) = visibleRest[0];
                segments.Add(new StatisticsTileSegment(
                    row.ModelId, Math.Clamp(amount!.Value / windowTotal, 0.0, 1.0), -1, row.ModelId, formatRow(row), row.ModelId));
            }
            else if (visibleRest.Count > 1)
            {
                var restSum = visibleRest.Sum(x => x.Amount!.Value);
                segments.Add(new StatisticsTileSegment(
                    "Other", Math.Clamp(restSum / windowTotal, 0.0, 1.0), -1, "Other", formatOther(restRows), visibleRest.Count + " more models"));
            }
        }

        if (segments.Count == 0)
        {
            var emptyNote = unpricedCount > 0 ? "excl. " + unpricedCount + " unpriced" : "";
            return new StatisticsTile(kind, label, value, Note: emptyNote, CanFlip: false, Breakdown: Array.Empty<StatisticsTileSegment>());
        }

        var note = unpricedCount > 0 ? "excl. " + unpricedCount + " unpriced" : "";
        return new StatisticsTile(kind, label, value, Note: note, CanFlip: true, Breakdown: segments);
    }

    /// <summary>
    /// Omitted entirely when Claude Code's own cache did not say (<see cref="UsageSnapshot.ExtraUsage"/>
    /// is <see langword="null"/>) — never a fabricated "off" for an account setting nobody read
    /// (ADR-0001, OVI-168).
    /// </summary>
    private static string ExtraUsageLine(ExtraUsageReading? extraUsage, DateTimeOffset utcNow, TimeZoneInfo zone)
    {
        if (extraUsage is not { } reading)
        {
            return "";
        }

        var state = reading.State == ExtraUsageState.Enabled ? "on" : "off";
        var at = TimeZoneInfo.ConvertTime(reading.FetchedAtUtc, zone);

        return string.Create(CultureInfo.InvariantCulture, $"Extra usage: {state} (reported {at:HH:mm})");
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Marker(UsageValueStatus status) => status == UsageValueStatus.Estimated ? "~" : "";

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
    /// pairing with <see cref="UsageStatistics.UnpricedModels"/>).
    /// </summary>
    private static string ModelSectionNote(ModelUsageBreakdown models, UnpricedModels unpriced)
    {
        if (models.Status != UsageValueStatus.Real)
        {
            return "Per-model breakdown unavailable.";
        }

        if (models.Rows.Count == 0)
        {
            return "No activity recorded in this window.";
        }

        return unpriced.Status == UsageValueStatus.Real && unpriced.ModelIds.Count > 0
            ? "Subtotal — excludes " + string.Join(", ", unpriced.ModelIds) + " (no published rate)."
            : "";
    }

    /// <summary>
    /// States plainly when neither window's token-kind totals could be read (gate G7 parity
    /// slice P12) — never a silent empty bar with no explanation. Empty once either window is
    /// <see cref="UsageValueStatus.Real"/>, even if the other is not, matching
    /// <see cref="TokenKindBar.HasData"/>'s own per-bar fallback.
    /// </summary>
    private static string TokenKindSectionNote(TokenKindTotals today, TokenKindTotals window31d) =>
        today.Status != UsageValueStatus.Real && window31d.Status != UsageValueStatus.Real
            ? "Token-kind totals unavailable."
            : "";
}
