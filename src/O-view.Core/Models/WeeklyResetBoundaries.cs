namespace OView.Core.Models;

/// <summary>
/// The weekly-reset gridlines inside the detail window's 31-day graph (ADR-0008 D9e), derived
/// at query time from Core's stored reset anchor — nothing new is persisted.
/// </summary>
/// <param name="FromLocalDate">The first local day the window covers.</param>
/// <param name="ToLocalDate">The last local day the window covers.</param>
/// <param name="Boundaries">Every reset instant inside the window, ascending.</param>
/// <param name="Status">Whether this list is trustworthy at all. See
/// <see cref="ModelUsageBreakdown"/>'s remarks for the same real-empty-vs-unavailable
/// distinction — an empty, <see cref="UsageValueStatus.Real"/> list means the window genuinely
/// holds no boundary.</param>
public sealed record WeeklyResetBoundaries(
    DateOnly FromLocalDate,
    DateOnly ToLocalDate,
    IReadOnlyList<WeeklyResetBoundary> Boundaries,
    UsageValueStatus Status)
{
    /// <summary>
    /// The canonical "no data" boundary list. <see cref="FromLocalDate"/> and
    /// <see cref="ToLocalDate"/> are <see cref="DateOnly.MinValue"/> here as an explicit
    /// "never" sentinel, not a fabricated window (the same pattern
    /// <see cref="ModelUsageBreakdown.Unavailable"/> uses). No skin reads these two dates for
    /// an <see cref="UsageValueStatus.Unavailable"/> list.
    /// </summary>
    public static WeeklyResetBoundaries Unavailable { get; } = new(
        DateOnly.MinValue,
        DateOnly.MinValue,
        Array.Empty<WeeklyResetBoundary>(),
        UsageValueStatus.Unavailable);
}
