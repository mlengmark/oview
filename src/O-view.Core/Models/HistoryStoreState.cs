namespace OView.Core.Models;

/// <summary>
/// Whether a local history store (ADR-0006 D1) survived this session intact
/// (<see cref="Ok"/>), was found corrupt and moved aside in favour of a fresh empty store
/// (<see cref="Rebuilt"/>), or still cannot be read or written after that recovery attempt
/// (<see cref="Unavailable"/>) — ADR-0006 D4, the one row that ADR adds to
/// <c>UsageSnapshot</c>'s contract.
///
/// <para>Without this, a rebuilt store and a fresh install are indistinguishable to a skin,
/// and <c>HistoryCoverage.RecordedDays</c> silently reads as near-zero with no explanation
/// available to render. Core reports which of these three happened; the skin decides whether,
/// and how, to say so.</para>
/// </summary>
public enum HistoryStoreState
{
    /// <summary>The store was readable and writable this session. The default, unremarkable case.</summary>
    Ok,

    /// <summary>
    /// A corrupt copy of the store was found this session, moved aside rather than deleted
    /// (ADR-0006 D3.3), and replaced with a fresh empty store. <c>HistoryCoverage.RecordedDays</c>
    /// is newly near-zero for a reason the user should be allowed to know.
    /// </summary>
    Rebuilt,

    /// <summary>
    /// The store still cannot be read or written after a recovery attempt — for example, the
    /// corrupt copy could not even be moved aside. History for this session is unavailable, not
    /// guessed at.
    /// </summary>
    Unavailable,
}
