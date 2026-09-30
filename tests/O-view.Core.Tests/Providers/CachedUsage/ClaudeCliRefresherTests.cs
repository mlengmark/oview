using OView.Core.Providers.CachedUsage;

namespace OView.Core.Tests.Providers.CachedUsage;

/// <summary>
/// Proves <see cref="ClaudeCliRefresher"/> against gate G6's three limits (ADR-0005,
/// board-answered 2026-09-28) and its own never-throw obligation. No test here spawns a real
/// process — <see cref="ClaudeCliRefresher.ProcessRun"/> is injected throughout, the same
/// pattern <see cref="CachedUtilizationProviderTests"/> uses for file I/O.
/// </summary>
public sealed class ClaudeCliRefresherTests : IDisposable
{
    private readonly string _root;

    public ClaudeCliRefresherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi227-refresher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void UsageArgumentIsExactlyTheDocumentedSlashCommand()
    {
        // Gate G6 (i): the vendor's own documented, read-shaped command, and nothing else.
        Assert.Equal("/usage", ClaudeCliRefresher.UsageArgument);
    }

    [Fact]
    public void RefreshedWhenTheBlocksFetchTimeAdvances()
    {
        WriteBlock(fetchedAtMs: 1000);
        var refresher = new ClaudeCliRefresher([_root], _ =>
        {
            WriteBlock(fetchedAtMs: 2000);
            return new ClaudeCliRefresher.ProcessRun(Started: true, Exited: true, ExitCode: 0);
        });

        var result = refresher.Refresh();

        Assert.Equal(RefreshOutcome.Refreshed, result.Outcome);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RefreshedWhenABlockAppearsWhereTherWasNoneBefore()
    {
        var refresher = new ClaudeCliRefresher([_root], _ =>
        {
            WriteBlock(fetchedAtMs: 1000);
            return new ClaudeCliRefresher.ProcessRun(Started: true, Exited: true, ExitCode: 0);
        });

        var result = refresher.Refresh();

        Assert.Equal(RefreshOutcome.Refreshed, result.Outcome);
    }

    [Fact]
    public void UnchangedWhenTheBlockDoesNotMove()
    {
        WriteBlock(fetchedAtMs: 1000);
        var refresher = new ClaudeCliRefresher([_root], _ => new ClaudeCliRefresher.ProcessRun(true, true, 0));

        var result = refresher.Refresh();

        Assert.Equal(RefreshOutcome.Unchanged, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void NotFoundWhenTheExecutableDidNotStart()
    {
        var refresher = new ClaudeCliRefresher([_root], _ => new ClaudeCliRefresher.ProcessRun(false, false, 0, "Win32Exception"));

        var result = refresher.Refresh();

        // Gate G6 (iii): no claude on PATH is not a failure this class escalates — a machine
        // where the refresh does nothing must still work via the cached block alone.
        Assert.Equal(RefreshOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void TimedOutWhenTheProcessNeverExits()
    {
        var refresher = new ClaudeCliRefresher([_root], _ => new ClaudeCliRefresher.ProcessRun(true, false, 0));

        var result = refresher.Refresh();

        Assert.Equal(RefreshOutcome.TimedOut, result.Outcome);
    }

    [Fact]
    public void FailedWhenTheProcessExitsNonZero()
    {
        var refresher = new ClaudeCliRefresher([_root], _ => new ClaudeCliRefresher.ProcessRun(true, true, 1));

        var result = refresher.Refresh();

        Assert.Equal(RefreshOutcome.Failed, result.Outcome);
        Assert.Equal("exit 1", result.Detail);
    }

    [Fact]
    public void NeverThrowsWhenTheRunDelegateItselfThrows()
    {
        var refresher = new ClaudeCliRefresher([_root], _ => throw new InvalidOperationException("boom"));

        var exception = Record.Exception(() => refresher.Refresh());
        var result = refresher.Refresh();

        Assert.Null(exception);
        Assert.Equal(RefreshOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void NeverThrowsWhenTheCandidateFileIsUnreadable()
    {
        File.WriteAllText(Path.Combine(_root, CachedUtilization.FileName), "not json at all");
        var refresher = new ClaudeCliRefresher([_root], _ => new ClaudeCliRefresher.ProcessRun(true, true, 0));

        var exception = Record.Exception(() => refresher.Refresh());

        Assert.Null(exception);
    }

    [Fact]
    public void ResolveWorkingDirectoryPrefersThePreferredDirectoryWhenItExists()
    {
        Assert.Equal(_root, ClaudeCliRefresher.ResolveWorkingDirectory(_root, "fallback-does-not-matter"));
    }

    [Fact]
    public void ResolveWorkingDirectoryFallsBackWhenThePreferredDirectoryDoesNotExist()
    {
        var fallback = Path.Combine(_root, "fallback");
        Directory.CreateDirectory(fallback);

        var resolved = ClaudeCliRefresher.ResolveWorkingDirectory(Path.Combine(_root, "does-not-exist"), fallback);

        Assert.Equal(fallback, resolved);
    }

    /// <summary>
    /// Gate G6 (ii): never passed arguments that mutate vendor state. Proven at the type's
    /// only public entry point for arguments — <see cref="ClaudeCliRefresher.UsageArgument"/> is
    /// the sole constant this class exposes for building a command line, and it is read-only
    /// data, not an API a caller could extend with a second argument.
    /// </summary>
    [Fact]
    public void NoPublicMemberOnThisTypeAcceptsAnArbitraryArgumentList()
    {
        var members = typeof(ClaudeCliRefresher).GetMembers(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        foreach (var member in members)
        {
            if (member is System.Reflection.MethodInfo method)
            {
                Assert.DoesNotContain(
                    method.GetParameters(),
                    p => p.ParameterType == typeof(string[]) || p.ParameterType == typeof(IEnumerable<string>));
            }
        }
    }

    private void WriteBlock(long fetchedAtMs) =>
        File.WriteAllText(
            Path.Combine(_root, CachedUtilization.FileName),
            $$"""{ "cachedUsageUtilization": { "fetchedAtMs": {{fetchedAtMs}} } }""");
}
