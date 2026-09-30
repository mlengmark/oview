using OView.Core.Models;
using OView.Core.Storage;

namespace OView.Core.Tests.Storage;

/// <summary>
/// Proves ADR-0006 slice 4's contract: a directory-injected store (D2) that writes the
/// per-provider ingest audit trail atomically and never throws on a corrupt or missing file
/// (D3).
/// </summary>
public sealed class IngestAuditStoreTests : IDisposable
{
    private readonly string _directory;

    public IngestAuditStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi252-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ReadAllReturnsEmptyWhenNoFileHasEverBeenWritten()
    {
        var store = new IngestAuditStore(_directory);

        Assert.Empty(store.ReadAll());
    }

    [Fact]
    public void ReadAllReturnsEmptyWhenTheDirectoryItselfDoesNotExist()
    {
        var store = new IngestAuditStore(Path.Combine(_directory, "does-not-exist"));

        Assert.Empty(store.ReadAll());
    }

    [Fact]
    public void ReadAllReturnsEmptyAndNeverThrowsWhenTheFileIsUnparseableJson()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "ingest-audit.json"), "{ this is not valid json");

        var store = new IngestAuditStore(_directory);

        Assert.Empty(store.ReadAll());
    }

    [Fact]
    public void ReadAllReturnsEmptyAndNeverThrowsWhenTheProvidersFieldIsMissing()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "ingest-audit.json"), "{ \"version\": 1 }");

        var store = new IngestAuditStore(_directory);

        Assert.Empty(store.ReadAll());
    }

    [Fact]
    public void SaveThenReadAllRoundTripsEveryProvidersRecord()
    {
        var store = new IngestAuditStore(_directory);
        var records = new Dictionary<string, IngestAuditRecord>
        {
            ["a"] = new IngestAuditRecord(new DateTimeOffset(2026, 9, 25, 14, 0, 0, TimeSpan.Zero), 0),
            ["b"] = new IngestAuditRecord(null, 3),
        };

        var saved = store.Save(records);
        var result = store.ReadAll();

        Assert.True(saved);
        Assert.Equal(records["a"], result["a"]);
        Assert.Equal(records["b"], result["b"]);
    }

    [Fact]
    public void SaveCreatesTheDirectoryWhenItDoesNotAlreadyExist()
    {
        var nested = Path.Combine(_directory, "nested", "path");
        var store = new IngestAuditStore(nested);

        var saved = store.Save(new Dictionary<string, IngestAuditRecord>());

        Assert.True(saved);
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void SaveOverwritesThePreviouslyStoredTrail()
    {
        var store = new IngestAuditStore(_directory);
        store.Save(new Dictionary<string, IngestAuditRecord> { ["a"] = new IngestAuditRecord(null, 1) });
        store.Save(new Dictionary<string, IngestAuditRecord> { ["a"] = new IngestAuditRecord(null, 2) });

        var result = store.ReadAll();

        Assert.Equal(2, result["a"].ConsecutiveFailures);
    }

    [Fact]
    public void SaveLeavesNoTempFileBehindAfterASuccessfulWrite()
    {
        var store = new IngestAuditStore(_directory);

        store.Save(new Dictionary<string, IngestAuditRecord>());

        Assert.False(File.Exists(Path.Combine(_directory, "ingest-audit.json.tmp")));
    }

    [Fact]
    public void StateIsOkBeforeAnyCorruptionIsEverEncountered()
    {
        var store = new IngestAuditStore(_directory);

        store.Save(new Dictionary<string, IngestAuditRecord>());
        store.ReadAll();

        Assert.Equal(HistoryStoreState.Ok, store.State);
    }

    [Fact]
    public void ReadAllMovesAnUnparseableFileAsideRatherThanLeavingOrDeletingIt()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "ingest-audit.json");
        File.WriteAllText(path, "{ this is not valid json");
        var store = new IngestAuditStore(_directory);

        store.ReadAll();

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".corrupt"));
        Assert.Equal("{ this is not valid json", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void ReadAllReportsRebuiltAfterMovingACorruptFileAside()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "ingest-audit.json"), "not json at all");
        var store = new IngestAuditStore(_directory);

        store.ReadAll();

        Assert.Equal(HistoryStoreState.Rebuilt, store.State);
    }

    [Fact]
    public void ReadAllReportsUnavailableWhenTheCorruptFileCannotBeMovedAside()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "ingest-audit.json");
        File.WriteAllText(path, "{ this is not valid json");
        // Occupy the exact backup destination with a directory: File.Move onto an existing
        // directory always fails, on every platform, regardless of permissions — the
        // deterministic way to force the "still can't be recovered" branch in a test.
        Directory.CreateDirectory(path + ".corrupt");
        var store = new IngestAuditStore(_directory);

        var result = store.ReadAll();

        Assert.Empty(result);
        Assert.Equal(HistoryStoreState.Unavailable, store.State);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveRecoversAfterAPreviousCorruptFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "ingest-audit.json"), "{ not valid json at all");
        var store = new IngestAuditStore(_directory);
        var records = new Dictionary<string, IngestAuditRecord> { ["a"] = new IngestAuditRecord(null, 1) };

        store.Save(records);

        Assert.Equal(records["a"], store.ReadAll()["a"]);
    }
}
