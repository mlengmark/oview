using OView.Core.Models;
using OView.Core.Statistics;
using OView.Core.Updates;

namespace OView.App.Tests;

/// <summary>
/// Proves ADR-0007 D2 point 6's dedup rule against fakes only, per
/// <see cref="UsageEventDecider"/>'s own doc comment: raise once on the way up, re-arm on the
/// way back down (threshold, off-plan), or once per provider/version (input degraded, update
/// available) — never twice for the same unchanged occurrence. The boundary cases — exactly at
/// the threshold, exactly one point below it, the read immediately after a reset — are the
/// ones a wrong implementation gets wrong, so they get their own tests rather than being folded
/// into a "happy path" case.
/// </summary>
public sealed class UsageEventDeciderTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;
    private static readonly ShellSettings Settings80 = ShellSettings.Default with { AlertThresholdPercent = 80 };

    private static UsageSnapshot SnapshotAt(double? sessionPercent, UsageLevel level = UsageLevel.Green) => new(
        DataSourceKind.Live,
        DateTimeOffset.UnixEpoch,
        sessionPercent is { } value ? new UsagePercent(value, UsageValueStatus.Real) : new UsagePercent(null, UsageValueStatus.Unavailable),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(0, UsageValueStatus.Real),
        new UsageInstant(DateTimeOffset.UnixEpoch.AddDays(1), UsageValueStatus.Real),
        level);

    private static DivergenceReading Diverging() => new(
        DivergenceState.Diverging,
        new TokenCount(1000, UsageValueStatus.Real),
        PlanRisePoints: 5);

    private static DivergenceReading Consistent() => new(
        DivergenceState.Consistent,
        new TokenCount(0, UsageValueStatus.Real),
        PlanRisePoints: null);

    // ---- ThresholdCrossed ---------------------------------------------------------------

    [Fact]
    public void Crossing_at_exactly_the_threshold_raises_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        var events = decider.OnPollSucceeded(SnapshotAt(80), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Single(events, e => e.Kind == UsageEventKind.ThresholdCrossed);
    }

    [Fact]
    public void One_point_below_the_threshold_never_raises()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        var events = decider.OnPollSucceeded(SnapshotAt(79), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Empty(events);
    }

    [Fact]
    public void Staying_above_the_threshold_across_polls_raises_only_on_the_first_one()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(80), Array.Empty<ProviderHealth>(), Settings80);
        var second = decider.OnPollSucceeded(SnapshotAt(95), Array.Empty<ProviderHealth>(), Settings80);
        var third = decider.OnPollSucceeded(SnapshotAt(100), Array.Empty<ProviderHealth>(), Settings80);

        Assert.DoesNotContain(second, e => e.Kind == UsageEventKind.ThresholdCrossed);
        Assert.DoesNotContain(third, e => e.Kind == UsageEventKind.ThresholdCrossed);
    }

    [Fact]
    public void Dropping_below_then_crossing_again_raises_a_second_time()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(85), Array.Empty<ProviderHealth>(), Settings80);
        decider.OnPollSucceeded(SnapshotAt(70), Array.Empty<ProviderHealth>(), Settings80);
        var reCrossed = decider.OnPollSucceeded(SnapshotAt(85), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Single(reCrossed, e => e.Kind == UsageEventKind.ThresholdCrossed);
    }

    [Fact]
    public void A_missing_session_percent_re_arms_rather_than_assuming_continuation()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(85), Array.Empty<ProviderHealth>(), Settings80);
        decider.OnPollSucceeded(SnapshotAt(null), Array.Empty<ProviderHealth>(), Settings80);
        var afterUnknown = decider.OnPollSucceeded(SnapshotAt(85), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Single(afterUnknown, e => e.Kind == UsageEventKind.ThresholdCrossed);
    }

    [Fact]
    public void Lowering_the_threshold_between_polls_is_read_fresh_every_call()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var settings70 = Settings80 with { AlertThresholdPercent = 70 };

        var events = decider.OnPollSucceeded(SnapshotAt(75), Array.Empty<ProviderHealth>(), settings70);

        Assert.Single(events, e => e.Kind == UsageEventKind.ThresholdCrossed);
    }

    [Fact]
    public void Threshold_event_carries_the_snapshots_usage_level()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        var events = decider.OnPollSucceeded(SnapshotAt(80, UsageLevel.Red), Array.Empty<ProviderHealth>(), Settings80);

        var raised = Assert.Single(events, e => e.Kind == UsageEventKind.ThresholdCrossed);
        Assert.Equal(UsageLevel.Red, raised.UsageLevel);
    }

    // ---- OffPlanEntered ------------------------------------------------------------------

    [Fact]
    public void Entering_off_plan_raises_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(Diverging()), new FakeClock(), Zone);

        var events = decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Single(events, e => e.Kind == UsageEventKind.OffPlanEntered);
    }

    [Fact]
    public void Staying_off_plan_across_polls_raises_only_once()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);
        var second = decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);

        Assert.DoesNotContain(second, e => e.Kind == UsageEventKind.OffPlanEntered);
    }

    [Fact]
    public void Returning_to_plan_then_diverging_again_raises_a_second_time()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);
        statistics.Divergence = Consistent();
        decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);
        statistics.Divergence = Diverging();
        var reEntered = decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);

        Assert.Single(reEntered, e => e.Kind == UsageEventKind.OffPlanEntered);
    }

    [Fact]
    public void A_failed_ledger_read_preserves_the_armed_state_instead_of_re_arming()
    {
        var statistics = new FakeStatisticsSource(Diverging());
        var decider = new UsageEventDecider(statistics, new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);
        statistics.Divergence = null;
        decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);
        statistics.Divergence = Diverging();
        var stillDiverging = decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);

        Assert.DoesNotContain(stillDiverging, e => e.Kind == UsageEventKind.OffPlanEntered);
    }

    [Fact]
    public void OffPlan_event_carries_the_divergence_reading()
    {
        var divergence = Diverging();
        var decider = new UsageEventDecider(new FakeStatisticsSource(divergence), new FakeClock(), Zone);

        var events = decider.OnPollSucceeded(SnapshotAt(0), Array.Empty<ProviderHealth>(), Settings80);

        var raised = Assert.Single(events, e => e.Kind == UsageEventKind.OffPlanEntered);
        Assert.Equal(divergence, raised.Divergence);
    }

    // ---- InputDegraded --------------------------------------------------------------------

    [Fact]
    public void A_provider_failing_raises_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var health = new[] { Health("jsonl", ProviderHealthOutcome.Failed) };

        var events = decider.OnPollSucceeded(SnapshotAt(0), health, Settings80);

        Assert.Single(events, e => e.Kind == UsageEventKind.InputDegraded);
    }

    [Fact]
    public void A_provider_staying_failed_across_polls_raises_only_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var health = new[] { Health("jsonl", ProviderHealthOutcome.Failed) };

        decider.OnPollSucceeded(SnapshotAt(0), health, Settings80);
        var second = decider.OnPollSucceeded(SnapshotAt(0), health, Settings80);

        Assert.DoesNotContain(second, e => e.Kind == UsageEventKind.InputDegraded);
    }

    [Fact]
    public void A_provider_recovering_then_failing_again_raises_a_second_time()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.OnPollSucceeded(SnapshotAt(0), new[] { Health("jsonl", ProviderHealthOutcome.Failed) }, Settings80);
        decider.OnPollSucceeded(SnapshotAt(0), new[] { Health("jsonl", ProviderHealthOutcome.Ok) }, Settings80);
        var reFailed = decider.OnPollSucceeded(SnapshotAt(0), new[] { Health("jsonl", ProviderHealthOutcome.Failed) }, Settings80);

        Assert.Single(reFailed, e => e.Kind == UsageEventKind.InputDegraded);
    }

    [Fact]
    public void NoData_does_not_count_as_degraded()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var health = new[] { Health("jsonl", ProviderHealthOutcome.NoData) };

        var events = decider.OnPollSucceeded(SnapshotAt(0), health, Settings80);

        Assert.DoesNotContain(events, e => e.Kind == UsageEventKind.InputDegraded);
    }

    [Fact]
    public void Two_providers_failing_each_raise_their_own_event_independently()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var health = new[] { Health("jsonl", ProviderHealthOutcome.Failed), Health("cached", ProviderHealthOutcome.Failed) };

        var events = decider.OnPollSucceeded(SnapshotAt(0), health, Settings80);

        Assert.Equal(2, events.Count(e => e.Kind == UsageEventKind.InputDegraded));
    }

    [Fact]
    public void InputDegraded_event_carries_the_failing_providers_health()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);
        var failed = Health("jsonl", ProviderHealthOutcome.Failed);

        var events = decider.OnPollSucceeded(SnapshotAt(0), new[] { failed }, Settings80);

        var raised = Assert.Single(events, e => e.Kind == UsageEventKind.InputDegraded);
        Assert.Equal(failed, raised.Health);
    }

    private static ProviderHealth Health(string name, ProviderHealthOutcome outcome) =>
        new(name, outcome, LastSuccessAt: null, ConsecutiveFailures: outcome == ProviderHealthOutcome.Failed ? 1 : 0);

    // ---- UpdateAvailable -------------------------------------------------------------------

    [Fact]
    public void A_newer_version_raises_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        var raised = decider.DecideUpdateAvailable(UpdateResult("1.1.0"));

        Assert.NotNull(raised);
        Assert.Equal(UsageEventKind.UpdateAvailable, raised!.Kind);
    }

    [Fact]
    public void The_same_version_checked_twice_raises_only_once()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.DecideUpdateAvailable(UpdateResult("1.1.0"));
        var second = decider.DecideUpdateAvailable(UpdateResult("1.1.0"));

        Assert.Null(second);
    }

    [Fact]
    public void A_genuinely_newer_version_after_an_earlier_one_was_already_raised_raises_again()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.DecideUpdateAvailable(UpdateResult("1.1.0"));
        var raised = decider.DecideUpdateAvailable(UpdateResult("1.2.0"));

        Assert.NotNull(raised);
    }

    [Fact]
    public void UpToDate_raises_nothing()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        Assert.Null(decider.DecideUpdateAvailable(UpdateCheckResult.UpToDate));
    }

    [Fact]
    public void A_rate_limited_gap_does_not_re_arm_an_already_notified_version()
    {
        var decider = new UsageEventDecider(new FakeStatisticsSource(null), new FakeClock(), Zone);

        decider.DecideUpdateAvailable(UpdateResult("1.1.0"));
        decider.DecideUpdateAvailable(UpdateCheckResult.RateLimited());
        var stillSuppressed = decider.DecideUpdateAvailable(UpdateResult("1.1.0"));

        Assert.Null(stillSuppressed);
    }

    private static UpdateCheckResult UpdateResult(string version)
    {
        ReleaseVersion.TryParse(version, out var parsed);
        return new UpdateCheckResult(UpdateOutcome.UpdateAvailable, new AvailableUpdate(parsed, $"v{version}", "https://example.invalid/asset"));
    }

    private sealed class FakeStatisticsSource : IUsageStatisticsSource
    {
        public FakeStatisticsSource(DivergenceReading? divergence) => Divergence = divergence;

        public DivergenceReading? Divergence { get; set; }

        public UsageStatistics GetStatistics(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            UsageStatistics.Unavailable with { Divergence = Divergence };

        public ModelUsageBreakdown GetModelBreakdown(DateTimeOffset utcNow, TimeZoneInfo zone) =>
            ModelUsageBreakdown.Unavailable;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
