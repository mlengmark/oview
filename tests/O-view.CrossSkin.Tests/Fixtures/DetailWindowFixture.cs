using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned content-fact fixture (ADR-0003) for the detail window surface (ADR-0008 D2).
/// Pure data only, by design, and narrower than the six existing families on purpose: no
/// presenter exists yet for the detail window (ADR-0008 slices 6/7/10/11), so this type
/// carries no <c>Render</c>/<c>SkinUnderTest</c> — a real cross-skin test is wired against
/// each fixture once a presenter does (OVI-324, per Chief Gary II's OVI-325 scoping decision).
///
/// <para>Scoped to what <see cref="UsageSnapshot"/> already carries today: session/weekly
/// percent, their resets, <see cref="UsageSnapshot.DataSourceKind"/>, and
/// <see cref="UsageSnapshot.ExtraUsage"/>. ADR-0008 D2 also requires the detail window to
/// show the per-model split, the statistics, and the no-data explanation — the first two need
/// a widened <c>IShellToSkin</c>/Core type this slice is not scoped to invent (that is a
/// separate Core/shell contract task); the no-data case is covered here via
/// <see cref="UsageSnapshot.Unavailable"/>, which the current contract already expresses.</para>
/// </summary>
public sealed record DetailWindowFixture(
    string Name,
    UsageSnapshot Snapshot,
    TimeZoneInfo DisplayZone,
    IReadOnlyList<ContentFact> ContentFacts);
