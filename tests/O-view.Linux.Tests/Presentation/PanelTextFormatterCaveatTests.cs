using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>This skin's own usage-tile caveat wording (OVI-165); cross-skin facts live in the golden-master harness.</summary>
public class PanelTextFormatterCaveatTests
{
    private static UsageStatistics Stats(HistoryCoverage coverage, UnpricedModels unpriced, long ttl, RateCardStamp rates) =>
        new(
            new TokenCount(1, UsageValueStatus.Real),
            new EstimatedUsd(1m, UsageValueStatus.Estimated),
            new TokenCount(1, UsageValueStatus.Real),
            new EstimatedUsd(1m, UsageValueStatus.Estimated),
            coverage)
        {
            UnpricedModels = unpriced,
            TtlUnrecordedCacheWritesWindow31d = new TokenCount(ttl, UsageValueStatus.Real),
            Rates = rates,
        };

    [Fact]
    public void CaveatJoinsEveryApplicableQualifierWithMiddleDots()
    {
        var stats = Stats(
            new HistoryCoverage(18, 31),
            new UnpricedModels(new[] { "m1" }),
            2_500,
            new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 6, 24), true));

        var caveat = PanelTextFormatter.Caveat(stats);

        Assert.Equal(4, caveat.Split(" · ").Length);
        Assert.Contains("2.5K", caveat);
    }

    [Fact]
    public void RateAgeNamesAUserFileSourceDifferentlyFromABundledOne()
    {
        var bundled = PanelTextFormatter.RateAge(new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 6, 24), true));
        var user = PanelTextFormatter.RateAge(new RateCardStamp(RateCardSource.UserFile, new DateOnly(2026, 6, 24), true));

        Assert.NotEqual(bundled, user);
        Assert.Contains("user file", user);
    }

    [Fact]
    public void TokenScopeCaveatIsNonEmpty() => Assert.NotEmpty(PanelTextFormatter.TokenScopeCaveat);
}
