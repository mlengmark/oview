using OView.Core.Models;

namespace OView.Core.Tests.Models;

public class TokenKindTotalsTests
{
    [Fact]
    public void UnavailableTotalsMarkEveryKindAndTheGrandTotalUnavailableRatherThanZero()
    {
        var totals = TokenKindTotals.Unavailable;

        Assert.Null(totals.Input.Tokens.Value);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Input.Tokens.Status);
        Assert.Null(totals.Output.Tokens.Value);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Output.Tokens.Status);
        Assert.Null(totals.CacheCreation.Tokens.Value);
        Assert.Equal(UsageValueStatus.Unavailable, totals.CacheCreation.Tokens.Status);
        Assert.Null(totals.CacheRead.Tokens.Value);
        Assert.Equal(UsageValueStatus.Unavailable, totals.CacheRead.Tokens.Status);
        Assert.Null(totals.Total.Value);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Total.Status);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Rates.Status);
        Assert.Equal(UsageValueStatus.Unavailable, totals.Status);
    }

    [Fact]
    public void TotalIsCarriedRatherThanDerivedSoASkinNeverSumsTheFourKindsItself()
    {
        var totals = new TokenKindTotals(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 1),
            new TokenKindAmount(new TokenCount(10, UsageValueStatus.Real), new EstimatedUsd(0.01m, UsageValueStatus.Estimated)),
            new TokenKindAmount(new TokenCount(20, UsageValueStatus.Real), new EstimatedUsd(0.02m, UsageValueStatus.Estimated)),
            new TokenKindAmount(new TokenCount(30, UsageValueStatus.Real), new EstimatedUsd(0.03m, UsageValueStatus.Estimated)),
            new TokenKindAmount(new TokenCount(40, UsageValueStatus.Real), new EstimatedUsd(0.04m, UsageValueStatus.Estimated)),
            new TokenCount(100, UsageValueStatus.Real),
            RateCardStamp.Unavailable,
            UsageValueStatus.Real);

        Assert.Equal(100, totals.Total.Value);
    }
}
