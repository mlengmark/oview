using OView.Core.Models;
using OView.Core.Providers;
using OView.Core.Providers.Jsonl;

namespace OView.Core.Tests.Providers.Jsonl;

/// <summary>
/// Proves ADR-0005 D6a's field table and D1's never-throw obligation for
/// <see cref="JsonlUsageProvider"/> (slice 3b). No test here asserts a meter value — D6a's
/// whole point is that this provider never produces one.
/// </summary>
public sealed class JsonlUsageProviderTests : IDisposable
{
    private readonly string _root;

    public JsonlUsageProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi219-" + Guid.NewGuid().ToString("N"));
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
        var provider = new JsonlUsageProvider([]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void RootThatDoesNotExistYieldsUnavailable()
    {
        var provider = new JsonlUsageProvider([Path.Combine(_root, "does-not-exist")]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void RootWithOnlyUnparseableLinesYieldsUnavailable()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllLines(Path.Combine(_root, "session.jsonl"), ["not json at all", "{\"type\":\"user\"}"]);

        var provider = new JsonlUsageProvider([_root]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(UsageSnapshot.Unavailable, snapshot);
    }

    [Fact]
    public void ReadableTranscriptRecordYieldsJsonlFallbackSnapshotShapedExactlyPerD6a()
    {
        var projectDirectory = Path.Combine(_root, "projects", "some-project");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllLines(Path.Combine(projectDirectory, "session.jsonl"), [AssistantLine()]);

        var provider = new JsonlUsageProvider([_root]);
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(DataSourceKind.JsonlFallback, snapshot.DataSourceKind);
        Assert.Equal(utcNow, snapshot.LastIngestAt);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionUtilizationPercent.Status);
        Assert.Null(snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.WeeklyUtilizationPercent.Status);
        Assert.Null(snapshot.WeeklyUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionResetAt.Status);
        Assert.Null(snapshot.SessionResetAt.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.WeeklyResetAt.Status);
        Assert.Null(snapshot.WeeklyResetAt.Value);
        Assert.Equal(UsageLevel.Green, snapshot.UsageLevel);
        Assert.Null(snapshot.ExtraUsage);
    }

    [Fact]
    public void LastIngestAtIsTheInjectedClockNotAnyActivityTimestampFromTheTranscript()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllLines(
            Path.Combine(_root, "session.jsonl"),
            [AssistantLine(timestamp: "2020-01-01T00:00:00.000Z")]);

        var provider = new JsonlUsageProvider([_root]);
        var utcNow = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);

        var snapshot = provider.GetSnapshot(utcNow);

        Assert.Equal(utcNow, snapshot.LastIngestAt);
        Assert.NotEqual(DateTimeOffset.Parse("2020-01-01T00:00:00.000Z"), snapshot.LastIngestAt);
    }

    [Fact]
    public void EarlierCandidateRootWinsWhenItAlreadyHasAReadableRecord()
    {
        var firstRoot = Path.Combine(_root, "first");
        var secondRoot = Path.Combine(_root, "second");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        File.WriteAllLines(Path.Combine(firstRoot, "session.jsonl"), [AssistantLine()]);
        // second root deliberately has no transcripts.

        var provider = new JsonlUsageProvider([firstRoot, secondRoot]);

        var snapshot = provider.GetSnapshot(DateTimeOffset.UtcNow);

        Assert.Equal(DataSourceKind.JsonlFallback, snapshot.DataSourceKind);
    }

    [Fact]
    public void NeverThrowsWhenAFileIsLockedForExclusiveWriteByAnotherProcess()
    {
        Directory.CreateDirectory(_root);
        var lockedFile = Path.Combine(_root, "session.jsonl");
        File.WriteAllText(lockedFile, AssistantLine() + Environment.NewLine);

        using var exclusiveHandle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var provider = new JsonlUsageProvider([_root]);

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    private static string AssistantLine(
        string requestId = "req_1",
        string timestamp = "2026-07-20T12:58:16.640Z",
        string model = "claude-opus-4-8") =>
        "{\"type\":\"assistant\",\"requestId\":\"" + requestId + "\",\"timestamp\":\"" + timestamp + "\"," +
        "\"message\":{\"model\":\"" + model + "\",\"usage\":{" +
        "\"input_tokens\":2,\"output_tokens\":120," +
        "\"cache_creation_input_tokens\":14226,\"cache_read_input_tokens\":25061}}}";
}
