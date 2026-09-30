namespace OView.Core.Models;

/// <summary>
/// One provider's outcome on one poll (ADR-0005 D4) — the row this ADR adds to
/// ADR-0001's contract. Not a log line: a skin cannot tell the user something it is never
/// handed, and this is how
/// <see cref="OView.Core.Providers.Composite.CompositeUsageProvider"/> hands it over.
///
/// <para>By contract this type carries no pre-built sentence — Core detects and counts;
/// the skin decides whether and how to surface it (ADR-0003).</para>
/// </summary>
/// <param name="ProviderName">Identifies which provider this entry describes. Stable across
/// polls for the same provider instance so a skin or diagnostic can track one provider's
/// history.</param>
/// <param name="Outcome">What happened this poll.</param>
/// <param name="LastSuccessAt">The <c>utcNow</c> of this provider's most recent
/// <see cref="ProviderHealthOutcome.Ok"/> poll, or <c>null</c> if it has never once
/// succeeded.</param>
/// <param name="ConsecutiveFailures">How many polls in a row this provider has thrown, most
/// recent streak only. Reset to zero by an <see cref="ProviderHealthOutcome.Ok"/> poll.
/// <see cref="ProviderHealthOutcome.NoData"/> neither extends nor resets this streak — it is
/// a different fact from a failure (D4).</param>
public sealed record ProviderHealth(
    string ProviderName,
    ProviderHealthOutcome Outcome,
    DateTimeOffset? LastSuccessAt,
    int ConsecutiveFailures);
