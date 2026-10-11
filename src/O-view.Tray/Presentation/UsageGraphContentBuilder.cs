using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// One column of the detail window's 31-day usage graph (ADR-0008 D9e/D10b, gate G7 parity
/// slice P13). <see cref="IsBlank"/> is the authoritative "no bar" signal — it is not inferred
/// from <see cref="HeightFraction"/> being zero, because a recorded, genuinely idle day also has
/// a zero <see cref="HeightFraction"/> and must still draw as a (very short) real bar, not a
/// blank column (<see cref="DailyUsagePoint"/>'s own remarks).
/// </summary>
/// <param name="LocalDate">The day this column represents.</param>
/// <param name="IsBlank">True when <see cref="DailyUsagePoint.OutputTokens"/>'
/// <see cref="UsageValueStatus"/> is <see cref="UsageValueStatus.Unavailable"/> — this day
/// predates recorded history. No bar is drawn, but the date label still is.</param>
/// <param name="HeightFraction">The bar's height, <c>0..1</c> of the 31-day window's own
/// maximum output-token day — the same single scale every column shares, so columns stay
/// comparable against one another.</param>
/// <param name="Intensity">The bar's colour ramp position, <c>0..1</c> of its own week's maximum
/// (W3, OVI-594: a day containing a weekly boundary is bucketed by which side holds the majority
/// of the day — approximated here, as the source does, by testing the day's own midpoint).
/// Meaningless (and left at <c>0</c>) for a blank or zero-token column.</param>
/// <param name="DateLabel">The short date label drawn under every column, blank or not.</param>
/// <param name="OutputTokens">Carried through verbatim from <see cref="DailyUsagePoint.OutputTokens"/>
/// for the hover card's own figure — never re-derived from <see cref="HeightFraction"/>, which is
/// already a lossy fraction of the window's own maximum.</param>
public sealed record UsageGraphColumn(
    DateOnly LocalDate,
    bool IsBlank,
    double HeightFraction,
    double Intensity,
    string DateLabel,
    TokenCount OutputTokens);

/// <summary>
/// Turns <see cref="UsageDetail.History"/> and <see cref="UsageDetail.ResetBoundaries"/> into the
/// 31 columns the detail window's graph draws (ADR-0008 D10b, gate G7 parity slice P13): bar
/// heights, the per-week intensity ramp, and the blank-column rule are this skin's own
/// presentation decision over the one output-token figure per day Core already handed over — nothing
/// here sums, averages, or re-buckets a vendor-derived row (D9c). Gridlines over the same
/// <see cref="WeeklyResetBoundaries"/> are <see cref="UsageGraphGridlineBuilder"/>'s own, separate
/// slice P14 presentation decision — this builder's columns are drawn first and the gridlines on
/// top, but neither builder reads the other's output.
/// </summary>
public static class UsageGraphContentBuilder
{
    public static IReadOnlyList<UsageGraphColumn> Build(
        DailyUsageSeries history, WeeklyResetBoundaries resetBoundaries, TimeZoneInfo zone)
    {
        if (history.Status != UsageValueStatus.Real || history.Days.Count == 0)
        {
            return Array.Empty<UsageGraphColumn>();
        }

        var days = history.Days;
        var globalMax = Math.Max(1, days
            .Where(d => d.OutputTokens.Status == UsageValueStatus.Real)
            .Select(d => d.OutputTokens.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max());

        var boundaries = resetBoundaries.Status == UsageValueStatus.Real
            ? resetBoundaries.Boundaries
            : Array.Empty<WeeklyResetBoundary>();

        var weekIndexByDate = days.ToDictionary(d => d.LocalDate, d => WeekIndexOf(d.LocalDate, zone, boundaries));

        var weekMax = days
            .Where(d => d.OutputTokens.Status == UsageValueStatus.Real)
            .GroupBy(d => weekIndexByDate[d.LocalDate])
            .ToDictionary(g => g.Key, g => Math.Max(1, g.Max(d => d.OutputTokens.Value ?? 0)));

        var columns = new List<UsageGraphColumn>(days.Count);
        foreach (var day in days)
        {
            var isBlank = day.OutputTokens.Status != UsageValueStatus.Real;
            var tokens = day.OutputTokens.Value ?? 0;
            var heightFraction = isBlank ? 0.0 : tokens / (double)globalMax;
            var intensity = isBlank || tokens == 0
                ? 0.0
                : tokens / (double)weekMax[weekIndexByDate[day.LocalDate]];

            columns.Add(new UsageGraphColumn(
                day.LocalDate, isBlank, heightFraction, intensity, FormatDateLabel(day.LocalDate), day.OutputTokens));
        }

        return columns;
    }

    /// <summary>
    /// Which week band <paramref name="date"/> belongs to, counting the boundaries at or before
    /// its own midpoint (W3, OVI-594) — the same approximation the source's own <c>PlanWeeks</c>
    /// uses for a day a boundary crosses. The midpoint is derived from this day's own real UTC
    /// span, never assumed to be 24 hours: a DST transition inside the window makes the local day
    /// 23 or 25 hours long, and halving a wrong duration misplaces the midpoint by up to an hour —
    /// enough to flip which week a boundary-crossing day is bucketed into.
    /// </summary>
    private static int WeekIndexOf(DateOnly date, TimeZoneInfo zone, IReadOnlyList<WeeklyResetBoundary> boundaries)
    {
        var start = LocalDayStartUtc(date, zone);
        var end = LocalDayStartUtc(date.AddDays(1), zone);
        var midpoint = start + (end - start) / 2;
        return boundaries.Count(b => b.Instant <= midpoint);
    }

    /// <summary>Shared with <see cref="UsageGraphGridlineBuilder"/> (slice P14) — both need the
    /// same real local-day span, not an assumed 24 hours, for the same DST reason.</summary>
    internal static DateTimeOffset LocalDayStartUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static string FormatDateLabel(DateOnly date) =>
        date.ToString("d MMM", CultureInfo.InvariantCulture);
}
