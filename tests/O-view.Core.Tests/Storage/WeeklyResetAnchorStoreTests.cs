using OView.Core.Storage;

namespace OView.Core.Tests.Storage;

/// <summary>
/// Proves ADR-0006 slice 1's contract: a directory-injected store (D2) that writes
/// atomically and never throws on a corrupt or missing file (D3).
/// </summary>
public sealed class WeeklyResetAnchorStoreTests : IDisposable
{
    private readonly string _directory;

    public WeeklyResetAnchorStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi215-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ReadReturnsNullWhenNoFileHasEverBeenWritten()
    {
        var store = new WeeklyResetAnchorStore(_directory);

        Assert.Null(store.Read());
    }

    [Fact]
    public void ReadReturnsNullWhenTheDirectoryItselfDoesNotExist()
    {
        var store = new WeeklyResetAnchorStore(Path.Combine(_directory, "does-not-exist"));

        Assert.Null(store.Read());
    }

    [Fact]
    public void ReadReturnsNullAndNeverThrowsWhenTheFileIsUnparseableJson()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "weekly-reset.json"), "{ this is not valid json");

        var store = new WeeklyResetAnchorStore(_directory);

        Assert.Null(store.Read());
    }

    [Fact]
    public void ReadReturnsNullAndNeverThrowsWhenTheAnchorFieldIsMissing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "weekly-reset.json"), "{ \"version\": 1 }");

        var store = new WeeklyResetAnchorStore(_directory);

        Assert.Null(store.Read());
    }

    [Fact]
    public void ReadReturnsNullAndNeverThrowsWhenTheAnchorFieldIsNotAParseableTimestamp()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "weekly-reset.json"),
            "{ \"version\": 1, \"anchorUtc\": \"not-a-timestamp\" }");

        var store = new WeeklyResetAnchorStore(_directory);

        Assert.Null(store.Read());
    }

    [Fact]
    public void SaveThenReadRoundTripsTheSameInstant()
    {
        var store = new WeeklyResetAnchorStore(_directory);
        var anchor = new DateTimeOffset(2026, 9, 25, 14, 0, 0, TimeSpan.Zero);

        var saved = store.Save(anchor);

        Assert.True(saved);
        Assert.Equal(anchor, store.Read());
    }

    [Fact]
    public void SaveCreatesTheDirectoryWhenItDoesNotAlreadyExist()
    {
        var nested = Path.Combine(_directory, "nested", "path");
        var store = new WeeklyResetAnchorStore(nested);

        var saved = store.Save(DateTimeOffset.UtcNow);

        Assert.True(saved);
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void SaveOverwritesAPreviouslyStoredAnchor()
    {
        var store = new WeeklyResetAnchorStore(_directory);
        var first = new DateTimeOffset(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);
        var second = new DateTimeOffset(2026, 9, 25, 14, 0, 0, TimeSpan.Zero);

        store.Save(first);
        store.Save(second);

        Assert.Equal(second, store.Read());
    }

    [Fact]
    public void SaveLeavesNoTempFileBehindAfterASuccessfulWrite()
    {
        var store = new WeeklyResetAnchorStore(_directory);

        store.Save(DateTimeOffset.UtcNow);

        Assert.False(File.Exists(Path.Combine(_directory, "weekly-reset.json.tmp")));
    }

    [Fact]
    public void SaveRecoversTheStoredAnchorAfterAPreviousCorruptFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "weekly-reset.json"), "{ not valid json at all");
        var store = new WeeklyResetAnchorStore(_directory);
        var anchor = new DateTimeOffset(2026, 9, 25, 14, 0, 0, TimeSpan.Zero);

        store.Save(anchor);

        Assert.Equal(anchor, store.Read());
    }
}
