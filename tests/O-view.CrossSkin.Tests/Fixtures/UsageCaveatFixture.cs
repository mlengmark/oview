namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the usage-tile <c>Caveat</c>/<c>RateAge</c>.
/// Follows <see cref="BoostNoticeFixture"/>'s member-selecting shape: <see cref="Render"/> closes
/// over which <see cref="UsageCaveatSkinUnderTest"/> member and which input a fixture exercises.
/// Reuses <see cref="ContentFact"/> as-is (ADR-0003's 2026-09-23 amendment, OVI-100).
/// </summary>
public sealed record UsageCaveatFixture(
    string Name,
    Func<UsageCaveatSkinUnderTest, string> Render,
    IReadOnlyList<ContentFact> ContentFacts);
