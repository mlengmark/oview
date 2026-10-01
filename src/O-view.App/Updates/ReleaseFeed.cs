using System.Net.Http;
using OView.App;
using OView.Core.Updates;

namespace OView.App.Updates;

/// <summary>
/// The shell's single update-check holder (ADR-0007 D3): fetches the release feed and
/// compares it against the running version, holding GitHub's rate-limit cooldown for the
/// lifetime of this instance.
///
/// <para><b>One instance per process, not one per call.</b> The cooldown is an instance
/// field. The source repository's Windows head built a new feed on every check and so
/// discarded the cooldown it had just recorded (CONFIRMED, ADR-0001 D4's 2026-09-26/OVI-140
/// amendment) — the retry pattern GitHub issue #176 was meant to stop. The shell must
/// construct exactly one <see cref="ReleaseFeed"/> at startup and route every skin's check
/// through it, the same discipline <see cref="UsagePollLoop"/> applies to one clock and one
/// timer.</para>
///
/// <para>The pure comparison stays in Core's <see cref="UpdateCheck"/>; this class is only
/// the IO and the cooldown. It fetches the release feed only — the rate card has no network
/// source on the contract (ADR-0001's <c>RateCardSource</c> is <c>Bundled</c>/<c>UserFile</c>),
/// so adding one here would be a contract change, not a porting detail.</para>
/// </summary>
public sealed class ReleaseFeed
{
    private readonly IReleaseFeedTransport _transport;
    private readonly IClock _clock;

    private DateTimeOffset? _rateLimitedUntilUtc;

    public ReleaseFeed(IReleaseFeedTransport transport, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(clock);

        _transport = transport;
        _clock = clock;
    }

    /// <summary>
    /// Queries the feed and compares. Any transport failure or non-success status that is
    /// not a recognised rate limit becomes <see cref="UpdateOutcome.Unknown"/> — a failed
    /// update check must never take down a shell that is otherwise working, and "I could not
    /// tell" is an honest answer where "up to date" would be a fabricated one.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        ReleaseAssetSelector asset,
        CancellationToken cancellation = default)
    {
        // Still throttled from a previous answer: do not spend a request finding that out
        // again. This is the one cooldown holder D3 requires — every caller through this
        // same instance sees it, regardless of which skin asked.
        if (_rateLimitedUntilUtc is { } until && _clock.UtcNow < until)
        {
            return UpdateCheckResult.RateLimited(until);
        }

        ReleaseFeedResponse response;
        try
        {
            response = await _transport.FetchLatestReleaseAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return UpdateCheckResult.Unknown;
        }

        if (RateLimitResponse.IsRateLimited(
                response.StatusCode,
                response.RateLimitRemaining,
                response.RateLimitReset,
                response.RetryAfter,
                _clock.UtcNow,
                out var retryAfterUtc))
        {
            _rateLimitedUntilUtc = retryAfterUtc;
            return UpdateCheckResult.RateLimited(retryAfterUtc);
        }

        if (response.StatusCode is < 200 or >= 300)
        {
            return UpdateCheckResult.Unknown;
        }

        _rateLimitedUntilUtc = null;
        return UpdateCheck.Evaluate(currentVersion, response.Body, asset);
    }
}
