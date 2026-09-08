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
    UsagePercent SessionUtilizationPercent,
    UsageInstant SessionResetAt,
    UsagePercent WeeklyUtilizationPercent,
    UsageInstant WeeklyResetAt,
    UsageLevel UsageLevel)
{
    /// <summary>The canonical "no data" snapshot — every value unavailable, not zero.</summary>
    public static UsageSnapshot Unavailable { get; } = new(
        DataSourceKind.Unavailable,
        new UsagePercent(null, UsageValueStatus.Unavailable),
        new UsageInstant(null, UsageValueStatus.Unavailable),
        new UsagePercent(null, UsageValueStatus.Unavailable),
        new UsageInstant(null, UsageValueStatus.Unavailable),
        UsageLevel.Green);
}
