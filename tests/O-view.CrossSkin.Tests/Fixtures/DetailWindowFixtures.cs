using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="DetailWindowFixture"/>s (ADR-0003, ADR-0008 D2, OVI-324).
/// Covers what <see cref="UsageSnapshot"/> carries today: ordinary live usage with extra usage
/// known on, a stale reading with extra usage known off, a jsonl-fallback reading with extra
/// usage unknown, and the fully unavailable "no data" case. Per Chief Gary II's OVI-325
/// scoping decision, the per-model split and the 31-day statistics ADR-0008 D2 also requires
/// the detail window to show are <b>not</b> covered here — <see cref="UsageSnapshot"/> does
/// not carry them yet, and widening it is a separate Core/shell contract task, not this
/// fixtures-only slice's to invent. <c>TODO(detail-window-per-model-and-statistics)</c>: add a
/// fixture family for those two facts once that widening lands.
/// </summary>
public static class DetailWindowFixtures
{
    private static readonly DateTimeOffset SessionReset = new(2026, 9, 8, 20, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeeklyReset = new(2026, 9, 7, 23, 0, 0, TimeSpan.Zero); // a Monday

    /// <summary>
    /// Ordinary live usage, extra usage known on, fetched the same day as ingest. Pins the
    /// percentages, both reset instants (as <see cref="GoldenMasterFixture"/> already does for
    /// the tooltip's slice of this same snapshot shape), and the extra-usage reading's own
    /// fetched-at time.
    /// </summary>
    public static readonly DetailWindowFixture OrdinaryLiveReadingExtraUsageOn = new(
        Name: "ordinary-live-reading-extra-usage-on",
        Snapshot: new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(63, UsageValueStatus.Real),
            new UsageInstant(SessionReset, UsageValueStatus.Real),
            new UsagePercent(22, UsageValueStatus.Real),
            new UsageInstant(WeeklyReset, UsageValueStatus.Real),
            UsageLevel.Amber)
        {
            ExtraUsage = new ExtraUsageReading(ExtraUsageState.Enabled, new DateTimeOffset(2026, 9, 8, 20, 40, 0, TimeSpan.Zero)),
        },
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("63%"),
            ContentFact.Contains("22%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            ContentFact.Contains("20:40"),
        });

    /// <summary>
    /// A stale reading, extra usage known off, fetched on an earlier day than the ingest —
    /// mirrors <c>OffPlanFixtures</c>' earlier-day convention (a month-abbreviation stamp, not
    /// a bare time, for a reading that is not from today).
    /// </summary>
    public static readonly DetailWindowFixture StaleReadingExtraUsageOff = new(
        Name: "stale-reading-extra-usage-off",
        Snapshot: new UsageSnapshot(
            DataSourceKind.Stale,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(40, UsageValueStatus.Real),
            new UsageInstant(SessionReset, UsageValueStatus.Real),
            new UsagePercent(10, UsageValueStatus.Real),
            new UsageInstant(WeeklyReset, UsageValueStatus.Real),
            UsageLevel.Green)
        {
            ExtraUsage = new ExtraUsageReading(ExtraUsageState.Disabled, new DateTimeOffset(2026, 9, 4, 10, 15, 0, TimeSpan.Zero)),
        },
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("40%"),
            ContentFact.Contains("10%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            new ContentFact(
                "names the earlier-day stamp, not a bare time, for a reading from a prior day",
                rendered => rendered.Contains("Sep", StringComparison.Ordinal)),
            new ContentFact(
                "states extra usage is switched off",
                rendered => rendered.Contains("off", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// A jsonl-fallback reading where Claude Code's own cache did not say whether extra usage
    /// is on or off (<see cref="UsageSnapshot.ExtraUsage"/> is <c>null</c>) — the "cache did
    /// not say" case ADR-0001 (OVI-168) distinguishes from a fabricated <c>Disabled</c>.
    /// </summary>
    public static readonly DetailWindowFixture JsonlFallbackReadingExtraUsageUnknown = new(
        Name: "jsonl-fallback-reading-extra-usage-unknown",
        Snapshot: new UsageSnapshot(
            DataSourceKind.JsonlFallback,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(5, UsageValueStatus.Real),
            new UsageInstant(SessionReset, UsageValueStatus.Real),
            new UsagePercent(1, UsageValueStatus.Real),
            new UsageInstant(WeeklyReset, UsageValueStatus.Real),
            UsageLevel.Green),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("5%"),
            ContentFact.Contains("1%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            new ContentFact(
                "never claims extra usage is definitely on or off when the cache did not say",
                rendered => !rendered.Contains("Enabled", StringComparison.Ordinal)
                    && !rendered.Contains("Disabled", StringComparison.Ordinal)),
        });

    /// <summary>
    /// The fully unavailable snapshot — the "no data" explanation D2 requires. Never a
    /// fabricated zero, same rule as <see cref="UsageStatisticsFixtures.Unavailable"/>.
    /// </summary>
    public static readonly DetailWindowFixture UnavailableNoDataExplanation = new(
        Name: "unavailable-no-data-explanation",
        Snapshot: UsageSnapshot.Unavailable,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "never renders a fabricated 0% session or weekly figure",
                rendered => !rendered.Contains("0%", StringComparison.Ordinal)),
            new ContentFact(
                "never claims extra usage is on or off when there is no data at all",
                rendered => !rendered.Contains("Enabled", StringComparison.Ordinal)
                    && !rendered.Contains("Disabled", StringComparison.Ordinal)),
        });

    public static IReadOnlyList<DetailWindowFixture> All { get; } = new[]
    {
        OrdinaryLiveReadingExtraUsageOn,
        StaleReadingExtraUsageOff,
        JsonlFallbackReadingExtraUsageUnknown,
        UnavailableNoDataExplanation,
    };
}
