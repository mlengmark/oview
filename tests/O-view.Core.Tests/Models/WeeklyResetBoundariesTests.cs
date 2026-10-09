using OView.Core.Models;

namespace OView.Core.Tests.Models;

public class WeeklyResetBoundariesTests
{
    [Fact]
    public void UnavailableBoundariesCarryNoBoundaryAndTheMinValueSentinelWindow()
    {
        var boundaries = WeeklyResetBoundaries.Unavailable;

        Assert.Equal(DateOnly.MinValue, boundaries.FromLocalDate);
        Assert.Equal(DateOnly.MinValue, boundaries.ToLocalDate);
        Assert.Empty(boundaries.Boundaries);
        Assert.Equal(UsageValueStatus.Unavailable, boundaries.Status);
    }

    [Fact]
    public void ThreeDistinctKindsExistSoDerivedIsNeverConfusedWithObserved()
    {
        var values = Enum.GetValues<WeeklyResetBoundaryKind>();

        Assert.Contains(WeeklyResetBoundaryKind.Observed, values);
        Assert.Contains(WeeklyResetBoundaryKind.DerivedFromObserved, values);
        Assert.Contains(WeeklyResetBoundaryKind.MondayFallback, values);
        Assert.Equal(values.Distinct().Count(), values.Length);
    }

    [Fact]
    public void EachBoundaryCarriesItsOwnOffsetRatherThanOneWindowWideOffset()
    {
        var beforeDst = new WeeklyResetBoundary(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(1)), WeeklyResetBoundaryKind.Observed);
        var afterDst = new WeeklyResetBoundary(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.FromHours(2)), WeeklyResetBoundaryKind.DerivedFromObserved);

        Assert.NotEqual(beforeDst.Instant.Offset, afterDst.Instant.Offset);
    }
}
