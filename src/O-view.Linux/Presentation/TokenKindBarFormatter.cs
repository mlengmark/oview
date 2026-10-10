using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

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
/// One token kind's share of a <see cref="TokenKindBar"/> (gate G7 parity slice P12), the Linux
/// counterpart of <c>O-view.Tray</c>'s own <see cref="TokenKindBarSegment"/> — independently
/// implemented, not shared (D1). Carries no colour; <see cref="Fraction"/> is a proportion of the
/// bar's own carried <see cref="TokenKindBar.Total"/> — never a sum this skin performs (D10b).
/// </summary>
public sealed record TokenKindBarSegment(
    TokenKind Kind,
    string Label,
    double Fraction,
    string ShareText,
    string HoverFigure,
    string HoverCaption);

/// <summary>
/// One of the detail window's two token-kind bars — "today" or the 31-day window (gate G7 parity
/// slice P12, ADR-0008 D9e), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <see cref="TokenKindBar"/> — independently implemented, not shared (D1). <see cref="Segments"/>
/// excludes any kind with nothing to show, and is built once per pushed <see cref="UsageDetail"/>.
/// </summary>
public sealed record TokenKindBar(string Label, string Total, bool HasData, IReadOnlyList<TokenKindBarSegment> Segments);

/// <summary>
/// One row of the breakdown table behind the view switch (gate G7 parity slice P12) — one token
/// kind's figures across both windows, built once alongside the two bars so the switch only ever
/// toggles visibility, never triggers a read.
/// </summary>
public sealed record TokenKindBreakdownRow(string Label, string TodayTokens, string TodayValue, string Window31dTokens, string Window31dValue);

/// <summary>
/// Builds the detail window's token-kind bars and their breakdown table (ADR-0008 D9e, gate G7
/// parity slice P12), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <see cref="TokenKindBarFormatter"/> — independently implemented, not shared (D1), from the two
/// <see cref="TokenKindTotals"/> already carried on <see cref="UsageDetail"/> (slice P6/P5). This
/// section is a superset of the statistics tiles (slice P10), not a breakdown of them: it reports
/// the same two windows split a different way (by token kind rather than by model), and is wired
/// into the window as its own section rather than nested under the tiles.
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

    private static string FormatShare(double fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero)}%");
}
