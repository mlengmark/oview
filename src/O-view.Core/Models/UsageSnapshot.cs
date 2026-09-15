namespace OView.Core.Models;

/// <summary>
/// The subset of the Core-to-skin data contract (ADR-0001) that tooltip presentation
/// needs: session and weekly utilization, their reset instants, the threshold band, and
/// the confidence tier they were produced under. Every value states its own trust status
/// rather than requiring a skin to guess it.
///
/// <para>By contract, this type carries no pre-built sentence, no field separator, no
/// locale-bound date/time string, and no platform-imposed length or pixel limit. Wording,
/// formatting, and any length cap belong to whichever skin renders this snapshot
/// (ADR-0001, ADR-0002) — never to this type.</para>
/// </summary>
public sealed record UsageSnapshot(
    DataSourceKind DataSourceKind,
    DateTimeOffset LastIngestAt,
    UsagePercent SessionUtilizationPercent,
    UsageInstant SessionResetAt,
    UsagePercent WeeklyUtilizationPercent,
    UsageInstant WeeklyResetAt,
    UsageLevel UsageLevel)
{
    /// <summary>
    /// The canonical "no data" snapshot — every value unavailable, not zero.
    /// <see cref="LastIngestAt"/> is <see cref="DateTimeOffset.MinValue"/> here as an
    /// explicit "never" sentinel, not a fabricated recent timestamp: no ingest has ever
    /// produced this snapshot, so there is no real capture time to report (ADR-0001's
    /// "never fabricate a number" rule). No skin reads this value for an
    /// <see cref="OView.Core.Models.DataSourceKind.Unavailable"/> snapshot — the same
    /// pattern already used by <see cref="UsageLevel"/> below, a non-nullable field given a
    /// documented sentinel rather than a paired status flag.
    /// </summary>
    public static UsageSnapshot Unavailable { get; } = new(
        DataSourceKind.Unavailable,
        DateTimeOffset.MinValue,
        new UsagePercent(null, UsageValueStatus.Unavailable),
        new UsageInstant(null, UsageValueStatus.Unavailable),
        new UsagePercent(null, UsageValueStatus.Unavailable),
        new UsageInstant(null, UsageValueStatus.Unavailable),
        UsageLevel.Green);
}
