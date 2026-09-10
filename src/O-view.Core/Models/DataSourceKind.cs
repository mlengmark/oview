namespace OView.Core.Models;

/// <summary>
/// The confidence tier that produced the other values in a <see cref="UsageSnapshot"/>
/// (ADR-0001). A skin uses this, not a guess, to decide how much to trust — and how to
/// word — the values it renders.
/// </summary>
public enum DataSourceKind
{
    /// <summary>Authoritative data, fresh from the vendor source.</summary>
    Live,

    /// <summary>Derived from local JSONL token counts when the authoritative source is unreachable.</summary>
    JsonlFallback,

    /// <summary>Modelled from token pricing rather than measured usage.</summary>
    Estimate,

    /// <summary>No source produced a snapshot at all.</summary>
    Unavailable,
}
