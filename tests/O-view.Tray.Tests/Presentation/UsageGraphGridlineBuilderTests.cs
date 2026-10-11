using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves <see cref="UsageGraphGridlineBuilder"/> (ADR-0008 D9e, gate G7 parity slice P14): a
/// <see cref="WeeklyResetBoundaryKind.MondayFallback"/> boundary is always local midnight and so
/// always lands exactly on a column edge (<see cref="UsageGraphGridline.FractionWithinColumn"/>
/// <c>0</c>), while a plan-defined boundary carries whatever time of day the plan actually resets
/// at and lands wherever that really is inside the column — never snapped to the edge. This is
/// the "ResetBoundaryFixture" the task's own "Done when" names.
/// </summary>
public sealed class UsageGraphGridlineBuilderTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static IReadOnlyList<UsageGraphColumn> ColumnsOf(params DateOnly[] dates) => dates
        .Select(d => new UsageGraphColumn(d, IsBlank: false, HeightFraction: 0.5, Intensity: 0.5, DateLabel: "x", new TokenCount(1, UsageValueStatus.Real)))
        .ToList();

    [Fact]
    public void MondayFallbackBoundaryLandsExactlyOnTheColumnEdge()
    {
        var day = new DateOnly(2026, 9, 7);
        var columns = ColumnsOf(day);
        var boundaries = new WeeklyResetBoundaries(
            day, day,
            new[] { new WeeklyResetBoundary(new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), WeeklyResetBoundaryKind.MondayFallback) },
            UsageValueStatus.Real);

        var gridlines = UsageGraphGridlineBuilder.Build(columns, boundaries, Utc);

        var gridline = Assert.Single(gridlines);
        Assert.Equal(0, gridline.ColumnIndex);
        Assert.Equal(0.0, gridline.FractionWithinColumn, precision: 10);
    }

    [Fact]
    public void PlanDefinedBoundaryLandsInsideTheColumnAtItsTrueFractionalPosition()
    {
        var day = new DateOnly(2026, 9, 7);
        var columns = ColumnsOf(day);
        // 14:00 UTC on a 24-hour local day is 14/24 of the way through it.
        var boundaries = new WeeklyResetBoundaries(
            day, day,
            new[] { new WeeklyResetBoundary(new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.Zero), WeeklyResetBoundaryKind.Observed) },
            UsageValueStatus.Real);

        var gridlines = UsageGraphGridlineBuilder.Build(columns, boundaries, Utc);

        var gridline = Assert.Single(gridlines);
        Assert.Equal(0, gridline.ColumnIndex);
        Assert.Equal(14.0 / 24.0, gridline.FractionWithinColumn, precision: 10);
        Assert.NotEqual(0.0, gridline.FractionWithinColumn);
    }

    [Fact]
    public void BoundaryOutsideTheWindowIsNotDrawn()
    {
        var windowDay = new DateOnly(2026, 9, 7);
        var outsideDay = new DateOnly(2026, 8, 1);
        var columns = ColumnsOf(windowDay);
        var boundaries = new WeeklyResetBoundaries(
            outsideDay, outsideDay,
            new[] { new WeeklyResetBoundary(new DateTimeOffset(outsideDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), WeeklyResetBoundaryKind.MondayFallback) },
            UsageValueStatus.Real);

        var gridlines = UsageGraphGridlineBuilder.Build(columns, boundaries, Utc);

        Assert.Empty(gridlines);
    }

    [Fact]
    public void UnavailableResetBoundariesProduceNoGridlines()
    {
        var gridlines = UsageGraphGridlineBuilder.Build(ColumnsOf(new DateOnly(2026, 9, 7)), WeeklyResetBoundaries.Unavailable, Utc);

        Assert.Empty(gridlines);
    }

    [Fact]
    public void MondayFallbackAndPlanDefinedKindsAreDistinguishableOnHover()
    {
        var day1 = new DateOnly(2026, 9, 7);
        var day2 = new DateOnly(2026, 9, 8);
        var columns = ColumnsOf(day1, day2);
        var boundaries = new WeeklyResetBoundaries(
            day1, day2,
            new[]
            {
                new WeeklyResetBoundary(new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), WeeklyResetBoundaryKind.MondayFallback),
                new WeeklyResetBoundary(new DateTimeOffset(2026, 9, 8, 14, 0, 0, TimeSpan.Zero), WeeklyResetBoundaryKind.Observed),
            },
            UsageValueStatus.Real);

        var gridlines = UsageGraphGridlineBuilder.Build(columns, boundaries, Utc);

        Assert.Equal(2, gridlines.Count);
        Assert.NotEqual(gridlines[0].HoverCaption, gridlines[1].HoverCaption);
        Assert.Contains("fallback", gridlines[0].HoverCaption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plan", gridlines[1].HoverCaption, StringComparison.OrdinalIgnoreCase);
    }
}
