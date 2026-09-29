using OView.Core.Models;
using OView.Core.Providers;
using OView.Core.Providers.PlanHistory;

namespace OView.Core.Tests.Providers.PlanHistory;

/// <summary>
/// Proves ADR-0005 D2's provider row for <see cref="PlanHistoryProvider"/> (slice 4):
/// <see cref="DataSourceKind.Live"/> for a fresh sample, <see cref="DataSourceKind.Stale"/>
/// for an old one, <see cref="UsageSnapshot.Unavailable"/> for no file or no valid sample,
/// and D1's never-throw obligation.
/// </summary>
public sealed class PlanHistoryProviderTests : IDisposable
{
    private readonly string _root;

    public PlanHistoryProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi222-" + Guid.NewGuid().ToString("N"));
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
        var provider = new PlanHistoryProvider([]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void RootThatDoesNotExistYieldsUnavailable()
    {
        var provider = new PlanHistoryProvider([Path.Combine(_root, "does-not-exist")]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void FileWithNoSamplesArrayYieldsUnavailable()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "plan-usage-history.json"), "{\"version\":2}");

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void FileWithOnlyMalformedSamplesYieldsUnavailable()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "plan-usage-history.json"),
            "{\"samples\":[{\"t\":\"not-a-number\",\"org\":\"o1\",\"u\":{\"fh\":10,\"sd\":5}},{\"org\":\"o1\",\"u\":{\"fh\":10,\"sd\":5}}]}");

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void FreshSampleYieldsLiveSnapshotWithItsPercentages()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var sampleAt = utcNow - TimeSpan.FromMinutes(5);
        WriteSamplesFile(sampleAt, fiveHour: 42, sevenDay: 17);

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Live, snapshot.DataSourceKind);
        Assert.Equal(utcNow, snapshot.LastIngestAt);
        Assert.Equal(UsageValueStatus.Real, snapshot.SessionUtilizationPercent.Status);
        Assert.Equal(42d, snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Real, snapshot.WeeklyUtilizationPercent.Status);
        Assert.Equal(17d, snapshot.WeeklyUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionResetAt.Status);
        Assert.Null(snapshot.SessionResetAt.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.WeeklyResetAt.Status);
        Assert.Null(snapshot.WeeklyResetAt.Value);
        Assert.Equal(UsageLevel.Green, snapshot.UsageLevel);
        Assert.Null(snapshot.ExtraUsage);
    }

    [Fact]
    public void SampleOlderThanFreshnessYieldsStale()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var sampleAt = utcNow - PlanHistoryProvider.DefaultFreshness - TimeSpan.FromMinutes(1);
        WriteSamplesFile(sampleAt, fiveHour: 42, sevenDay: 17);

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Stale, snapshot.DataSourceKind);
        Assert.Equal(42d, snapshot.SessionUtilizationPercent.Value);
    }

    [Fact]
    public void SampleExactlyAtFreshnessBoundaryYieldsLive()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var sampleAt = utcNow - PlanHistoryProvider.DefaultFreshness;
        WriteSamplesFile(sampleAt, fiveHour: 1, sevenDay: 1);

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Live, snapshot.DataSourceKind);
    }

    [Fact]
    public void CustomFreshnessOverridesTheDefault()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var sampleAt = utcNow - TimeSpan.FromMinutes(2);
        WriteSamplesFile(sampleAt, fiveHour: 1, sevenDay: 1);

        var provider = new PlanHistoryProvider([_root], freshness: TimeSpan.FromMinutes(1));

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Stale, snapshot.DataSourceKind);
    }

    [Fact]
    public void NewestSampleWinsWhenTheFileHasSeveral()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var older = (utcNow - TimeSpan.FromMinutes(30)).ToUnixTimeMilliseconds();
        var newer = (utcNow - TimeSpan.FromMinutes(2)).ToUnixTimeMilliseconds();
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "plan-usage-history.json"),
            "{\"samples\":[" +
            $"{{\"t\":{older},\"org\":\"o1\",\"u\":{{\"fh\":5,\"sd\":5}}}}," +
            $"{{\"t\":{newer},\"org\":\"o1\",\"u\":{{\"fh\":80,\"sd\":40}}}}" +
            "]}");

        var provider = new PlanHistoryProvider([_root]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Live, snapshot.DataSourceKind);
        Assert.Equal(80d, snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(40d, snapshot.WeeklyUtilizationPercent.Value);
    }

    [Fact]
    public void EarlierCandidateRootWinsWhenItAlreadyHasAValidSample()
    {
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        var firstRoot = Path.Combine(_root, "first");
        var secondRoot = Path.Combine(_root, "second");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        var epochMs = (utcNow - TimeSpan.FromMinutes(1)).ToUnixTimeMilliseconds();
        File.WriteAllText(
            Path.Combine(firstRoot, "plan-usage-history.json"),
            $"{{\"samples\":[{{\"t\":{epochMs},\"org\":\"o1\",\"u\":{{\"fh\":9,\"sd\":9}}}}]}}");
        // second root deliberately has no file at all.

        var provider = new PlanHistoryProvider([firstRoot, secondRoot]);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.Live, snapshot.DataSourceKind);
        Assert.Equal(9d, snapshot.SessionUtilizationPercent.Value);
    }

    [Fact]
    public void NeverThrowsWhenAFileIsLockedForExclusiveWriteByAnotherProcess()
    {
        Directory.CreateDirectory(_root);
        var lockedFile = Path.Combine(_root, "plan-usage-history.json");
        File.WriteAllText(lockedFile, "{\"samples\":[]}");

        using var exclusiveHandle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var provider = new PlanHistoryProvider([_root]);

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NeverThrowsOnNotJson()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "plan-usage-history.json"), "not json at all");

        var provider = new PlanHistoryProvider([_root]);

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    private void WriteSamplesFile(DateTimeOffset sampleAt, int fiveHour, int sevenDay)
    {
        Directory.CreateDirectory(_root);
        var epochMs = sampleAt.ToUnixTimeMilliseconds();
        File.WriteAllText(
            Path.Combine(_root, "plan-usage-history.json"),
            $"{{\"samples\":[{{\"t\":{epochMs},\"org\":\"org-1\",\"u\":{{\"fh\":{fiveHour},\"sd\":{sevenDay}}}}}]}}");
    }
}
