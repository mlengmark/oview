namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="RateLimitedNoticeFixture"/>s (ADR-0003, OVI-80).
/// </summary>
public static class RateLimitedNoticeFixtures
{
    private static readonly DateTimeOffset RetryAfterUtc = new(2026, 9, 21, 14, 30, 0, TimeSpan.Zero);

    public static readonly RateLimitedNoticeFixture RetryAfterKnown = new(
        Name: "retry-after-known",
        RetryAfterUtc: RetryAfterUtc,
        Local: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("14:30"),
            ContentFact.Contains("connection"),
        });

    public static readonly RateLimitedNoticeFixture RetryAfterUnknown = new(
        Name: "retry-after-unknown",
        RetryAfterUtc: null,
        Local: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact("does not render a clock time", rendered => !rendered.Contains(':')),
            ContentFact.Contains("connection"),
        });

    public static IReadOnlyList<RateLimitedNoticeFixture> All { get; } = new[]
    {
        RetryAfterKnown,
        RetryAfterUnknown,
    };
}
