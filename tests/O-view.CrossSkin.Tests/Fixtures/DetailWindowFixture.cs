using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned content-fact fixture (ADR-0003) for the detail window surface (ADR-0008 D2).
/// Pure data only, by design, and narrower than the six existing families on purpose: no
/// presenter exists yet for the detail window (ADR-0008 slices 6/7/10/11), so this type
/// carries no <c>Render</c>/<c>SkinUnderTest</c> — a real cross-skin test is wired against
/// each fixture once a presenter does (OVI-324, per Chief Gary II's OVI-325 scoping decision).
///
/// <para>Carries the full <see cref="UsageDetail"/> the detail window actually receives
/// (ADR-0008 D9a, OVI-329): the plan-meter snapshot, the 31-day statistics, and the per-model
/// breakdown, pushed together so the window can never pair a percent from one poll with
/// statistics from another. Earlier (OVI-324) this fixture carried only a bare
/// <see cref="UsageSnapshot"/>, before <see cref="UsageDetail"/> existed.</para>
/// </summary>
public sealed record DetailWindowFixture(
    string Name,
    UsageDetail Detail,
    TimeZoneInfo DisplayZone,
    IReadOnlyList<ContentFact> ContentFacts);
