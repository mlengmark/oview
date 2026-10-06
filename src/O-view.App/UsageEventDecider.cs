using OView.Core.Models;
using OView.Core.Statistics;
using OView.Core.Updates;

namespace OView.App;

/// <summary>
/// ADR-0007 D2 point 6's event decision (ADR-0009 slicing table row 3, OVI-457): decides
/// *that* one of the four <see cref="UsageEventKind"/> values happened and whether it has
/// already been raised "for this window" — never how it looks, which stays the skin's job
/// (<see cref="IShellToSkin.RaiseEvent"/>). Ported from the source app's <c>ThresholdWatcher</c>
/// and <c>UsageEngine.CheckOffPlan</c> (edge-triggered: raise once on the way up, re-arm on the
/// way back down), the only prior art this contract has for the dedup rule's exact shape.
///
/// <para><b>Threshold crossing</b> (<see cref="UsageEventKind.ThresholdCrossed"/>) compares
/// <see cref="UsageSnapshot.SessionUtilizationPercent"/> — the same figure the source app's
/// watcher compared — against <see cref="ShellSettings.AlertThresholdPercent"/>, never a
/// hardcoded default (the threshold picker this slice precedes, ADR-0009 D5, is pointless
/// otherwise). A missing percent re-arms, exactly like the source watcher's "data went
/// unknown" branch: it cannot tell whether the next real reading is a fresh crossing or a
/// continuation, so it must not assume continuation.</para>
///
/// <para><b>Off-plan entry</b> (<see cref="UsageEventKind.OffPlanEntered"/>) reads
/// <see cref="DivergenceReading.IsOffPlan"/> off <see cref="IUsageStatisticsSource.GetStatistics"/>
/// — the same ledger seam <see cref="DetailPushCoordinator"/> already reads for ADR-0008 D9a, so
/// this is not new fetch/poll logic, only a second consultation of data already produced. Unlike
/// the threshold case, a failed read (<c>null</c> <see cref="UsageStatistics.Divergence"/>)
/// preserves whatever armed state already existed rather than re-arming — mirroring source's
/// own comment that "a failed build must not re-arm the edge trigger," because the ledger read
/// failing says nothing about whether local activity actually came back on plan.</para>
///
/// <para><b>Input degraded</b> (<see cref="UsageEventKind.InputDegraded"/>) fires once per
/// provider name the moment its <see cref="ProviderHealth.Outcome"/> becomes
/// <see cref="ProviderHealthOutcome.Failed"/>, re-arming when that provider is next
/// <see cref="ProviderHealthOutcome.Ok"/> or <see cref="ProviderHealthOutcome.NoData"/> — mirrors
/// ADR-0005 D4's own distinction that only <c>Failed</c> counts toward <c>DegradedInputCount</c>;
/// <c>NoData</c> is an expected steady state for some providers, not a fault to alert on.</para>
///
/// <para><b>Update available</b> (<see cref="UsageEventKind.UpdateAvailable"/>) is decided by
/// <see cref="DecideUpdateAvailable"/>, deliberately <i>not</i> called from
/// <see cref="OnPollSucceeded"/>: the update check's fetch is ADR-0007 D3's own slice (OVI-433),
/// and the poll loop produces no <see cref="UpdateCheckResult"/> for this slice to consume
/// without adding the fetch logic this slice's brief rules out. The dedup rule — raise once per
/// distinct newer version, not once ever — is proven here so whichever later slice wires the
/// fetch has a tested decision to call into rather than inventing one inline.</para>
/// </summary>
public sealed class UsageEventDecider
{
    private readonly IUsageStatisticsSource _statistics;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _zone;

    private bool _aboveThreshold;
    private bool _offPlan;
    private readonly Dictionary<string, bool> _providerFailed = new(StringComparer.Ordinal);
    private ReleaseVersion? _lastNotifiedUpdateVersion;

    public UsageEventDecider(IUsageStatisticsSource statistics, IClock clock, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(zone);

        _statistics = statistics;
        _clock = clock;
        _zone = zone;
    }

    /// <summary>
    /// Called from the poll loop's success path (same call-site shape as
    /// <see cref="DetailPushCoordinator.OnPollSucceeded"/>) with the snapshot just polled and
    /// the provider health behind it. Returns every event newly decided this poll — usually
    /// none. <paramref name="settings"/> is read fresh each call so a threshold change
    /// (<c>ISkinToShell.SetThresholdPercent</c>) takes effect on the very next poll.
    /// </summary>
    public IReadOnlyList<UsageEvent> OnPollSucceeded(
        UsageSnapshot snapshot, IReadOnlyList<ProviderHealth> health, ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(settings);

        var events = new List<UsageEvent>();

        if (DecideThresholdCrossed(snapshot, settings) is { } thresholdEvent)
        {
            events.Add(thresholdEvent);
        }

        if (DecideOffPlanEntered() is { } offPlanEvent)
        {
            events.Add(offPlanEvent);
        }

        events.AddRange(DecideInputDegraded(health));

        return events;
    }

    private UsageEvent? DecideThresholdCrossed(UsageSnapshot snapshot, ShellSettings settings)
    {
        if (snapshot.SessionUtilizationPercent.Value is not { } percent || percent < settings.AlertThresholdPercent)
        {
            _aboveThreshold = false;
            return null;
        }

        if (_aboveThreshold)
        {
            return null;
        }

        _aboveThreshold = true;
        return new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = snapshot.UsageLevel };
    }

    private UsageEvent? DecideOffPlanEntered()
    {
        var divergence = _statistics.GetStatistics(_clock.UtcNow, _zone).Divergence;

        if (divergence is null)
        {
            return null;
        }

        if (!divergence.IsOffPlan)
        {
            _offPlan = false;
            return null;
        }

        if (_offPlan)
        {
            return null;
        }

        _offPlan = true;
        return new UsageEvent(UsageEventKind.OffPlanEntered) { Divergence = divergence };
    }

    private List<UsageEvent> DecideInputDegraded(IReadOnlyList<ProviderHealth> health)
    {
        var events = new List<UsageEvent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in health)
        {
            seen.Add(entry.ProviderName);

            if (entry.Outcome != ProviderHealthOutcome.Failed)
            {
                _providerFailed.Remove(entry.ProviderName);
                continue;
            }

            if (_providerFailed.GetValueOrDefault(entry.ProviderName))
            {
                continue;
            }

            _providerFailed[entry.ProviderName] = true;
            events.Add(new UsageEvent(UsageEventKind.InputDegraded) { Health = entry });
        }

        // A provider absent from this poll's health list (composition changed) re-arms rather
        // than leaking state for a provider that no longer exists to recover.
        foreach (var stale in _providerFailed.Keys.Where(name => !seen.Contains(name)).ToList())
        {
            _providerFailed.Remove(stale);
        }

        return events;
    }

    /// <summary>
    /// The update-check half of the decision (see type summary): raise at most once per
    /// distinct <see cref="AvailableUpdate.Version"/>, so a feed re-checked on the same cooldown
    /// (ADR-0007 D3) does not re-notify for a version the user has already been told about, but
    /// a genuinely newer one still gets through.
    /// </summary>
    public UsageEvent? DecideUpdateAvailable(UpdateCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Outcome != UpdateOutcome.UpdateAvailable || result.Available is not { } available)
        {
            return null;
        }

        if (_lastNotifiedUpdateVersion == available.Version)
        {
            return null;
        }

        _lastNotifiedUpdateVersion = available.Version;
        return new UsageEvent(UsageEventKind.UpdateAvailable);
    }
}
