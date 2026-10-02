using System.Globalization;

namespace OView.Core.Updates;

/// <summary>
/// Reads GitHub's rate-limit signals off a response, without touching an HTTP response type
/// so the rules stay unit-testable with no network and no HTTP types in Core (ADR-0007 D3,
/// ADR-0001 D4 — "the pure rules stay in BCL-only Core").
///
/// <para>Ported from the source repository's <c>O-view.Core/Updates/RateLimitResponse.cs</c>
/// with no behaviour change. The source's doc comment explains why this is its own decision:
/// a throttle and a dead network must not collapse into the same outcome, because the old
/// behaviour retried straight back into the limit forever (source issue #176).</para>
///
/// <para><b>Not every 403 is a throttle.</b> GitHub returns 403 for several reasons, so the
/// headers have to agree before this claims a rate limit — otherwise a genuinely broken
/// request would be excused as "try again in an hour" and never investigated.</para>
/// </summary>
public static class RateLimitResponse
{
    /// <summary>Status codes GitHub uses to refuse a request it is throttling.</summary>
    public const int Forbidden = 403;

    public const int TooManyRequests = 429;

    /// <summary>
    /// Whether this response is GitHub saying "you are over the limit", and when it lifts.
    ///
    /// <para><paramref name="retryAfterUtc"/> is null when nothing usable was sent. That is
    /// not a failure: the caller still knows it was throttled, and falls back to its own
    /// cooldown rather than hammering.</para>
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="rateLimitRemaining">Value of <c>x-ratelimit-remaining</c>, if present.</param>
    /// <param name="rateLimitReset">Value of <c>x-ratelimit-reset</c> — <b>epoch seconds</b>, not a delta.</param>
    /// <param name="retryAfter">Value of <c>retry-after</c> — <b>delta seconds</b>, not an instant.</param>
    /// <param name="utcNow">Now, for turning a delta into an instant.</param>
    public static bool IsRateLimited(
        int statusCode,
        string? rateLimitRemaining,
        string? rateLimitReset,
        string? retryAfter,
        DateTimeOffset utcNow,
        out DateTimeOffset? retryAfterUtc)
    {
        retryAfterUtc = null;

        // 429 is unambiguous. 403 needs corroboration, because GitHub also uses it for
        // things a cooldown would not fix.
        var throttled = statusCode == TooManyRequests
            || (statusCode == Forbidden && (Exhausted(rateLimitRemaining) || retryAfter is not null));

        if (!throttled)
        {
            return false;
        }

        retryAfterUtc = ResetInstant(rateLimitReset, utcNow) ?? RetryDelta(retryAfter, utcNow);
        return true;
    }

    /// <summary>A remaining count of exactly zero. Absent or unparseable is not evidence.</summary>
    private static bool Exhausted(string? remaining) =>
        long.TryParse(remaining, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        && value <= 0;

    /// <summary>
    /// <c>x-ratelimit-reset</c> is Unix epoch seconds. Reading it as a delta — the mistake the
    /// header name invites next to <c>retry-after</c> — would put the retry 56 years out.
    /// </summary>
    private static DateTimeOffset? ResetInstant(string? reset, DateTimeOffset utcNow)
    {
        if (!long.TryParse(reset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochSeconds))
        {
            return null;
        }

        var instant = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

        // A reset already in the past says nothing useful, and a clock skewed the other way
        // would otherwise pin the app into a cooldown it can never leave.
        return instant > utcNow ? instant : null;
    }

    /// <summary><c>retry-after</c> is a delta in seconds, so it is added to now.</summary>
    private static DateTimeOffset? RetryDelta(string? retryAfter, DateTimeOffset utcNow) =>
        long.TryParse(retryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? utcNow.AddSeconds(seconds)
            : null;
}
