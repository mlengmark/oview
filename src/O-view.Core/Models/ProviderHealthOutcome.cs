namespace OView.Core.Models;

/// <summary>
/// What happened when <see cref="OView.Core.Providers.Composite.CompositeUsageProvider"/>
/// consulted one provider on a poll (ADR-0005 D4). <see cref="NoData"/> and
/// <see cref="Failed"/> are different facts and must never be merged: a provider with
/// nothing to report is not the same as one that broke.
/// </summary>
public enum ProviderHealthOutcome
{
    /// <summary>The provider returned a snapshot with real data.</summary>
    Ok,

    /// <summary>
    /// The provider ran without throwing and reported
    /// <see cref="UsageSnapshot.Unavailable"/> — for example, no vendor file exists on this
    /// machine. Expected steady state for some providers, not a fault.
    /// </summary>
    NoData,

    /// <summary>
    /// The provider threw. <see cref="IUsageProvider.GetSnapshot"/>'s own contract (ADR-0005
    /// D1) says this should never happen; this outcome exists so a provider that violates
    /// that contract is caught and counted rather than propagated or silently swallowed.
    /// </summary>
    Failed,
}
