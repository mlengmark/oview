namespace OView.App.Updates;

/// <summary>
/// One HTTP response, reduced to what <see cref="ReleaseFeed"/> and
/// <see cref="OView.Core.Updates.RateLimitResponse"/> need: a status code, the headers
/// GitHub's rate limit uses, and the body. Kept header-name-agnostic beyond those three so
/// a fake in tests never has to construct a real HTTP response type.
/// </summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="RateLimitRemaining">Value of <c>x-ratelimit-remaining</c>, if present.</param>
/// <param name="RateLimitReset">Value of <c>x-ratelimit-reset</c> — epoch seconds, if present.</param>
/// <param name="RetryAfter">Value of <c>retry-after</c> — delta seconds, if present.</param>
/// <param name="Body">The response body, read as text.</param>
public sealed record ReleaseFeedResponse(
    int StatusCode,
    string? RateLimitRemaining,
    string? RateLimitReset,
    string? RetryAfter,
    string Body);

/// <summary>
/// The seam between <see cref="ReleaseFeed"/> and the network (ADR-0007 D3): fetching
/// GitHub's <c>releases/latest</c> endpoint is the one piece of real HTTP in this
/// repository, so it is isolated behind an interface the same way <c>IClock</c> and
/// <c>IAppTimer</c> isolate the clock and the timer — a fake implementation drives the
/// cooldown tests with no network and no real time.
/// </summary>
public interface IReleaseFeedTransport
{
    Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation);
}
