using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <c>Freshness</c> rendering, wired into the harness so every
/// <see cref="FreshnessFixture"/> is exercised against every skin from one test run
/// (ADR-0003). Parallel to <see cref="SkinUnderTest"/>, which carries no <c>utcNow</c> input.
/// </summary>
public sealed record FreshnessSkinUnderTest(string Name, Func<UsageSnapshot, DateTimeOffset, TimeZoneInfo, string> Format);
