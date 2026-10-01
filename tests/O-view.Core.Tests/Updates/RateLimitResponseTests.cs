using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

public class RateLimitResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TooManyRequests_is_always_a_rate_limit()
    {
        var limited = RateLimitResponse.IsRateLimited(429, null, null, null, Now, out var retryAfterUtc);

        Assert.True(limited);
        Assert.Null(retryAfterUtc);
    }

    [Fact]
    public void Forbidden_with_exhausted_remaining_is_a_rate_limit()
    {
        var limited = RateLimitResponse.IsRateLimited(403, "0", null, null, Now, out _);

        Assert.True(limited);
    }

    [Fact]
    public void Forbidden_with_retry_after_present_is_a_rate_limit_even_without_remaining()
    {
        var limited = RateLimitResponse.IsRateLimited(403, null, null, "60", Now, out var retryAfterUtc);

        Assert.True(limited);
        Assert.Equal(Now.AddSeconds(60), retryAfterUtc);
    }

    [Fact]
    public void Forbidden_with_no_corroborating_header_is_not_a_rate_limit()
    {
        var limited = RateLimitResponse.IsRateLimited(403, null, null, null, Now, out var retryAfterUtc);

        Assert.False(limited);
        Assert.Null(retryAfterUtc);
    }

    [Fact]
    public void Success_status_is_never_a_rate_limit()
    {
        var limited = RateLimitResponse.IsRateLimited(200, "0", null, null, Now, out _);

        Assert.False(limited);
    }

    [Fact]
    public void RateLimitReset_is_read_as_epoch_seconds_not_a_delta()
    {
        var resetInstant = Now.AddMinutes(30);
        var epochSeconds = resetInstant.ToUnixTimeSeconds().ToString();

        var limited = RateLimitResponse.IsRateLimited(429, "0", epochSeconds, null, Now, out var retryAfterUtc);

        Assert.True(limited);
        Assert.Equal(resetInstant, retryAfterUtc);
    }

    [Fact]
    public void RateLimitReset_in_the_past_is_ignored_in_favour_of_retry_after()
    {
        var pastEpochSeconds = Now.AddMinutes(-5).ToUnixTimeSeconds().ToString();

        var limited = RateLimitResponse.IsRateLimited(429, "0", pastEpochSeconds, "30", Now, out var retryAfterUtc);

        Assert.True(limited);
        Assert.Equal(Now.AddSeconds(30), retryAfterUtc);
    }

    [Fact]
    public void No_usable_header_leaves_retryAfterUtc_null_but_still_reports_rate_limited()
    {
        var limited = RateLimitResponse.IsRateLimited(429, null, "not-a-number", "also-not-a-number", Now, out var retryAfterUtc);

        Assert.True(limited);
        Assert.Null(retryAfterUtc);
    }
}
