using OView.Core.Models;
using OView.Core.Providers;
using OView.Core.Providers.Composite;

namespace OView.Core.Tests.Providers.Composite;

/// <summary>
/// Proves ADR-0005 D3/D4 (slice 6): tier beats completeness beats recency beats argument
/// order when selecting among candidate snapshots, the winner's own fields travel unchanged,
/// <see cref="ProviderHealth"/>/<see cref="CompositeUsageProvider.DegradedInputCount"/> are
/// emitted correctly with <c>NoData</c> and <c>Failed</c> kept distinct, and a swallowed
/// per-provider exception is observable through the <c>log</c> seam.
/// </summary>
public sealed class CompositeUsageProviderTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed class StubProvider(UsageSnapshot snapshot) : IUsageProvider
    {
        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow) => snapshot;
    }

    private sealed class ThrowingProvider(Exception exception) : IUsageProvider
    {
        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow) => throw exception;
    }

    private static UsageSnapshot Snapshot(
        DataSourceKind kind,
        DateTimeOffset lastIngestAt,
        bool sessionPercent = true,
        bool sessionReset = true,
        bool weeklyPercent = true,
        bool weeklyReset = true) => new(
        kind,
        lastIngestAt,
        sessionPercent ? new UsagePercent(50, UsageValueStatus.Real) : new UsagePercent(null, UsageValueStatus.Unavailable),
        sessionReset ? new UsageInstant(lastIngestAt, UsageValueStatus.Real) : new UsageInstant(null, UsageValueStatus.Unavailable),
        weeklyPercent ? new UsagePercent(50, UsageValueStatus.Real) : new UsagePercent(null, UsageValueStatus.Unavailable),
        weeklyReset ? new UsageInstant(lastIngestAt, UsageValueStatus.Real) : new UsageInstant(null, UsageValueStatus.Unavailable),
        UsageLevel.Green);

    [Fact]
    public void NoProvidersThrowsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new CompositeUsageProvider([]));
    }

    [Fact]
    public void HigherTierWinsRegardlessOfArgumentOrder()
    {
        var stale = Snapshot(DataSourceKind.Stale, UtcNow);
        var live = Snapshot(DataSourceKind.Live, UtcNow);
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("first", new StubProvider(stale)),
            new NamedUsageProvider("second", new StubProvider(live)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Same(live, result);
    }

    [Theory]
    [InlineData(DataSourceKind.Live, DataSourceKind.Stale)]
    [InlineData(DataSourceKind.Stale, DataSourceKind.JsonlFallback)]
    [InlineData(DataSourceKind.JsonlFallback, DataSourceKind.Estimate)]
    public void TierOrderingIsLiveThenStaleThenJsonlFallbackThenEstimate(DataSourceKind higher, DataSourceKind lower)
    {
        var higherSnapshot = Snapshot(higher, UtcNow);
        var lowerSnapshot = Snapshot(lower, UtcNow);
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("lower", new StubProvider(lowerSnapshot)),
            new NamedUsageProvider("higher", new StubProvider(higherSnapshot)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Same(higherSnapshot, result);
    }

    [Fact]
    public void SameTierMoreCompleteSnapshotWinsOverLessComplete()
    {
        var incomplete = Snapshot(DataSourceKind.JsonlFallback, UtcNow, sessionPercent: false, sessionReset: false, weeklyPercent: false, weeklyReset: false);
        var complete = Snapshot(DataSourceKind.JsonlFallback, UtcNow);
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("incomplete", new StubProvider(incomplete)),
            new NamedUsageProvider("complete", new StubProvider(complete)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Same(complete, result);
    }

    [Fact]
    public void SameTierAndCompletenessMoreRecentSnapshotWins()
    {
        var older = Snapshot(DataSourceKind.Live, UtcNow.AddMinutes(-10));
        var newer = Snapshot(DataSourceKind.Live, UtcNow);
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("older", new StubProvider(older)),
            new NamedUsageProvider("newer", new StubProvider(newer)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Same(newer, result);
    }

    [Fact]
    public void SameTierCompletenessAndRecencyFirstArgumentWins()
    {
        var first = Snapshot(DataSourceKind.Live, UtcNow);
        var second = Snapshot(DataSourceKind.Live, UtcNow);
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("first", new StubProvider(first)),
            new NamedUsageProvider("second", new StubProvider(second)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Same(first, result);
    }

    [Fact]
    public void WinningSnapshotIsReturnedWithItsOwnFieldsUnchanged()
    {
        var live = Snapshot(DataSourceKind.Live, UtcNow) with { UsageLevel = UsageLevel.Red };
        var composite = new CompositeUsageProvider([new NamedUsageProvider("only", new StubProvider(live))]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Equal(live, result);
        Assert.Equal(DataSourceKind.Live, result.DataSourceKind);
        Assert.Equal(UsageLevel.Red, result.UsageLevel);
    }

    [Fact]
    public void AllProvidersUnavailableYieldsUnavailable()
    {
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("a", new StubProvider(UsageSnapshot.Unavailable)),
            new NamedUsageProvider("b", new StubProvider(UsageSnapshot.Unavailable)),
        ]);

        var result = composite.GetSnapshot(UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, result);
    }

    [Fact]
    public void AllProvidersThrowingYieldsUnavailableAndNeverThrows()
    {
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("a", new ThrowingProvider(new InvalidOperationException("boom"))),
            new NamedUsageProvider("b", new ThrowingProvider(new InvalidOperationException("boom"))),
        ]);

        var exception = Record.Exception(() => composite.GetSnapshot(UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, composite.GetSnapshot(UtcNow));
    }

    [Fact]
    public void HealthDistinguishesOkNoDataAndFailedWithoutMerging()
    {
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("ok", new StubProvider(Snapshot(DataSourceKind.Live, UtcNow))),
            new NamedUsageProvider("nodata", new StubProvider(UsageSnapshot.Unavailable)),
            new NamedUsageProvider("failed", new ThrowingProvider(new InvalidOperationException("boom"))),
        ]);

        composite.GetSnapshot(UtcNow);

        Assert.Equal(ProviderHealthOutcome.Ok, composite.Health.Single(h => h.ProviderName == "ok").Outcome);
        Assert.Equal(ProviderHealthOutcome.NoData, composite.Health.Single(h => h.ProviderName == "nodata").Outcome);
        Assert.Equal(ProviderHealthOutcome.Failed, composite.Health.Single(h => h.ProviderName == "failed").Outcome);
    }

    [Fact]
    public void DegradedInputCountCountsOnlyFailedProviders()
    {
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("ok", new StubProvider(Snapshot(DataSourceKind.Live, UtcNow))),
            new NamedUsageProvider("nodata", new StubProvider(UsageSnapshot.Unavailable)),
            new NamedUsageProvider("failed1", new ThrowingProvider(new InvalidOperationException("boom"))),
            new NamedUsageProvider("failed2", new ThrowingProvider(new InvalidOperationException("boom"))),
        ]);

        composite.GetSnapshot(UtcNow);

        Assert.Equal(2, composite.DegradedInputCount);
    }

    [Fact]
    public void ConsecutiveFailuresAccumulatesAcrossPollsAndResetsOnSuccess()
    {
        var isFailing = true;
        var provider = new DelegateProvider(now => isFailing
            ? throw new InvalidOperationException("boom")
            : Snapshot(DataSourceKind.Live, now));
        var composite = new CompositeUsageProvider([new NamedUsageProvider("flaky", provider)]);

        composite.GetSnapshot(UtcNow);
        composite.GetSnapshot(UtcNow.AddMinutes(1));
        Assert.Equal(2, composite.Health.Single().ConsecutiveFailures);

        isFailing = false;
        composite.GetSnapshot(UtcNow.AddMinutes(2));

        Assert.Equal(0, composite.Health.Single().ConsecutiveFailures);
        Assert.Equal(ProviderHealthOutcome.Ok, composite.Health.Single().Outcome);
        Assert.Equal(UtcNow.AddMinutes(2), composite.Health.Single().LastSuccessAt);
    }

    [Fact]
    public void NoDataNeitherExtendsNorResetsAConsecutiveFailureStreak()
    {
        var mode = 0; // 0 = fail, 1 = fail, 2 = NoData
        var provider = new DelegateProvider(now => mode switch
        {
            2 => UsageSnapshot.Unavailable,
            _ => throw new InvalidOperationException("boom"),
        });
        var composite = new CompositeUsageProvider([new NamedUsageProvider("p", provider)]);

        composite.GetSnapshot(UtcNow);
        mode = 1;
        composite.GetSnapshot(UtcNow.AddMinutes(1));
        Assert.Equal(2, composite.Health.Single().ConsecutiveFailures);

        mode = 2;
        composite.GetSnapshot(UtcNow.AddMinutes(2));

        Assert.Equal(ProviderHealthOutcome.NoData, composite.Health.Single().Outcome);
        Assert.Equal(2, composite.Health.Single().ConsecutiveFailures);
    }

    [Fact]
    public void SwallowedProviderExceptionIsObservableThroughTheLogSeam()
    {
        var logged = new List<string>();
        var composite = new CompositeUsageProvider(
            [new NamedUsageProvider("boom-provider", new ThrowingProvider(new InvalidOperationException("simulated vendor read failure")))],
            log: logged.Add);

        composite.GetSnapshot(UtcNow);

        var line = Assert.Single(logged);
        Assert.Contains("boom-provider", line);
        Assert.Contains("simulated vendor read failure", line);
    }

    [Fact]
    public void NoLogDelegateSuppliedStillYieldsUnavailableRatherThanThrowing()
    {
        var composite = new CompositeUsageProvider([
            new NamedUsageProvider("boom-provider", new ThrowingProvider(new InvalidOperationException("boom"))),
        ]);

        var exception = Record.Exception(() => composite.GetSnapshot(UtcNow));

        Assert.Null(exception);
    }

    private sealed class DelegateProvider(Func<DateTimeOffset, UsageSnapshot> getSnapshot) : IUsageProvider
    {
        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow) => getSnapshot(utcNow);
    }
}
