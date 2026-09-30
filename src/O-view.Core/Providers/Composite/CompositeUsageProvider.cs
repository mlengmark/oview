using OView.Core.Models;

namespace OView.Core.Providers.Composite;

/// <summary>
/// Resolves the provider chain — typically <c>PlanHistoryProvider</c> →
/// <c>CachedUtilizationProvider</c> → <c>JsonlUsageProvider</c> (OAuth deferred) — into the
/// one <see cref="UsageSnapshot"/> a skin renders (ADR-0005 D3, slice 6).
///
/// <para><b>Selection is by information value, not list order (D3).</b> Every candidate
/// snapshot that is not itself <see cref="DataSourceKind.Unavailable"/> is ranked, highest
/// first, by: (1) tier — <see cref="DataSourceKind.Live"/> beats
/// <see cref="DataSourceKind.Stale"/> beats <see cref="DataSourceKind.JsonlFallback"/> beats
/// <see cref="DataSourceKind.Estimate"/>; (2) completeness — how many of the four session/
/// weekly percent-and-reset fields are not <see cref="UsageValueStatus.Unavailable"/>; (3)
/// recency — <see cref="UsageSnapshot.LastIngestAt"/>, newest first; (4) argument order, as a
/// final tie-break only, earliest-declared provider first. The winning snapshot is returned
/// exactly as its provider built it — its own <see cref="UsageSnapshot.DataSourceKind"/> and
/// every other field travel to the skin unchanged; this type never relabels or merges
/// fields across candidates. <b>Rejected: strict precedence by list position</b> — the
/// source repository tried that and amended it away (ADR-0002 precedence, amended by
/// ADR-0007): with two sources reporting the same meters, position alone could show a
/// reading up to a sampling interval old while a fresher one sat unread in the other
/// source.</para>
///
/// <para><b>Provider health is contract data, not a log line (D4).</b> Every poll produces
/// one <see cref="ProviderHealth"/> entry per provider consulted, exposed on <see cref="Health"/>
/// after <see cref="GetSnapshot"/> returns, plus <see cref="DegradedInputCount"/> — the number
/// of providers currently <see cref="ProviderHealthOutcome.Failed"/>. This type detects and
/// counts; it never decides whether or how a skin surfaces that (ADR-0003) and never builds a
/// sentence out of it.</para>
///
/// <para>The <c>log</c> constructor parameter receives one line per swallowed per-provider
/// exception — a provider that violates <see cref="IUsageProvider"/>'s own never-throw
/// contract (ADR-0005 D1) is still caught here so this type's own <see cref="GetSnapshot"/>
/// upholds that contract, but the violation is never silent: it is what let five days of
/// failed ingestion sit behind a panel reading <c>status: Ok</c> (CONFIRMED, D4). The seam is
/// diagnostics-bundle detail, in addition to <see cref="Health"/> — not a replacement for it,
/// since a log file the user never opens is not something a skin can act on.</para>
/// </summary>
public sealed class CompositeUsageProvider : IUsageProvider
{
    private readonly IReadOnlyList<NamedUsageProvider> _providers;
    private readonly Action<string>? _log;
    private readonly Dictionary<string, DateTimeOffset?> _lastSuccessAt;
    private readonly Dictionary<string, int> _consecutiveFailures;

    /// <param name="providers">The provider chain, in the order ties are broken by (D3) —
    /// earliest first. Must be non-empty.</param>
    /// <param name="log">Receives one diagnostic line whenever a provider throws, in addition
    /// to that provider's <see cref="ProviderHealth"/> entry (D4). Optional — a caller with no
    /// diagnostics bundle to write to may omit it; <see cref="Health"/> still reports the
    /// failure either way.</param>
    public CompositeUsageProvider(IReadOnlyList<NamedUsageProvider> providers, Action<string>? log = null)
    {
        if (providers is null || providers.Count == 0)
        {
            throw new ArgumentException("CompositeUsageProvider needs at least one provider to compose.", nameof(providers));
        }

        _providers = providers;
        _log = log;
        _lastSuccessAt = new Dictionary<string, DateTimeOffset?>();
        _consecutiveFailures = new Dictionary<string, int>();
        foreach (var named in providers)
        {
            _lastSuccessAt[named.Name] = null;
            _consecutiveFailures[named.Name] = 0;
        }
    }

    /// <summary>
    /// One <see cref="ProviderHealth"/> entry per provider, from the most recent
    /// <see cref="GetSnapshot"/> call — empty until the first call. Order matches the
    /// constructor's <c>providers</c> argument.
    /// </summary>
    public IReadOnlyList<ProviderHealth> Health { get; private set; } = Array.Empty<ProviderHealth>();

    /// <summary>
    /// How many providers were <see cref="ProviderHealthOutcome.Failed"/> on the most recent
    /// <see cref="GetSnapshot"/> call (D4) — lets a caller decide whether to say something
    /// without walking <see cref="Health"/> itself.
    /// </summary>
    public int DegradedInputCount => Health.Count(entry => entry.Outcome == ProviderHealthOutcome.Failed);

    /// <summary>
    /// Consults every provider in the chain, updates <see cref="Health"/> and
    /// <see cref="DegradedInputCount"/>, and returns the highest-information-value snapshot
    /// per D3 — or <see cref="UsageSnapshot.Unavailable"/> if none produced one. Never throws:
    /// a provider that throws is caught, logged through <see cref="Log"/> if supplied, and
    /// recorded as <see cref="ProviderHealthOutcome.Failed"/> rather than propagated.
    /// </summary>
    public UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
    {
        var health = new List<ProviderHealth>(_providers.Count);
        UsageSnapshot? bestSnapshot = null;
        int bestTier = -1;
        int bestCompleteness = -1;
        DateTimeOffset bestLastIngestAt = DateTimeOffset.MinValue;
        bool haveBest = false;

        for (var i = 0; i < _providers.Count; i++)
        {
            var named = _providers[i];
            ProviderHealthOutcome outcome;
            UsageSnapshot? snapshot = null;

            try
            {
                snapshot = named.Provider.GetSnapshot(utcNow);
                outcome = snapshot.DataSourceKind == DataSourceKind.Unavailable
                    ? ProviderHealthOutcome.NoData
                    : ProviderHealthOutcome.Ok;
            }
            catch (Exception ex)
            {
                outcome = ProviderHealthOutcome.Failed;
                _log?.Invoke($"CompositeUsageProvider: provider '{named.Name}' threw: {ex}");
            }

            if (outcome == ProviderHealthOutcome.Ok)
            {
                _lastSuccessAt[named.Name] = utcNow;
                _consecutiveFailures[named.Name] = 0;
            }
            else if (outcome == ProviderHealthOutcome.Failed)
            {
                _consecutiveFailures[named.Name] = _consecutiveFailures[named.Name] + 1;
            }

            health.Add(new ProviderHealth(
                named.Name,
                outcome,
                _lastSuccessAt[named.Name],
                _consecutiveFailures[named.Name]));

            if (outcome != ProviderHealthOutcome.Ok || snapshot is null)
            {
                continue;
            }

            var tier = Tier(snapshot.DataSourceKind);
            var completeness = Completeness(snapshot);
            var isBetter =
                !haveBest ||
                tier > bestTier ||
                (tier == bestTier && completeness > bestCompleteness) ||
                (tier == bestTier && completeness == bestCompleteness && snapshot.LastIngestAt > bestLastIngestAt);
            // Argument order is the final tie-break: the first candidate at an equal
            // tier/completeness/recency already holds `bestSnapshot`, so an equal-ranked
            // later candidate is deliberately not "better" and is left in place.

            if (isBetter)
            {
                bestSnapshot = snapshot;
                bestTier = tier;
                bestCompleteness = completeness;
                bestLastIngestAt = snapshot.LastIngestAt;
                haveBest = true;
            }
        }

        Health = health;
        return bestSnapshot ?? UsageSnapshot.Unavailable;
    }

    private static int Tier(DataSourceKind kind) => kind switch
    {
        DataSourceKind.Live => 4,
        DataSourceKind.Stale => 3,
        DataSourceKind.JsonlFallback => 2,
        DataSourceKind.Estimate => 1,
        _ => 0,
    };

    private static int Completeness(UsageSnapshot snapshot)
    {
        var count = 0;
        if (snapshot.SessionUtilizationPercent.Status != UsageValueStatus.Unavailable) count++;
        if (snapshot.SessionResetAt.Status != UsageValueStatus.Unavailable) count++;
        if (snapshot.WeeklyUtilizationPercent.Status != UsageValueStatus.Unavailable) count++;
        if (snapshot.WeeklyResetAt.Status != UsageValueStatus.Unavailable) count++;
        return count;
    }
}
