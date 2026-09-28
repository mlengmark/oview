using OView.Core.Models;

namespace OView.Core.Providers;

/// <summary>
/// Core's one input seam for usage data (ADR-0005 D1), carried forward from the source
/// repository's <c>IUsageProvider</c>. A provider is one synchronous, side-effect-free
/// read — no <c>TryGet…</c>, no <c>Task&lt;…&gt;</c>, no stream; the polling loop that
/// would consume one belongs to the shell (ADR-0007), not to Core.
///
/// <para>Four obligations are contract, not style, and are proven by
/// <c>IUsageProviderContractTests</c>:</para>
/// <list type="bullet">
/// <item>Never throws — any failure, malformed file, missing directory or permission
/// error yields <see cref="UsageSnapshot.Unavailable"/>, never an exception.</item>
/// <item>The clock is injected — <paramref name="utcNow"/>, on <see cref="GetSnapshot"/>,
/// is the only time source; an implementation never reads the system clock itself.</item>
/// <item>Read-only — an implementation opens vendor data for reading and never writes,
/// moves, or truncates it.</item>
/// <item>No display text — an implementation returns contract values with status flags
/// (ADR-0001), never a pre-built sentence.</item>
/// </list>
/// </summary>
public interface IUsageProvider
{
    /// <summary>
    /// Returns the current usage snapshot as of <paramref name="utcNow"/>, or
    /// <see cref="UsageSnapshot.Unavailable"/> if this provider has nothing to report.
    /// </summary>
    /// <param name="utcNow">The instant to evaluate staleness and reset prediction
    /// against. The only time source this method may use.</param>
    UsageSnapshot GetSnapshot(DateTimeOffset utcNow);
}
