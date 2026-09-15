using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003): a canonical <see cref="UsageSnapshot"/>
/// paired with the content facts every skin's rendering of it must state. Pins facts, not
/// exact strings — each skin keeps ownership of its own wording.
/// </summary>
public sealed record GoldenMasterFixture(
    string Name,
    UsageSnapshot Snapshot,
    TimeZoneInfo DisplayZone,
    IReadOnlyList<ContentFact> ContentFacts);
