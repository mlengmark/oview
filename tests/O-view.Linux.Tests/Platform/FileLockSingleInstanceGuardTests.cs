using OView.Linux.Platform;

namespace OView.Linux.Tests.Platform;

/// <summary>
/// The Linux single-instance mechanism. The property that matters is the one a named mutex
/// on Unix does not give: the claim is released when the process dies, however it dies. That
/// cannot be tested without killing a process, so what is tested here is everything around
/// it, plus the release-on-dispose behaviour that stands in for it. This is ordinary file IO
/// (no Linux-only API), so it runs wherever <c>dotnet test</c> runs — it does not confirm
/// behaviour on a real Linux session, which this repository has never had a runner to observe.
/// </summary>
public class FileLockSingleInstanceGuardTests : IDisposable
{
    private readonly string _directory;

    public FileLockSingleInstanceGuardTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "oview-filelock-guard-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string LockPath => Path.Combine(_directory, "o-view.lock");

    [Fact]
    public void FirstInstanceAcquires()
    {
        using var guard = new FileLockSingleInstanceGuard(LockPath);

        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void SecondInstanceIsRefusedWhileTheFirstHolds()
    {
        using var first = new FileLockSingleInstanceGuard(LockPath);
        Assert.True(first.TryAcquire());

        using var second = new FileLockSingleInstanceGuard(LockPath);
        Assert.False(second.TryAcquire());
    }

    /// <summary>
    /// Releasing must let the next instance in. A named mutex on Unix can outlive its owner;
    /// this is the behaviour that makes the lock file the right mechanism instead.
    /// </summary>
    [Fact]
    public void ReleasingLetsTheNextInstanceIn()
    {
        var first = new FileLockSingleInstanceGuard(LockPath);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var second = new FileLockSingleInstanceGuard(LockPath);
        Assert.True(second.TryAcquire());
    }

    /// <summary>
    /// A lock file left behind by a killed process must not, on its own, look like a live
    /// instance — the file's existence means nothing, only the lock does.
    /// </summary>
    [Fact]
    public void AStaleFileWithNoHolderDoesNotBlockStartup()
    {
        File.WriteAllText(LockPath, "999999");   // a pid that is not running

        using var guard = new FileLockSingleInstanceGuard(LockPath);

        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void AcquiringTwiceFromTheSameGuardIsNotASecondInstance()
    {
        using var guard = new FileLockSingleInstanceGuard(LockPath);

        Assert.True(guard.TryAcquire());
        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void CreatesTheLockDirectoryWhenAbsent()
    {
        var nested = Path.Combine(_directory, "run", "user", "1000", "o-view.lock");

        using var guard = new FileLockSingleInstanceGuard(nested);

        Assert.True(guard.TryAcquire());
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void DisposingWithoutAcquiringIsSafe()
    {
        // The losing instance shuts down through the same path as the winner, so Dispose has
        // to tolerate never having held anything.
        var guard = new FileLockSingleInstanceGuard(LockPath);
        guard.Dispose();
    }

    [Fact]
    public void DefaultPathPrefersXdgRuntimeDir()
    {
        var original = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", Path.Combine(Path.GetTempPath(), "xdg-probe"));
            Assert.Contains("xdg-probe", FileLockSingleInstanceGuard.DefaultPath, StringComparison.Ordinal);

            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", null);
            Assert.EndsWith("o-view.lock", FileLockSingleInstanceGuard.DefaultPath, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", original);
        }
    }
}
