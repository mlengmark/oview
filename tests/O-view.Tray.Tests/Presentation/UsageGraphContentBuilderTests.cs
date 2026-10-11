using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves <see cref="UsageGraphContentBuilder"/> (ADR-0008 D10b, gate G7 parity slice P13): the
/// absent-day rule (blank column vs. recorded-zero bar), the per-week intensity ramp (W3,
/// OVI-594), that every day gets a date label even when blank, and that the week bucketing is
/// derived from each local day's own real span rather than an assumed 24 hours — the DST case.
/// </summary>
public sealed class UsageGraphContentBuilderTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static DailyUsageSeries SeriesOf(params (DateOnly Date, long? Tokens)[] days)
    {
        var points = days
            .Select(d => new DailyUsagePoint(
                d.Date,
                d.Tokens is { } value
                    ? new TokenCount(value, UsageValueStatus.Real)
                    : new TokenCount(null, UsageValueStatus.Unavailable)))
            .ToList();

        return new DailyUsageSeries(
            points[0].LocalDate, points[^1].LocalDate, points, new HistoryCoverage(points.Count, points.Count), UsageValueStatus.Real);
    }

    private static WeeklyResetBoundaries BoundariesOf(DateOnly from, DateOnly to, params WeeklyResetBoundary[] boundaries) =>
        new(from, to, boundaries, UsageValueStatus.Real);

    [Fact]
    public void UnavailableHistoryProducesNoColumns()
    {
        var columns = UsageGraphContentBuilder.Build(DailyUsageSeries.Unavailable, WeeklyResetBoundaries.Unavailable, Utc);

        Assert.Empty(columns);
    }

    [Fact]
    public void ADayBeforeInstallIsBlankNotZeroHeight()
    {
        var series = SeriesOf(
            (new DateOnly(2026, 9, 1), null),      // predates recorded history
            (new DateOnly(2026, 9, 2), 0));        // recorded, genuinely idle

        var columns = UsageGraphContentBuilder.Build(series, WeeklyResetBoundaries.Unavailable, Utc);

        var blankDay = columns[0];
        var zeroDay = columns[1];

        Assert.True(blankDay.IsBlank, "a day predating recorded history must be blank, not a zero bar");
        Assert.False(zeroDay.IsBlank, "a recorded, idle day must not read the same as a blank, pre-install one");
        Assert.Equal(0.0, blankDay.HeightFraction);
        Assert.Equal(0.0, zeroDay.HeightFraction);
    }

    [Fact]
    public void EveryColumnCarriesADateLabelIncludingBlankOnes()
    {
        var series = SeriesOf(
            (new DateOnly(2026, 9, 1), null),
            (new DateOnly(2026, 9, 2), 100));

        var columns = UsageGraphContentBuilder.Build(series, WeeklyResetBoundaries.Unavailable, Utc);

        Assert.All(columns, c => Assert.False(string.IsNullOrEmpty(c.DateLabel)));
    }

    [Fact]
    public void HeightFractionIsRelativeToTheWholeWindowsOwnMaximum()
    {
        var series = SeriesOf(
            (new DateOnly(2026, 9, 1), 50),
            (new DateOnly(2026, 9, 2), 200));

        var columns = UsageGraphContentBuilder.Build(series, WeeklyResetBoundaries.Unavailable, Utc);

        Assert.Equal(0.25, columns[0].HeightFraction, precision: 10);
        Assert.Equal(1.0, columns[1].HeightFraction, precision: 10);
    }

    /// <summary>
    /// The colour ramp is normalised per week (W3, OVI-594), not across the whole 31-day window:
    /// two days with the same raw token count get different intensities when their own weeks'
    /// maxima differ, even though their <see cref="UsageGraphColumn.HeightFraction"/> (the whole
    /// window's own scale) is identical.
    /// </summary>
    [Fact]
    public void IntensityIsNormalisedWithinEachDaysOwnWeekNotTheWholeWindow()
    {
        var week1Day = new DateOnly(2026, 9, 1);    // Tuesday
        var week1Peak = new DateOnly(2026, 9, 2);   // Wednesday, same week, higher peak
        var week2Day = new DateOnly(2026, 9, 8);    // next week, same raw tokens as week1Day
        var week2Peak = new DateOnly(2026, 9, 9);   // next week, much higher peak

        var series = SeriesOf(
            (week1Day, 100),
            (week1Peak, 200),
            (week2Day, 100),
            (week2Peak, 1000));

        // One boundary between the two weeks, at local midnight 2026-09-05 — the only
        // instant between 09-02 and 09-08 in this fixture, so it unambiguously separates them.
        var boundary = new WeeklyResetBoundary(
            new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero), WeeklyResetBoundaryKind.MondayFallback);
        var boundaries = BoundariesOf(week1Day, week2Peak, boundary);

        var columns = UsageGraphContentBuilder.Build(series, boundaries, Utc);

        var week1DayColumn = columns.Single(c => c.LocalDate == week1Day);
        var week2DayColumn = columns.Single(c => c.LocalDate == week2Day);

        Assert.Equal(0.5, week1DayColumn.Intensity, precision: 10);   // 100 / week1's own peak of 200
        Assert.Equal(0.1, week2DayColumn.Intensity, precision: 10);   // 100 / week2's own peak of 1000
        Assert.NotEqual(week1DayColumn.Intensity, week2DayColumn.Intensity);

        // Confirms both days share the same whole-window scale despite their different weeks.
        Assert.Equal(week1DayColumn.HeightFraction, week2DayColumn.HeightFraction, precision: 10);
    }

    /// <summary>
    /// A local day is not always 24 hours — a DST transition inside the window makes it 23 or 25
    /// (per the task: "DST days are 23 or 25 hours — do not assume 24h days"). This pins the week
    /// bucketing against a boundary placed strictly between the DST-correct midpoint of a
    /// 23-hour spring-forward day (16:30Z) and the midpoint a naive 24h/2 assumption would have
    /// produced (17:00Z): an implementation using the correct midpoint assigns the day to the
    /// later week (the boundary is still ahead of it); an implementation that naively assumed 24
    /// hours would assign it to the earlier week instead (the wrong midpoint already passed).
    /// </summary>
    [Fact]
    public void NaiveTwentyFourHourMidpointWouldDisagreeWithTheDstCorrectOne()
    {
        var zone = CreateDstZone();
        var springForwardDay = new DateOnly(2026, 3, 8);     // this test zone's 23-hour day
        var earlierWeekPeak = new DateOnly(2026, 3, 1);      // unambiguously the week before, both ways
        var laterWeekPeak = new DateOnly(2026, 4, 1);        // unambiguously the week after, both ways

        // Deliberately different peaks either side, so which week springForwardDay lands in is
        // observable in its own computed intensity rather than cancelling out by coincidence.
        var series = SeriesOf(
            (earlierWeekPeak, 500),
            (springForwardDay, 50),
            (laterWeekPeak, 200));

        // UTC start of 2026-03-08 local (offset -5, before the transition): 05:00Z.
        // UTC start of 2026-03-09 local (offset -4, after the transition):  04:00Z next day.
        // Real elapsed span: 23 hours, so the DST-correct midpoint is 11h30m after the start:
        // 16:30Z. A naive 24h/2 assumption from the same 05:00Z start would instead land on
        // 17:00Z. A probe boundary placed between the two (16:45Z) therefore resolves to a
        // different week depending on which midpoint an implementation actually used.
        var dstCorrectMidpoint = new DateTimeOffset(2026, 3, 8, 16, 30, 0, TimeSpan.Zero);
        var naiveMidpoint = new DateTimeOffset(2026, 3, 8, 17, 0, 0, TimeSpan.Zero);
        Assert.NotEqual(dstCorrectMidpoint, naiveMidpoint);
        var probe = dstCorrectMidpoint.AddMinutes(15);

        var boundaries = BoundariesOf(earlierWeekPeak, laterWeekPeak,
            new WeeklyResetBoundary(probe, WeeklyResetBoundaryKind.MondayFallback));

        var columns = UsageGraphContentBuilder.Build(series, boundaries, zone);
        var springForwardColumn = columns.Single(c => c.LocalDate == springForwardDay);

        // probe (16:45Z) is AFTER the DST-correct midpoint (16:30Z), so it does not count toward
        // springForwardDay's own bucket -> week index 0, grouped with earlierWeekPeak (500) ->
        // intensity 50/500 = 0.1. A naive implementation using the 17:00Z midpoint would instead
        // read probe (16:45Z) as BEFORE it, counting the boundary and placing springForwardDay in
        // week index 1 with laterWeekPeak (200) instead -> intensity 50/200 = 0.25. Pinning 0.1
        // here fails loudly if the implementation regresses to the naive midpoint.
        Assert.Equal(0.1, springForwardColumn.Intensity, precision: 10);
    }

    /// <summary>
    /// A synthetic zone with a US-shaped DST rule effective only around 2026, so this test is
    /// deterministic regardless of which OS tz database (or none) the test runner has — never
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>. Mirrors
    /// <c>LedgerUsageStatisticsSourceTests.CreateDstZone</c> in <c>O-view.Core.Tests</c>.
    /// </summary>
    private static TimeZoneInfo CreateDstZone()
    {
        var toDaylight = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 8);
        var toStandard = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            toDaylight,
            toStandard);

        return TimeZoneInfo.CreateCustomTimeZone(
            "OView.Tests.Dst",
            TimeSpan.FromHours(-5),
            "Test DST zone",
            "Test standard time",
            "Test daylight time",
            new[] { rule });
    }
}
