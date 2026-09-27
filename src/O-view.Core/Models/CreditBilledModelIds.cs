namespace OView.Core.Models;

/// <summary>
/// The vendor model ids known to bill as extra usage (credits) rather than drawing from the
/// plan's 5-hour window (ADR-0001, OVI-168). A Core-owned static set, not a snapshot field —
/// unlike every other row in the contract, this does not vary per reading. Core owns the set
/// verbatim, as vendor ids; a skin owns how it is joined into a sentence. Replaces the source
/// app's <c>CreditBilledModels.DisplayList</c>, which was a pre-joined display string.
/// </summary>
public static class CreditBilledModelIds
{
    /// <summary>Verbatim vendor model ids, never display names.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "claude-fable-5",
        "claude-mythos-5",
    };
}
