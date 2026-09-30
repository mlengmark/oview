using OView.Core.Models;
using OView.Core.Storage;

namespace OView.Core.Providers.CachedUsage;

/// <summary>
/// Serves session and weekly usage from Claude Code's own cached figures
/// (<see cref="CachedUtilization"/>, ADR-0005 D2, slice 5) — the only provider in this
/// repository that gives authoritative percentages and exact reset instants to a machine
/// running Claude Code with no Claude Desktop installed.
///
/// <para>Locates the block under <see cref="ClaudeDataRoots.ClaudeCliConfigRoots"/>'s roots
/// (ADR-0005 D5) and, per <see cref="CachedUtilization.TryReadNewest"/>, trusts the newest
/// fetch across every candidate rather than the first one that exists — a stub left behind by a
/// relocated config would otherwise read as valid and blank the two plan bars for a machine
/// that has both a working cache and a doomed one.</para>
///
/// <para><b>A reported reset instant already in the past is not carried forward.</b> The block
/// is a cache: left unrefreshed across a window boundary, it can still read a high percentage
/// for a window that rolled over hours ago. A bar whose own <c>ResetsAtUtc</c> is at or before
/// <paramref name="GetSnapshot"/>'s <c>utcNow</c> is therefore dropped, not shown as a stale-but-
/// plausible figure.</para>
///
/// <para><b>Deliberately deferred, and left for future evidence rather than guessed at here:</b>
/// the source repository's zero-reading distrust window (an aged zero degrading to unavailable,
/// on the theory that utilization only rises within a window) is not carried forward — this
/// repository's <c>PlanHistoryProvider</c> slice (4) did not port the constant it would share,
/// and inventing a fresh threshold with no measurement behind it would be exactly the kind of
/// number ADR-0001's "never fabricate" rule exists to prevent.</para>
/// </summary>
public sealed class CachedUtilizationProvider : IUsageProvider
{
    /// <summary>
    /// Maximum age at which the cached percentages are still labelled
    /// <see cref="DataSourceKind.Live"/>.
    ///
    /// <para>Not a measured sampling interval — this is a cache refreshed only when Claude Code
    /// talks to the API, so there is no cadence to measure the way <c>PlanHistoryProvider</c>
    /// measured Claude Desktop's sampler. It is a labelling threshold only: past it the reading
    /// is still shown, just as <see cref="DataSourceKind.Stale"/> rather than
    /// <see cref="DataSourceKind.Live"/>. The rollover check above, not this threshold, is what
    /// decides whether a figure may be shown at all.</para>
    /// </summary>
    public static readonly TimeSpan DefaultFreshness = TimeSpan.FromMinutes(15);

    private readonly IReadOnlyList<string> _candidateRoots;
    private readonly WeeklyResetAnchorStore? _anchorStore;
    private readonly TimeSpan _freshness;

    /// <param name="candidateRoots">
    /// The <c>.claude.json</c> search roots — typically
    /// <see cref="ClaudeDataRoots.ClaudeCliConfigRoots"/>'s result for the current host.
    /// Injected rather than resolved here, so this type never reads the real operating system
    /// or environment (ADR-0005 D5).
    /// </param>
    /// <param name="anchorStore">
    /// ADR-0006's weekly-reset anchor store. When this call observes a current, in-the-future
    /// weekly reset instant, it is saved here so the exact instant survives the next stretch the
    /// vendor's cache spends stale — measured at 43 hours on the development machine
    /// (ADR-0006). Optional: a caller that has not wired storage yet still gets a working
    /// provider, just without that persistence side effect.
    /// </param>
    /// <param name="freshness">Maximum age still labelled <see cref="DataSourceKind.Live"/>;
    /// defaults to <see cref="DefaultFreshness"/>.</param>
    public CachedUtilizationProvider(
        IReadOnlyList<string> candidateRoots,
        WeeklyResetAnchorStore? anchorStore = null,
        TimeSpan? freshness = null)
    {
        _candidateRoots = candidateRoots;
        _anchorStore = anchorStore;
        _freshness = freshness ?? DefaultFreshness;
    }

    /// <summary>
    /// Returns a <see cref="DataSourceKind.Live"/> or <see cref="DataSourceKind.Stale"/>
    /// snapshot built from the newest cached block across every candidate root, or
    /// <see cref="UsageSnapshot.Unavailable"/> when none has one, or when every bar it carries
    /// has aged out or rolled over. Every failure — a missing directory, an unreadable file, a
    /// permission error, malformed JSON — is silently equivalent to "no block here"; none of
    /// them ever surfaces as an exception (ADR-0005 D1).
    /// </summary>
    public UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
    {
        var cached = SafeReadNewest();
        if (cached is null)
        {
            return UsageSnapshot.Unavailable;
        }

        var fiveHour = CurrentWindow(cached.FiveHour, utcNow);
        var sevenDay = CurrentWindow(cached.SevenDay, utcNow);

        if (fiveHour is null && sevenDay is null)
        {
            // Everything this block carried has aged out or rolled over. Report nothing rather
            // than winning a tier with a blank (ADR-0005 D3) — a future composite can still fall
            // through to a source that knows something.
            return UsageSnapshot.Unavailable;
        }

        var weeklyReset = BarReset(sevenDay);
        if (weeklyReset is { Status: UsageValueStatus.Real, Value: { } weeklyResetValue })
        {
            _anchorStore?.Save(weeklyResetValue);
        }

        var age = utcNow - cached.FetchedAtUtc;
        var extraUsage = cached.ExtraUsage is { } state
            ? new ExtraUsageReading(state, cached.FetchedAtUtc)
            : null;

        return new UsageSnapshot(
            age <= _freshness ? DataSourceKind.Live : DataSourceKind.Stale,
            utcNow,
            BarPercent(fiveHour),
            BarReset(fiveHour),
            BarPercent(sevenDay),
            weeklyReset,
            UsageLevel.Green)
        {
            ExtraUsage = extraUsage,
        };
    }

    /// <summary>
    /// <paramref name="bar"/> as long as its own reset instant has not already passed
    /// <paramref name="utcNow"/>; null otherwise. A bar with no reset instant at all cannot be
    /// proven to have rolled over, so it passes through unchanged.
    /// </summary>
    private static UtilizationBar? CurrentWindow(UtilizationBar? bar, DateTimeOffset utcNow) =>
        bar is not null && bar.ResetsAtUtc is { } resets && resets <= utcNow ? null : bar;

    private static UsagePercent BarPercent(UtilizationBar? bar) =>
        bar is null
            ? new UsagePercent(null, UsageValueStatus.Unavailable)
            : new UsagePercent(bar.Percent, UsageValueStatus.Real);

    private static UsageInstant BarReset(UtilizationBar? bar) =>
        bar?.ResetsAtUtc is { } resets
            ? new UsageInstant(resets, UsageValueStatus.Real)
            : new UsageInstant(null, UsageValueStatus.Unavailable);

    /// <summary>
    /// A reader that throws must not blank the display — same contract as every other provider
    /// (<see cref="IUsageProvider"/>): unavailable data is <see cref="UsageSnapshot.Unavailable"/>,
    /// never an exception escaping into the poll loop.
    /// </summary>
    private CachedUtilization? SafeReadNewest()
    {
        try
        {
            return CachedUtilization.TryReadNewest(_candidateRoots);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
