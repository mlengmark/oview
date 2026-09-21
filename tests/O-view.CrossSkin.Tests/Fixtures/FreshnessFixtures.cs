using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="FreshnessFixture"/>s (ADR-0003, OVI-29). Add a new
/// fixture as a new entry in <see cref="All"/>, following <c>GoldenMasterFixtures</c>'
/// existing pattern.
/// </summary>
public static class FreshnessFixtures
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 11, 12, 40, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastIngestAt = new(2026, 9, 11, 12, 30, 0, TimeSpan.Zero);

    private static UsageSnapshot MakeSnapshot(DataSourceKind kind) => new(
        kind,
        LastIngestAt,
        new UsagePercent(57, UsageValueStatus.Real),
        new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
        new UsagePercent(14, UsageValueStatus.Real),
        new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
        UsageLevel.Amber);

    /// <summary>An ordinary authoritative reading, aged 10 minutes — both skins must state its age.</summary>
    public static readonly FreshnessFixture LiveAgedReading = new(
        Name: "live-aged-reading",
        Snapshot: MakeSnapshot(DataSourceKind.Live),
        UtcNow: UtcNow,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("12:30"),
        });

    /// <summary>
    /// ADR-0001's confirmed source behaviour: a <see cref="DataSourceKind.Stale"/> reading at
    /// the same age states the same age as a <see cref="DataSourceKind.Live"/> one — the two
    /// tiers collapse in <c>Freshness</c> on purpose. Pinning the identical content fact on
    /// both fixtures proves neither skin invents a different age-reporting rule for
    /// <c>Stale</c>; the byte-for-byte collapse itself is proven per-skin in
    /// <c>PanelTextFormatterTests</c>.
    /// </summary>
    public static readonly FreshnessFixture StaleReadingAtTheSameAge = new(
        Name: "stale-reading-at-the-same-age",
        Snapshot: MakeSnapshot(DataSourceKind.Stale),
        UtcNow: UtcNow,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("12:30"),
        });

    /// <summary>A reading captured within the current clock minute states "now", not a stamp.</summary>
    public static readonly FreshnessFixture ReadingCapturedThisMinute = new(
        Name: "reading-captured-this-minute",
        Snapshot: MakeSnapshot(DataSourceKind.Live) with { LastIngestAt = UtcNow },
        UtcNow: UtcNow,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("now"),
        });

    /// <summary>A modelled reading gets the "Local estimate" framing both skins share.</summary>
    public static readonly FreshnessFixture EstimatedReading = new(
        Name: "estimated-reading",
        Snapshot: MakeSnapshot(DataSourceKind.Estimate),
        UtcNow: UtcNow,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("Local estimate"),
            ContentFact.Contains("12:30"),
        });

    /// <summary>
    /// No snapshot at all — neither skin may render a percentage or an age for it, even
    /// though the sentinel <see cref="UsageSnapshot.Unavailable"/>'s <c>LastIngestAt</c> is
    /// <see cref="DateTimeOffset.MinValue"/> rather than a real capture time.
    /// </summary>
    public static readonly FreshnessFixture NoSnapshotAtAll = new(
        Name: "no-snapshot-at-all",
        Snapshot: UsageSnapshot.Unavailable,
        UtcNow: UtcNow,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact("does not render a percentage", rendered => !rendered.Contains('%')),
            new ContentFact("does not render year 0001 from the MinValue sentinel", rendered => !rendered.Contains("0001")),
        });

    public static IReadOnlyList<FreshnessFixture> All { get; } = new[]
    {
        LiveAgedReading,
        StaleReadingAtTheSameAge,
        ReadingCapturedThisMinute,
        EstimatedReading,
        NoSnapshotAtAll,
    };
}
