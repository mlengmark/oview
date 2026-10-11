using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// One weekly-reset gridline drawn over the 31-day usage graph (ADR-0008 D9e, gate G7 parity
/// slice P14). <see cref="FractionWithinColumn"/> is the boundary's true position inside the day
/// it falls in, <c>0..1</c> — never snapped to the column edge: a <see cref="WeeklyResetBoundaryKind.MondayFallback"/>
/// boundary is always local midnight and lands at <c>0</c>, but a plan-defined (<see cref="WeeklyResetBoundaryKind.Observed"/>
/// or <see cref="WeeklyResetBoundaryKind.DerivedFromObserved"/>) boundary carries whatever time of
/// day the plan actually resets at and lands wherever that really is.
/// </summary>
/// <param name="ColumnIndex">Which <see cref="UsageGraphColumn"/> (by position in the same list
/// <see cref="UsageGraphContentBuilder.Build"/> returned) this gridline falls inside.</param>
/// <param name="HoverCaption">Names the boundary's kind and local instant for the hover card —
/// the only way a Monday-fallback line is told apart from a plan-defined one, since both are the
/// same amber dotted stroke.</param>
public sealed record UsageGraphGridline(
    int ColumnIndex,
    double FractionWithinColumn,
    WeeklyResetBoundaryKind Kind,
    string HoverCaption);

/// <summary>
/// Turns <see cref="UsageDetail.ResetBoundaries"/> into the gridlines the graph draws last, on
/// top of <see cref="UsageGraphContentBuilder"/>'s own columns (ADR-0008 D9e, gate G7 parity
/// slice P14) — a separate presentation decision over the same boundary list, not a change to
/// how those columns are built or coloured (P13's own scope).
/// </summary>
public static class UsageGraphGridlineBuilder
{
    public static IReadOnlyList<UsageGraphGridline> Build(
        IReadOnlyList<UsageGraphColumn> columns, WeeklyResetBoundaries resetBoundaries, TimeZoneInfo zone)
    {
        if (columns.Count == 0 || resetBoundaries.Status != UsageValueStatus.Real)
        {
            return Array.Empty<UsageGraphGridline>();
        }

        var columnIndexByDate = new Dictionary<DateOnly, int>(columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            columnIndexByDate[columns[i].LocalDate] = i;
        }

        var gridlines = new List<UsageGraphGridline>(resetBoundaries.Boundaries.Count);
        foreach (var boundary in resetBoundaries.Boundaries)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(boundary.Instant, zone).DateTime);
            if (!columnIndexByDate.TryGetValue(localDate, out var columnIndex))
            {
                // The boundary falls outside the 31-day window this graph draws — no column to
                // anchor it to.
                continue;
            }

            var dayStart = UsageGraphContentBuilder.LocalDayStartUtc(localDate, zone);
            var dayEnd = UsageGraphContentBuilder.LocalDayStartUtc(localDate.AddDays(1), zone);
            var fraction = (boundary.Instant - dayStart) / (dayEnd - dayStart);

            gridlines.Add(new UsageGraphGridline(
                columnIndex, Math.Clamp(fraction, 0.0, 1.0), boundary.Kind, HoverCaption(boundary, zone)));
        }

        return gridlines;
    }

    /// <summary>
    /// Names the boundary's kind in plain words — never just the enum — and its local instant,
    /// the same <c>ddd … HH:mm</c> convention <see cref="PanelTextFormatter.WeeklyReset"/> uses
    /// elsewhere in this window. <see cref="WeeklyResetBoundaryKind.Observed"/> and
    /// <see cref="WeeklyResetBoundaryKind.DerivedFromObserved"/> read the same to a user — both
    /// are "plan-defined", the distinction the task asks this hover to carry is Monday-fallback
    /// vs. not, not whether this particular instance was itself observed.
    /// </summary>
    private static string HoverCaption(WeeklyResetBoundary boundary, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(boundary.Instant, zone);
        var kindText = boundary.Kind == WeeklyResetBoundaryKind.MondayFallback
            ? "Weekly reset (calendar-week fallback, no plan data)"
            : "Weekly reset (plan-defined)";

        return string.Create(CultureInfo.InvariantCulture, $"{kindText} — {local:ddd d MMM, HH:mm}");
    }
}
