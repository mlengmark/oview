using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the <see cref="UsageStatistics"/> slice
/// of the contract — the figures <c>UsageFormatter.cs</c>/<c>PanelStatistics.cs</c>'s
/// presentation logic renders. Kept as its own fixture type, parallel to
/// <see cref="GoldenMasterFixture"/> rather than a change to it: that type is pinned to
/// <see cref="UsageSnapshot"/> (the tooltip's slice of the contract), and generalizing it to
/// a second, differently-shaped Core snapshot is exactly the kind of harness-mechanism
/// question this slice's task flags for escalation rather than silent improvisation. This
/// type reuses <see cref="ContentFact"/> as-is — that type was already snapshot-agnostic.
/// See the OVI-27 PR description for the escalation note.
/// </summary>
public sealed record UsageStatisticsFixture(
    string Name,
    UsageStatistics Statistics,
    IReadOnlyList<ContentFact> ContentFacts);
