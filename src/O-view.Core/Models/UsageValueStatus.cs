namespace OView.Core.Models;

/// <summary>
/// The trust label every value in the Core-to-skin data contract carries (ADR-0001).
/// A skin must never guess whether a value is trustworthy — it is told.
/// </summary>
public enum UsageValueStatus
{
    /// <summary>Measured directly from a vendor source.</summary>
    Real,

    /// <summary>Derived or modelled — for example, from token pricing or a fallback source.</summary>
    Estimated,

    /// <summary>No source produced a value. A skin renders this as an explicit gap, never as zero or blank.</summary>
    Unavailable,
}
