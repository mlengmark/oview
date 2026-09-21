using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of golden-master fixtures (ADR-0003). Add a new fixture as a new
/// entry in <see cref="All"/> — see
/// <c>tests/O-view.CrossSkin.Tests/README.md</c> for the step-by-step guide, and
/// ADR-0003 for why a fixture pins content facts rather than exact strings.
/// </summary>
public static class GoldenMasterFixtures
{
    /// <summary>
    /// The one fixture this slice ships: the OVI-4 known-good reference reading (ADR-0003,
    /// "one fixture reuses OVI-4's actual observed runtime output"), also used byte-for-byte
    /// as <c>O-view.Tray.Tests</c>' own regression target. Ordinary usage, all real values,
    /// no estimate/fallback disclosure expected.
    /// </summary>
    public static readonly GoldenMasterFixture Ovi4ReferenceReading = new(
        Name: "ovi4-reference-reading",
        Snapshot: new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 8, 20, 59, 0, TimeSpan.Zero), UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(new DateTimeOffset(2026, 9, 7, 23, 0, 0, TimeSpan.Zero), UsageValueStatus.Real),
            UsageLevel.Amber),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            // Rounding rule: integer, no decimal (ADR-0003's own worked example).
            ContentFact.Contains("57%"),
            ContentFact.Contains("14%"),
            // Both reset instants render in the fixture's display zone (UTC here).
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
        });

    public static IReadOnlyList<GoldenMasterFixture> All { get; } = new[]
    {
        Ovi4ReferenceReading,
    };
}
