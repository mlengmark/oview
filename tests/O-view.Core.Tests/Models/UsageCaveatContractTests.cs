using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>ADR-0001's 2026-09-23 (OVI-100) usage-caveat fields: defaults are unavailable, never fabricated.</summary>
public class UsageCaveatContractTests
{
    [Fact]
    public void CaveatFieldsDefaultToUnavailable()
    {
        var stats = UsageStatistics.Unavailable;

        Assert.Equal(UsageValueStatus.Unavailable, stats.UnpricedModels.Status);
        Assert.Equal(UsageValueStatus.Unavailable, stats.TtlUnrecordedCacheWritesWindow31d.Status);
        Assert.Equal(UsageValueStatus.Unavailable, stats.Rates.Status);
    }

    [Fact]
    public void UnavailableRateStampCarriesNoSourceOrDate()
    {
        Assert.Null(RateCardStamp.Unavailable.Source);
        Assert.Null(RateCardStamp.Unavailable.AsOf);
        Assert.False(RateCardStamp.Unavailable.IsStale);
    }

    [Fact]
    public void RealStampCarriesItsFields()
    {
        var stamp = new RateCardStamp(RateCardSource.UserFile, new DateOnly(2026, 1, 2), true);

        Assert.Equal(UsageValueStatus.Real, stamp.Status);
        Assert.Equal(RateCardSource.UserFile, stamp.Source);
        Assert.Equal(new DateOnly(2026, 1, 2), stamp.AsOf);
        Assert.True(stamp.IsStale);
    }

    [Fact]
    public void NoUnpricedModelsIsRealAndEmptyNotUnavailable()
    {
        Assert.Equal(UsageValueStatus.Real, UnpricedModels.None.Status);
        Assert.Empty(UnpricedModels.None.ModelIds);
        Assert.Empty(UnpricedModels.Unavailable.ModelIds);
    }
}
