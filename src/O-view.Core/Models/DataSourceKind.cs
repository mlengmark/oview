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

    /// <summary>
    /// Authoritative data, but older than the producing provider's own freshness threshold
    /// (ADR-0001's 2026-09-09 amendment). Still trusted above <see cref="JsonlFallback"/> or
    /// <see cref="Estimate"/> — a skin words its age from <see cref="UsageSnapshot.LastIngestAt"/>,
    /// never fabricates a fresher one.
    /// </summary>
    Stale,

    /// <summary>
    /// A snapshot from <c>JsonlUsageProvider</c>: local transcripts confirmed a real session
    /// happened, so this carries provenance and O-view's own capture time
    /// (<see cref="UsageSnapshot.LastIngestAt"/>) — not derived percentages. Transcripts
    /// record tokens spent, never a plan's limit, so no meter is computed from them
    /// (ADR-0005 D6a/D6b).
    /// </summary>
    JsonlFallback,

    /// <summary>
    /// Reserved and currently unemitted. No Phase 2 provider models a utilization
    /// percentage from token pricing — doing so needs a plan-limit table this repository
    /// does not have, and inventing one would fabricate a number (ADR-0005 D6b). Kept in the
    /// enum for a future provider that can genuinely model a meter.
    /// </summary>
    Estimate,

    /// <summary>No source produced a snapshot at all.</summary>
    Unavailable,
}
