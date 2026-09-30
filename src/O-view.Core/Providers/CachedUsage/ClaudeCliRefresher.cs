using System.ComponentModel;
using System.Diagnostics;

namespace OView.Core.Providers.CachedUsage;

/// <summary>What an attempt to refresh Claude Code's usage cache achieved.</summary>
public enum RefreshOutcome
{
    /// <summary>The block's fetch timestamp advanced, or a block appeared where there was none.
    /// The only success.</summary>
    Refreshed,

    /// <summary>Claude Code ran and exited cleanly, but the block did not move — ordinary:
    /// Claude Code serves a cached answer to a second ask within its own freshness window.</summary>
    Unchanged,

    /// <summary>No <c>claude</c> on PATH. Not an error — most machines do not have one
    /// (gate G6 (iii): a machine where the refresh does nothing must still work via the cached
    /// block alone).</summary>
    NotFound,

    /// <summary>Killed at the timeout. Usually a login prompt waiting on input nobody will give.</summary>
    TimedOut,

    /// <summary>Ran and failed, or could not be launched for a reason other than "not found".
    /// <see cref="ClaudeCliRefreshResult.Detail"/> carries the exit code or exception type.</summary>
    Failed,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Detail">Exit code or exception type name. Never output text (ADR-0001 — no
/// display text leaves Core).</param>
public sealed record ClaudeCliRefreshResult(RefreshOutcome Outcome, string? Detail = null)
{
    public bool Succeeded => Outcome == RefreshOutcome.Refreshed;
}

/// <summary>
/// Something that can ask Claude Code to refresh its own usage cache. A seam rather than a
/// courtesy: it lets whatever decides the shell's poll cadence (ADR-0007) be tested without
/// spawning a process.
/// </summary>
public interface IUsageCacheRefresher
{
    ClaudeCliRefreshResult Refresh();
}

/// <summary>
/// Asks Claude Code to refresh its own usage cache by running the vendor's own documented,
/// read-shaped command, under gate G6 (ADR-0005, board-answered 2026-09-28, binding). All three
/// of G6's limits are structural, not aspirational:
///
/// <list type="number">
/// <item><b>The vendor's own documented, read-shaped command.</b> <see cref="UsageArgument"/> is
/// exactly <c>/usage</c> — Claude Code's own documented slash command for viewing usage, carried
/// forward from the source repository's confirmed evidence (GitHub issue #234).</item>
/// <item><b>Never passed arguments that mutate vendor state.</b> <see cref="UsageArgument"/> is
/// the only entry ever added to <see cref="ProcessStartInfo.ArgumentList"/>, and it is passed as
/// a list entry with <see cref="ProcessStartInfo.UseShellExecute"/> false — never through a
/// shell, which is what the source found necessary the hard way: a shell can path-translate
/// <c>/usage</c> into something that is no longer a recognised slash command.</item>
/// <item><b>Never a precondition for showing a number.</b> Nothing in
/// <see cref="CachedUtilizationProvider"/> calls this type or anything implementing
/// <see cref="IUsageCacheRefresher"/> — that provider reads the cached block exactly as it
/// stands and returns an unavailable snapshot when there is none. Invoking a refresher, if one
/// runs at all, is strictly the shell's (ADR-0007) polling decision, made before or alongside a
/// poll, never inside the read path a poll depends on. A machine with no <c>claude</c> on PATH —
/// <see cref="RefreshOutcome.NotFound"/> — is therefore not a degraded machine, just one this
/// class does nothing for.</item>
/// </list>
///
/// <para><b>Deliberately deferred: the source repository's billed-invocation cost guard.</b> The
/// source's <c>ClaudeCliRefresher</c> snapshots Claude Code's transcript tree before and after
/// the spawn to detect whether <c>/usage</c> was ever misread as a real prompt (which the source
/// measured at roughly 50K tokens per occurrence). That guard needs to read the same transcript
/// tree <c>JsonlUsageProvider</c> reads, and this slice's boundaries exclude touching that
/// provider or its parser beyond reuse. G6's three limits do not require the guard — they are
/// satisfied by the argument-list-not-shell design above, which is the source's own root-cause
/// fix for the one confirmed misinterpretation case. Left for a future slice with its own board
/// review of the transcript-coupling this would add.</para>
/// </summary>
public sealed class ClaudeCliRefresher : IUsageCacheRefresher
{
    /// <summary>The executable, resolved through PATH by the OS rather than searched for here.</summary>
    public const string ExecutableName = "claude";

    /// <summary>
    /// The only argument this class will ever pass. Exactly this string and nothing else —
    /// anything Claude Code does not recognise as a slash command becomes a billed prompt.
    /// </summary>
    public const string UsageArgument = "/usage";

    /// <summary>
    /// How long to wait before killing the process. Generous rather than tight: the cost of
    /// being wrong is asymmetric. A slow machine killed mid-refresh keeps its stale block and
    /// nothing is lost; a login prompt that ignores a closed stdin would otherwise hang forever.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The preferred working directory when it exists, otherwise the fallback. A working
    /// directory that does not exist makes <see cref="Process.Start(ProcessStartInfo)"/> throw.
    /// Exposed as a pure function rather than resolved internally — ADR-0006 D2's rule applies
    /// here too: this type never calls <see cref="Environment.SpecialFolder"/> or reads an
    /// environment variable; the shell decides both candidates and hands the result down.
    /// </summary>
    public static string ResolveWorkingDirectory(string preferred, string fallback) =>
        Directory.Exists(preferred) ? preferred : fallback;

    /// <summary>How a run ended, independent of what it cost or changed.</summary>
    /// <param name="Started">False when the executable could not be launched at all.</param>
    /// <param name="Exited">False when it was killed at the timeout.</param>
    /// <param name="ExitCode">Meaningful only when <paramref name="Exited"/>.</param>
    /// <param name="Failure">Exception type name when the launch failed. Never a message.</param>
    public readonly record struct ProcessRun(
        bool Started, bool Exited, int ExitCode, string? Failure = null);

    private readonly IReadOnlyList<string> _candidateRoots;
    private readonly Func<TimeSpan, ProcessRun> _run;
    private readonly TimeSpan _timeout;

    /// <param name="candidateRoots">
    /// The same <c>.claude.json</c> search roots <see cref="CachedUtilizationProvider"/> reads,
    /// typically <see cref="ClaudeDataRoots.ClaudeCliConfigRoots"/>'s result — used only to
    /// compare the block's fetch time before and after the run, never to decide whether to run.
    /// </param>
    /// <param name="workingDirectory">
    /// Working directory for the spawned process, chosen rather than inherited — Claude Code
    /// files its transcript under a slug derived from the process's working directory, so an
    /// inherited one can leave a meaningless folder name in the user's project list. Resolve
    /// with <see cref="ResolveWorkingDirectory"/> from the shell's own data directory.
    /// </param>
    /// <param name="timeout">Overrides <see cref="DefaultTimeout"/>.</param>
    public ClaudeCliRefresher(IReadOnlyList<string> candidateRoots, string workingDirectory, TimeSpan? timeout = null)
        : this(candidateRoots, t => Spawn(workingDirectory, t), timeout)
    {
    }

    /// <param name="candidateRoots">See the other constructor.</param>
    /// <param name="run">Runs the process. Injected so every outcome is testable without
    /// spawning anything.</param>
    /// <param name="timeout">Overrides <see cref="DefaultTimeout"/>.</param>
    public ClaudeCliRefresher(IReadOnlyList<string> candidateRoots, Func<TimeSpan, ProcessRun> run, TimeSpan? timeout = null)
    {
        _candidateRoots = candidateRoots;
        _run = run;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Runs one refresh. Never throws: every failure is an outcome, because a swallowed
    /// exception here must not stop whatever poll loop called this from continuing to read the
    /// cache directly.
    /// </summary>
    public ClaudeCliRefreshResult Refresh()
    {
        var before = SafeFetchedAt();

        ProcessRun run;
        try
        {
            run = _run(_timeout);
        }
        catch (Exception ex)
        {
            return new ClaudeCliRefreshResult(RefreshOutcome.Failed, ex.GetType().Name);
        }

        if (!run.Started)
        {
            // No claude on PATH. Not a failure — most machines do not have one, and G6 (iii)
            // means this must not matter to whether a number gets shown.
            return new ClaudeCliRefreshResult(RefreshOutcome.NotFound, run.Failure);
        }

        if (!run.Exited)
        {
            return new ClaudeCliRefreshResult(RefreshOutcome.TimedOut);
        }

        if (run.ExitCode != 0)
        {
            return new ClaudeCliRefreshResult(RefreshOutcome.Failed, $"exit {run.ExitCode}");
        }

        var after = SafeFetchedAt();

        // Advanced, or appeared where there was nothing. Both are a refresh; a block that exists
        // now and did not before is the strongest possible version of one.
        return after is { } now && (before is not { } was || now > was)
            ? new ClaudeCliRefreshResult(RefreshOutcome.Refreshed)
            : new ClaudeCliRefreshResult(RefreshOutcome.Unchanged);
    }

    private DateTimeOffset? SafeFetchedAt()
    {
        try
        {
            return CachedUtilization.TryReadNewest(_candidateRoots)?.FetchedAtUtc;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs <c>claude /usage</c> with no window, no shell and no stdin.
    ///
    /// <para>Output is redirected and drained rather than ignored: a child that fills its pipe
    /// buffer blocks on the write and never exits, so the timeout would fire on a process that
    /// had actually finished its work. It is read and discarded, never parsed — the structured
    /// block <see cref="CachedUtilization"/> already parses is the one source of truth, not
    /// another application's terminal formatting.</para>
    /// </summary>
    private static ProcessRun Spawn(string workingDirectory, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(ExecutableName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory,
        };

        info.ArgumentList.Add(UsageArgument);

        Process? process = null;
        try
        {
            process = Process.Start(info);
            if (process is null)
            {
                return new ProcessRun(false, false, 0, "no process");
            }

            // Nothing will be typed at it. Closing stdin turns a login prompt into a fast exit
            // rather than a wait for the full timeout.
            process.StandardInput.Close();

            // Drained on background threads so neither pipe can fill and deadlock the child.
            var drain = Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync());

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                return new ProcessRun(true, false, 0);
            }

            // Bounded: the process is gone, so the pipes are closed and this completes. A
            // faulted read is swallowed deliberately — the output is discarded either way.
            try
            {
                drain.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            return new ProcessRun(true, true, process.ExitCode);
        }
        catch (Win32Exception ex)
        {
            // The documented shape of "executable not found" on both platforms.
            return new ProcessRun(false, false, 0, ex.GetType().Name);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return new ProcessRun(false, false, 0, ex.GetType().Name);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // Already gone, or not ours to kill. Either way the outcome is the timeout.
        }
    }
}
