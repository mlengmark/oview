using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <c>Caveat</c> and <c>RateAge</c> entry points, wired into the harness
/// together because they take different input shapes (<see cref="UsageStatistics"/> and
/// <see cref="RateCardStamp"/>); <see cref="UsageCaveatFixture.Render"/> closes over which one a
/// fixture exercises (ADR-0003's 2026-09-23 amendment, OVI-100).
/// </summary>
public sealed record UsageCaveatSkinUnderTest(
    string Name,
    Func<UsageStatistics, string> Caveat,
    Func<RateCardStamp, string> RateAge);
