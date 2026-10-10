using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves <see cref="TokenKindBarFormatter"/> (gate G7 parity slice P12, ADR-0008 D9e): every
/// segment's share is a proportion of the <see cref="TokenKindTotals.Total"/> the fixture itself
/// carried in — never a sum this skin re-performs over the four kinds — and a kind with nothing
/// recorded never renders as a zero-width slice.
/// </summary>
public sealed class TokenKindBarFormatterTests
{
    private static TokenKindTotals RealTotals(long input, long output, long cacheCreation, long cacheRead, long total) => new(
        new DateOnly(2026, 9, 8),
        new DateOnly(2026, 9, 8),
        new TokenKindAmount(new TokenCount(input, UsageValueStatus.Real), new EstimatedUsd(1.00m, UsageValueStatus.Estimated)),
        new TokenKindAmount(new TokenCount(output, UsageValueStatus.Real), new EstimatedUsd(2.00m, UsageValueStatus.Estimated)),
        new TokenKindAmount(new TokenCount(cacheCreation, UsageValueStatus.Real), new EstimatedUsd(0.10m, UsageValueStatus.Estimated)),
        new TokenKindAmount(new TokenCount(cacheRead, UsageValueStatus.Real), new EstimatedUsd(0.05m, UsageValueStatus.Estimated)),
        new TokenCount(total, UsageValueStatus.Real),
        new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
        UsageValueStatus.Real);

    [Fact]
    public void Unavailable_totals_render_an_empty_bar_not_a_fabricated_reading()
    {
        var bar = TokenKindBarFormatter.BuildBar("Today", TokenKindTotals.Unavailable);

        Assert.False(bar.HasData);
        Assert.Empty(bar.Segments);
    }

    /// <summary>
    /// The one fact OVI-667 asked a test to pin: a segment's share text is computed against the
    /// carried <see cref="TokenKindTotals.Total"/> field, not against a sum of the four kinds
    /// this formatter re-performs. Here the four kinds sum to 900, but <c>Total</c> carries 1000
    /// (e.g. a kind the contract does not expose yet) — the share must still divide by 1000.
    /// </summary>
    [Fact]
    public void A_segments_share_divides_by_the_carried_total_not_a_resummed_one()
    {
        var totals = RealTotals(input: 400, output: 300, cacheCreation: 150, cacheRead: 50, total: 1000);

        var bar = TokenKindBarFormatter.BuildBar("Today", totals);

        var input = Assert.Single(bar.Segments, s => s.Kind == TokenKind.Input);
        Assert.Equal("40%", input.ShareText);
        Assert.Equal(0.40, input.Fraction, 3);

        var output = Assert.Single(bar.Segments, s => s.Kind == TokenKind.Output);
        Assert.Equal("30%", output.ShareText);

        var cacheCreation = Assert.Single(bar.Segments, s => s.Kind == TokenKind.CacheCreation);
        Assert.Equal("15%", cacheCreation.ShareText);

        var cacheRead = Assert.Single(bar.Segments, s => s.Kind == TokenKind.CacheRead);
        Assert.Equal("5%", cacheRead.ShareText);
    }

    [Fact]
    public void A_kind_with_nothing_recorded_is_excluded_not_rendered_as_a_zero_width_slice()
    {
        var totals = RealTotals(input: 100, output: 0, cacheCreation: 0, cacheRead: 0, total: 100);

        var bar = TokenKindBarFormatter.BuildBar("Today", totals);

        Assert.True(bar.HasData);
        var segment = Assert.Single(bar.Segments);
        Assert.Equal(TokenKind.Input, segment.Kind);
    }

    [Fact]
    public void Breakdown_rows_carry_all_four_kinds_for_both_windows()
    {
        var today = RealTotals(input: 10, output: 20, cacheCreation: 5, cacheRead: 5, total: 40);
        var window31d = RealTotals(input: 100, output: 200, cacheCreation: 50, cacheRead: 50, total: 400);

        var rows = TokenKindBarFormatter.BuildBreakdownRows(today, window31d);

        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, r => r.Label == "Input" && r.TodayTokens == "10" && r.Window31dTokens == "100");
    }

    [Fact]
    public void Breakdown_rows_are_empty_when_neither_window_could_be_read()
    {
        var rows = TokenKindBarFormatter.BuildBreakdownRows(TokenKindTotals.Unavailable, TokenKindTotals.Unavailable);

        Assert.Empty(rows);
    }
}
