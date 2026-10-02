using OView.App;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned content-fact fixture (ADR-0003) for the alerts surface (ADR-0008 D2): "present
/// <c>RaiseEvent</c> and nothing else". Pure data only, by design, same as
/// <see cref="DetailWindowFixture"/> — no alert presenter exists yet (ADR-0008 slices 6/7/10/11),
/// so this type carries no <c>Render</c>/<c>SkinUnderTest</c> either (OVI-324, per Chief Gary
/// II's OVI-325 scoping decision). <see cref="OView.App.UsageEvent"/>/<see cref="UsageEventKind"/>
/// already cover everything D2 requires an alert to state — no Core/shell gap here, unlike the
/// detail window's per-model/statistics gap.
/// </summary>
public sealed record AlertFixture(
    string Name,
    UsageEvent Event,
    IReadOnlyList<ContentFact> ContentFacts);
