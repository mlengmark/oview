using OView.Core.Models;
using OView.Core.Storage;

namespace OView.Core.Tests.Storage;

/// <summary>
/// Proves <see cref="CorruptStoreRecovery.MoveAsideIfPresent"/>'s three-way contract in
/// isolation from either store that calls it (OVI-238, a follow-up from the PR #45 review of
/// OVI-236): <see cref="HistoryStoreState.Rebuilt"/> means a file actually existed and was
/// moved aside, never a no-op success.
/// </summary>
public sealed class CorruptStoreRecoveryTests : IDisposable
{
    private readonly string _directory;

    public CorruptStoreRecoveryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi238-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ReportsOkWithoutMovingAnythingWhenNoFileExistsAtThePath()
    {
        var path = Path.Combine(_directory, "absent.db");

        var state = CorruptStoreRecovery.MoveAsideIfPresent(path);

        Assert.Equal(HistoryStoreState.Ok, state);
        Assert.False(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void ReportsRebuiltAndMovesTheFileWhenOneIsPresent()
    {
        var path = Path.Combine(_directory, "present.db");
        File.WriteAllText(path, "corrupt contents");

        var state = CorruptStoreRecovery.MoveAsideIfPresent(path);

        Assert.Equal(HistoryStoreState.Rebuilt, state);
        Assert.False(File.Exists(path));
        Assert.Equal("corrupt contents", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void ReportsUnavailableWhenThePresentFileCannotBeMovedAside()
    {
        var path = Path.Combine(_directory, "present.db");
        File.WriteAllText(path, "corrupt contents");
        // File.Move onto an existing directory always fails, on every platform, regardless of
        // permissions — the deterministic, OS-agnostic way to force this branch in a test.
        Directory.CreateDirectory(path + ".corrupt");

        var state = CorruptStoreRecovery.MoveAsideIfPresent(path);

        Assert.Equal(HistoryStoreState.Unavailable, state);
        Assert.True(File.Exists(path));
    }
}
