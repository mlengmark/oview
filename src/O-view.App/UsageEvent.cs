using OView.Core.Models;

namespace OView.App;

/// <summary>
/// An enum-plus-data event raised through <see cref="IShellToSkin.RaiseEvent"/> (ADR-0007
/// D6, lines 198-222). By contract this type carries no pre-built sentence, title, or
/// severity colour — never a decision about how the event looks, only which event happened
/// and the Core-contract data (ADR-0001) a skin needs to decide that for itself.
///
/// <para>Every payload property is optional: a given <see cref="UsageEventKind"/> populates
/// only the properties it has data for, and the shell's event-decision logic (D2 point 6) —
/// which detects threshold crossings and off-plan entry, de-duplicates per window, and wires
/// the update check — is out of scope for this slice. This type exists so that logic has
/// somewhere to put its result, and the seam can be tested against fakes now.</para>
/// </summary>
public sealed record UsageEvent(UsageEventKind Kind)
{
    /// <summary>Populated for <see cref="UsageEventKind.ThresholdCrossed"/>: the band the
    /// crossing moved into.</summary>
    public UsageLevel? UsageLevel { get; init; }

    /// <summary>Populated for <see cref="UsageEventKind.OffPlanEntered"/>: the divergence
    /// reading that triggered it (ADR-0001, OVI-168).</summary>
    public DivergenceReading? Divergence { get; init; }

    /// <summary>Populated for <see cref="UsageEventKind.InputDegraded"/>: which provider
    /// degraded and how (ADR-0005 D4).</summary>
    public ProviderHealth? Health { get; init; }
}
