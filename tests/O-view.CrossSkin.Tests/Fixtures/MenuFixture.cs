namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned content-fact fixture (ADR-0003) for one scenario of one control-surface menu
/// item (ADR-0009 D1-D4). Pure data only, by design, same as <see cref="AlertFixture"/> — no
/// menu presenter exists yet (ADR-0009 slices 6 and 9 build the Windows and Linux menus), so
/// this type carries no <c>Render</c>/<c>SkinUnderTest</c> either. Those slices read this fixture
/// set to know what their menus must say before either exists.
///
/// <para><see cref="StartupRequestedEnabled"/> and <see cref="StartupOsReturnedEnabled"/> pin
/// ADR-0009 D3: a future menu's checkbox must reflect what
/// <see cref="OView.App.IStartupRegistration.Apply"/> actually returned, not what the user
/// clicked. When the two differ, <see cref="FailedToggle"/> records that the future skin owes a
/// statement, in its own wording, that the change did not take effect — ADR-0003 forbids
/// pinning that wording here, so no <see cref="ContentFact"/> attempts to.</para>
///
/// <para><see cref="PersistedThresholdPercent"/> pins ADR-0009 D4 read through ADR-0003: the
/// picker must show the shell's persisted value, not a fixed label. <see cref="ContentFacts"/>
/// pins the raw digit for that scenario — it survives whatever wording wraps it, the same
/// convention <see cref="AlertFixtures.OffPlanEnteredWithMeasurableRise"/> uses for a scalar.</para>
/// </summary>
public sealed record MenuFixture(
    string Name,
    MenuItemKind Item,
    IReadOnlyList<ContentFact> ContentFacts,
    bool? StartupRequestedEnabled = null,
    bool? StartupOsReturnedEnabled = null,
    bool FailedToggle = false,
    int? PersistedThresholdPercent = null);
