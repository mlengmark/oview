using OView.Core.Models;

namespace OView.Core.Tests.Models;

public class DailyUsageSeriesTests
{
    [Fact]
    public void UnavailableSeriesCarriesNoDaysAndTheMinValueSentinelWindow()
    {
        var series = DailyUsageSeries.Unavailable;

        Assert.Equal(DateOnly.MinValue, series.FromLocalDate);
        Assert.Equal(DateOnly.MinValue, series.ToLocalDate);
        Assert.Empty(series.Days);
        Assert.Equal(UsageValueStatus.Unavailable, series.Status);
    }

    [Fact]
    public void AnAbsentDayIsAStatusNotAFabricatedZero()
    {
        var absentDay = new DailyUsagePoint(new DateOnly(2026, 10, 1), new TokenCount(null, UsageValueStatus.Unavailable));
        var idleDay = new DailyUsagePoint(new DateOnly(2026, 10, 2), new TokenCount(0, UsageValueStatus.Real));

        Assert.Null(absentDay.OutputTokens.Value);
        Assert.Equal(UsageValueStatus.Unavailable, absentDay.OutputTokens.Status);
        Assert.Equal(0, idleDay.OutputTokens.Value);
        Assert.Equal(UsageValueStatus.Real, idleDay.OutputTokens.Status);
    }
}
