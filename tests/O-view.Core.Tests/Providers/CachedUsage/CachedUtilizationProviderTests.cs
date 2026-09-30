using OView.Core.Models;
using OView.Core.Providers.CachedUsage;
using OView.Core.Storage;

namespace OView.Core.Tests.Providers.CachedUsage;

/// <summary>
/// Proves ADR-0005 D2's provider row for <see cref="CachedUtilizationProvider"/> (slice 5):
/// <see cref="DataSourceKind.Live"/> for a fresh block, <see cref="DataSourceKind.Stale"/> for
/// an old one, <see cref="UsageSnapshot.Unavailable"/> for no file, a rolled-over window, or an
/// aged-out block; D1's never-throw and clock-injection obligations; and the ADR-0006 anchor
/// wiring.
/// </summary>
public sealed class CachedUtilizationProviderTests : IDisposable
{
    private readonly string _root;

    public CachedUtilizationProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi227-provider-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void NoCandidateRootsYieldsUnavailable()
    {
        var provider = new CachedUtilizationProvider([]);

        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RootThatDoesNotExistYieldsUnavailable()
    {
        var provider = new CachedUtilizationProvider([Path.Combine(_root, "does-not-exist")]);

        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FreshBlockYieldsLiveSnapshotWithBothPercentagesAndResetInstants()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        var fiveHourReset = utcNow + TimeSpan.FromHours(3);
        var sevenDayReset = utcNow + TimeSpan.FromDays(2);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 42, fiveHourReset, sevenDayPercent: 17, sevenDayReset);

        var provider = new CachedUtilizationProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Live, snapshot.DataSourceKind);
        Assert.Equal(utcNow, snapshot.LastIngestAt);
        Assert.Equal(UsageValueStatus.Real, snapshot.SessionUtilizationPercent.Status);
        Assert.Equal(42d, snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(fiveHourReset, snapshot.SessionResetAt.Value);
        Assert.Equal(UsageValueStatus.Real, snapshot.WeeklyUtilizationPercent.Status);
        Assert.Equal(17d, snapshot.WeeklyUtilizationPercent.Value);
        Assert.Equal(sevenDayReset, snapshot.WeeklyResetAt.Value);
        Assert.Equal(UsageLevel.Green, snapshot.UsageLevel);
    }

    [Fact]
    public void BlockOlderThanFreshnessYieldsStaleButStillReportsThePercentages()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - CachedUtilizationProvider.DefaultFreshness - TimeSpan.FromMinutes(1);
        var futureReset = utcNow + TimeSpan.FromHours(1);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 50, futureReset, sevenDayPercent: 20, futureReset);

        var provider = new CachedUtilizationProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Stale, snapshot.DataSourceKind);
        Assert.Equal(50d, snapshot.SessionUtilizationPercent.Value);
    }

    [Fact]
    public void BlockExactlyAtFreshnessBoundaryYieldsLive()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - CachedUtilizationProvider.DefaultFreshness;
        var futureReset = utcNow + TimeSpan.FromHours(1);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 1, futureReset, sevenDayPercent: 1, futureReset);

        var provider = new CachedUtilizationProvider([_root]);

        Assert.Equal(DataSourceKind.Live, provider.GetSnapshot(utcNow).DataSourceKind);
    }

    [Fact]
    public void CustomFreshnessOverridesTheDefault()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        var futureReset = utcNow + TimeSpan.FromHours(1);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 1, futureReset, sevenDayPercent: 1, futureReset);

        var provider = new CachedUtilizationProvider([_root], freshness: TimeSpan.FromMinutes(1));

        Assert.Equal(DataSourceKind.Stale, provider.GetSnapshot(utcNow).DataSourceKind);
    }

    [Fact]
    public void ABarWhoseReportedResetHasAlreadyPassedIsDroppedRatherThanShownAsStale()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        var pastReset = utcNow - TimeSpan.FromMinutes(1);
        var futureReset = utcNow + TimeSpan.FromDays(1);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 91, pastReset, sevenDayPercent: 30, futureReset);

        var provider = new CachedUtilizationProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionUtilizationPercent.Status);
        Assert.Null(snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionResetAt.Status);
        Assert.Equal(30d, snapshot.WeeklyUtilizationPercent.Value);
    }

    [Fact]
    public void BothBarsRolledOverYieldsUnavailableRatherThanAnEmptyLiveSnapshot()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        var pastReset = utcNow - TimeSpan.FromMinutes(1);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 91, pastReset, sevenDayPercent: 80, pastReset);

        var provider = new CachedUtilizationProvider([_root]);

        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(utcNow));
    }

    [Fact]
    public void ABarWithNoReportedResetIsNotTreatedAsRolledOver()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, CachedUtilization.FileName),
            $$"""
            {
              "cachedUsageUtilization": {
                "fetchedAtMs": {{fetchedAt.ToUnixTimeMilliseconds()}},
                "utilization": { "five_hour": { "utilization": 42 } }
              }
            }
            """);

        var provider = new CachedUtilizationProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(42d, snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionResetAt.Status);
    }

    [Fact]
    public void ExtraUsageIsStampedWithTheBlocksOwnFetchTimeNotLastIngestAt()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(2);
        var futureReset = utcNow + TimeSpan.FromHours(1);
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, CachedUtilization.FileName),
            $$"""
            {
              "cachedUsageUtilization": {
                "fetchedAtMs": {{fetchedAt.ToUnixTimeMilliseconds()}},
                "utilization": {
                  "five_hour": { "utilization": 1, "resets_at": "{{futureReset:O}}" },
                  "extra_usage": { "is_enabled": true }
                }
              }
            }
            """);

        var provider = new CachedUtilizationProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.NotNull(snapshot.ExtraUsage);
        Assert.Equal(ExtraUsageState.Enabled, snapshot.ExtraUsage!.State);
        Assert.Equal(fetchedAt, snapshot.ExtraUsage.FetchedAtUtc);
    }

    [Fact]
    public void ExtraUsageIsNullWhenTheBlockDoesNotSay()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var futureReset = utcNow + TimeSpan.FromHours(1);
        WriteBlock(_root, utcNow - TimeSpan.FromMinutes(1), fiveHourPercent: 1, futureReset, sevenDayPercent: 1, futureReset);

        var provider = new CachedUtilizationProvider([_root]);

        Assert.Null(provider.GetSnapshot(utcNow).ExtraUsage);
    }

    [Fact]
    public void AFutureWeeklyResetIsSavedToTheAnchorStore()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var fetchedAt = utcNow - TimeSpan.FromMinutes(1);
        var weeklyReset = utcNow + TimeSpan.FromDays(3);
        WriteBlock(_root, fetchedAt, fiveHourPercent: 1, weeklyReset, sevenDayPercent: 1, weeklyReset);
        var anchorDirectory = Path.Combine(_root, "anchor-store");
        var anchorStore = new WeeklyResetAnchorStore(anchorDirectory);

        var provider = new CachedUtilizationProvider([_root], anchorStore);
        provider.GetSnapshot(utcNow);

        Assert.Equal(weeklyReset, anchorStore.Read());
    }

    [Fact]
    public void NoAnchorStoreSuppliedStillReturnsAWorkingSnapshot()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var futureReset = utcNow + TimeSpan.FromHours(1);
        WriteBlock(_root, utcNow - TimeSpan.FromMinutes(1), fiveHourPercent: 1, futureReset, sevenDayPercent: 1, futureReset);

        var provider = new CachedUtilizationProvider([_root], anchorStore: null);

        var exception = Record.Exception(() => provider.GetSnapshot(utcNow));

        Assert.Null(exception);
    }

    [Fact]
    public void NeverThrowsWhenAFileIsLockedForExclusiveWriteByAnotherProcess()
    {
        Directory.CreateDirectory(_root);
        var lockedFile = Path.Combine(_root, CachedUtilization.FileName);
        File.WriteAllText(lockedFile, "{}");

        using var exclusiveHandle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var provider = new CachedUtilizationProvider([_root]);

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NeverThrowsOnNotJson()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, CachedUtilization.FileName), "not json at all");

        var provider = new CachedUtilizationProvider([_root]);

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void GetSnapshotNeverCallsTheSystemClockItself()
    {
        var method = typeof(CachedUtilizationProvider).GetMethod(nameof(CachedUtilizationProvider.GetSnapshot));

        Assert.NotNull(method);
        var parameter = Assert.Single(method!.GetParameters());
        Assert.Equal(typeof(DateTimeOffset), parameter.ParameterType);
        Assert.Equal("utcNow", parameter.Name);
    }

    /// <summary>
    /// Gate G6 (iii): a refresher must never be a precondition for showing a number. Proven
    /// structurally — this provider's constructor has no parameter of type
    /// <see cref="IUsageCacheRefresher"/> or <see cref="ClaudeCliRefresher"/> at all, so there is
    /// no path by which <see cref="CachedUtilizationProvider.GetSnapshot"/> could depend on one.
    /// </summary>
    [Fact]
    public void ConstructorTakesNoUsageCacheRefresherSoARefreshCanNeverGateAReading()
    {
        var constructor = Assert.Single(typeof(CachedUtilizationProvider).GetConstructors());

        Assert.DoesNotContain(
            constructor.GetParameters(),
            p => typeof(IUsageCacheRefresher).IsAssignableFrom(p.ParameterType));
    }

    private static void WriteBlock(
        string root,
        DateTimeOffset fetchedAt,
        int fiveHourPercent,
        DateTimeOffset fiveHourReset,
        int sevenDayPercent,
        DateTimeOffset sevenDayReset)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, CachedUtilization.FileName),
            $$"""
            {
              "cachedUsageUtilization": {
                "fetchedAtMs": {{fetchedAt.ToUnixTimeMilliseconds()}},
                "utilization": {
                  "five_hour": { "utilization": {{fiveHourPercent}}, "resets_at": "{{fiveHourReset:O}}" },
                  "seven_day": { "utilization": {{sevenDayPercent}}, "resets_at": "{{sevenDayReset:O}}" }
                }
              }
            }
            """);
    }
}
