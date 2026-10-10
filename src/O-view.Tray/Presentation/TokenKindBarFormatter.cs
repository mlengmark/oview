using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Which of the four token kinds a <see cref="TokenKindBarSegment"/> carries (ADR-0008 D9e, gate
/// G7 parity slice P12). Used only to key a segment back to its own colour/hover identity — the
/// label a user reads is <see cref="TokenKindBarSegment.Label"/>, independently worded per skin.
/// </summary>
public enum TokenKind
{
    Input,
    Output,
    CacheCreation,
    CacheRead,
}

/// <summary>
/// One token kind's share of a <see cref="TokenKindBar"/> (gate G7 parity slice P12). Carries no
/// colour — same boundary <see cref="StatisticsTileSegment"/>'s own remarks draw for its tile
/// segments once P11 exists: a model/kind's colour is this window's own presentation decision,
/// not the content builder's. <see cref="Fraction"/> is a proportion of the bar's own carried
/// <see cref="TokenKindBar.Total"/> — never a sum this skin performs (D10b).
/// </summary>
/// <param name="Kind">Which token kind this segment is.</param>
/// <param name="Label">The segment's display label, e.g. "Input".</param>
/// <param name="Fraction">This kind's share of <see cref="TokenKindBar.Total"/>, clamped to
/// <c>0..1</c>.</param>
/// <param name="ShareText">The share, already formatted as a percent (e.g. <c>"42%"</c>).</param>
/// <param name="HoverFigure">The hover card's headline — this kind's own token count.</param>
/// <param name="HoverCaption">The hover card's caption — this kind's label and modelled
/// value.</param>
public sealed record TokenKindBarSegment(
    TokenKind Kind,
    string Label,
    double Fraction,
    string ShareText,
    string HoverFigure,
    string HoverCaption);

/// <summary>
/// One of the detail window's two token-kind bars — "today" or the 31-day window (gate G7 parity
/// slice P12, ADR-0008 D9e). <see cref="Segments"/> excludes any kind with nothing to show (the
/// same "no zero-width slice" rule <see cref="StatisticsTileSegment"/>'s own builder uses), and is
/// built once per pushed <see cref="UsageDetail"/> — the window never recomputes it.
/// </summary>
/// <param name="Label">"Today" or "31 days".</param>
/// <param name="Total">The bar's own total, already formatted.</param>
/// <param name="HasData">Whether <see cref="Segments"/> has anything to render — <see
/// langword="false"/> renders an empty track rather than a fabricated reading.</param>
/// <param name="Segments">The four kinds' shares, in a fixed input/output/cache-write/cache-read
/// order, minus any kind with nothing to show.</param>
public sealed record TokenKindBar(string Label, string Total, bool HasData, IReadOnlyList<TokenKindBarSegment> Segments);

/// <summary>
/// One row of the breakdown table behind the view switch (gate G7 parity slice P12) — one token
/// kind's figures across both windows, built once alongside the two bars so the switch only ever
/// toggles visibility, never triggers a read (the same "no I/O on click" rule slice P10 already
/// established for a flipped statistics tile).
/// </summary>
public sealed record TokenKindBreakdownRow(string Label, string TodayTokens, string TodayValue, string Window31dTokens, string Window31dValue);

/// <summary>
/// Builds the detail window's token-kind bars and their breakdown table (ADR-0008 D9e, gate G7
/// parity slice P12) from the two <see cref="TokenKindTotals"/> already carried on
/// <see cref="UsageDetail"/> (slice P6/P5) — this slice adds no new Core surface, only a new
/// skin-side read of fields no skin consumed yet. This section is a superset of the statistics
/// tiles (slice P10), not a breakdown of them: it reports the same two windows split a different
/// way (by token kind rather than by model), and is wired into the window as its own section
/// rather than nested under the tiles.
/// </summary>
public static class TokenKindBarFormatter
{
    private static readonly (TokenKind Kind, string Label)[] Kinds =
    {
        (TokenKind.Input, "Input"),
        (TokenKind.Output, "Output"),
        (TokenKind.CacheCreation, "Cache write"),
        (TokenKind.CacheRead, "Cache read"),
    };

    public static TokenKindBar BuildBar(string label, TokenKindTotals totals)
    {
        if (totals.Status != UsageValueStatus.Real)
        {
            return new TokenKindBar(label, UsageFormatter.Tokens(totals.Total), HasData: false, Array.Empty<TokenKindBarSegment>());
        }

        var amounts = Amounts(totals);
        var totalValue = (double)(totals.Total.Value ?? 0);
        var segments = new List<TokenKindBarSegment>(Kinds.Length);
        for (var i = 0; i < Kinds.Length; i++)
        {
            var tokenValue = (double)(amounts[i].Tokens.Value ?? 0);
            if (tokenValue <= 0)
            {
                // Nothing recorded for this kind — excluded, not a zero-width slice (same
                // distinction StatisticsTileSegment's own builder draws for an unpriced model).
                continue;
            }

            var fraction = totalValue > 0 ? Math.Clamp(tokenValue / totalValue, 0.0, 1.0) : 0.0;
            segments.Add(new TokenKindBarSegment(
                Kinds[i].Kind,
                Kinds[i].Label,
                fraction,
                ShareText: FormatShare(fraction),
                HoverFigure: UsageFormatter.Tokens(amounts[i].Tokens),
                HoverCaption: Kinds[i].Label + " · " + UsageFormatter.Usd(amounts[i].EstimatedValue)));
        }

        return new TokenKindBar(label, UsageFormatter.Tokens(totals.Total), HasData: segments.Count > 0, segments);
    }

    public static IReadOnlyList<TokenKindBreakdownRow> BuildBreakdownRows(TokenKindTotals today, TokenKindTotals window31d)
    {
        if (today.Status != UsageValueStatus.Real && window31d.Status != UsageValueStatus.Real)
        {
            return Array.Empty<TokenKindBreakdownRow>();
        }

        var todayAmounts = Amounts(today);
        var windowAmounts = Amounts(window31d);
        var rows = new List<TokenKindBreakdownRow>(Kinds.Length);
        for (var i = 0; i < Kinds.Length; i++)
        {
            rows.Add(new TokenKindBreakdownRow(
                Kinds[i].Label,
                UsageFormatter.Tokens(todayAmounts[i].Tokens),
                UsageFormatter.Usd(todayAmounts[i].EstimatedValue),
                UsageFormatter.Tokens(windowAmounts[i].Tokens),
                UsageFormatter.Usd(windowAmounts[i].EstimatedValue)));
        }

        return rows;
    }

    private static TokenKindAmount[] Amounts(TokenKindTotals totals) =>
        new[] { totals.Input, totals.Output, totals.CacheCreation, totals.CacheRead };

    /// <summary>Rounded, not truncated — a 49.6% share reads as 50%, matching
    /// <see cref="DetailWindowContentBuilder"/>'s own percent-line rounding.</summary>
    private static string FormatShare(double fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero)}%");
}
