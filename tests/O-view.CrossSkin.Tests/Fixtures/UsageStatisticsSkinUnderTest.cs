using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <see cref="UsageStatistics"/> rendering, wired into the harness so every
/// <see cref="UsageStatisticsFixture"/> is exercised against every skin from one test run
/// (ADR-0003). Parallel to <see cref="SkinUnderTest"/>, which is pinned to the tooltip's
/// <c>UsageSnapshot</c> shape.
/// </summary>
public sealed record UsageStatisticsSkinUnderTest(string Name, Func<UsageStatistics, string> Render);
