using System.Reflection;
using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>
/// Proves ADR-0001's contract holds for the slice of Core that
/// <c>UsageFormatter</c>/<c>PanelStatistics</c>'s presentation logic needs: every value is
/// structured (typed, with its own trust status), not a pre-built string, and none of the
/// Windows-WPF-owned presentation facts (the ~180px tile-width K/M threshold, the "$"
/// prefix, the "unknown" fallback string) have any footprint in this assembly.
/// </summary>
public class UsageStatisticsTests
{
    [Fact]
    public void EveryContractValueIsStructuredNeverAPreBuiltString()
    {
        var properties = typeof(UsageStatistics).GetProperties();

        Assert.NotEmpty(properties);
        Assert.All(properties, property => Assert.NotEqual(typeof(string), property.PropertyType));
    }

    [Fact]
    public void EveryTokenCountAndSpendCarriesItsOwnTrustStatus()
    {
        var statistics = new UsageStatistics(
            new TokenCount(1_500, UsageValueStatus.Real),
            new EstimatedUsd(9.36m, UsageValueStatus.Estimated),
            new TokenCount(12_700_000, UsageValueStatus.Real),
            new EstimatedUsd(492.52m, UsageValueStatus.Estimated),
            new HistoryCoverage(18, 31));

        Assert.Equal(UsageValueStatus.Real, statistics.OutputTokensToday.Status);
        Assert.Equal(UsageValueStatus.Estimated, statistics.EstimatedSpendToday.Status);
        Assert.Equal(UsageValueStatus.Real, statistics.OutputTokensWindow31d.Status);
        Assert.Equal(UsageValueStatus.Estimated, statistics.EstimatedValueWindow31d.Status);
    }

    [Fact]
    public void UnavailableStatisticsMarksEveryValueUnavailableRatherThanZeroOrBlank()
    {
        var statistics = UsageStatistics.Unavailable;

        Assert.Null(statistics.OutputTokensToday.Value);
        Assert.Equal(UsageValueStatus.Unavailable, statistics.OutputTokensToday.Status);
        Assert.Null(statistics.EstimatedSpendToday.Value);
        Assert.Equal(UsageValueStatus.Unavailable, statistics.EstimatedSpendToday.Status);
        Assert.Null(statistics.OutputTokensWindow31d.Value);
        Assert.Equal(UsageValueStatus.Unavailable, statistics.OutputTokensWindow31d.Status);
        Assert.Null(statistics.EstimatedValueWindow31d.Value);
        Assert.Equal(UsageValueStatus.Unavailable, statistics.EstimatedValueWindow31d.Status);
    }

    [Fact]
    public void HistoryCoverageIsPartialOnlyWhenRecordedDaysFallsShortOfTheWindow()
    {
        Assert.True(new HistoryCoverage(18, 31).HasPartialHistory);
        Assert.False(new HistoryCoverage(31, 31).HasPartialHistory);
    }

    /// <summary>
    /// Mirrors <c>UsageSnapshotTests.ThisAssemblyDefinesNoNotifyIconLengthCap</c>: the
    /// ~180px panel-tile-width threshold that gates <c>UsageFormatter.Tokens()</c>'s K/M
    /// abbreviation is a Windows-WPF rendering fact (ADR-0001), and must have no static
    /// member anywhere in this assembly.
    /// </summary>
    [Fact]
    public void ThisAssemblyDefinesNoTileWidthOrCurrencyPresentationConstant()
    {
        var presentationMemberNames = new[] { "TileWidthPx", "CurrencyPrefix", "CoverageNote" };

        var matches = typeof(UsageStatistics).Assembly.GetTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(member => presentationMemberNames.Contains(member.Name));

        Assert.Empty(matches);
    }

    /// <summary>
    /// <c>CoverageNote</c> was the one leaked sentence in the source
    /// <c>PanelStatistics.cs</c> (ADR-0001) — it must not exist as a string-returning member
    /// anywhere in this assembly, structured or not.
    /// </summary>
    [Fact]
    public void ThisAssemblyDefinesNoCoverageNoteStringMember()
    {
        var stringReturningCoverageNoteMembers = typeof(UsageStatistics).Assembly.GetTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.Name == "CoverageNote" && property.PropertyType == typeof(string));

        Assert.Empty(stringReturningCoverageNoteMembers);
    }
}
