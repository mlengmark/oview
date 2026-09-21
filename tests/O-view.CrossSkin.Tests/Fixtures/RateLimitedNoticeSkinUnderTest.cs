namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <c>RateLimitedNotice</c> entry point, wired into the harness so every
/// <see cref="RateLimitedNoticeFixture"/> is exercised against every skin from one test run
/// (ADR-0003, OVI-80).
/// </summary>
public sealed record RateLimitedNoticeSkinUnderTest(string Name, Func<DateTimeOffset?, TimeZoneInfo, string> Format);
