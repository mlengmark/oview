using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

public class PanelStatisticsFormatterTests
{
    [Fact]
    public void PartialHistoryStatesBothCounts()
    {
        var coverage = new HistoryCoverage(18, 31);

        Assert.Equal("18/31 days of history", PanelStatisticsFormatter.CoverageNote(coverage));
    }

    [Fact]
    public void CompleteHistoryIsSilent()
    {
        var coverage = new HistoryCoverage(31, 31);

        Assert.Equal("", PanelStatisticsFormatter.CoverageNote(coverage));
    }

    [Fact]
    public void EmptyHistoryStillStatesBothCounts()
    {
        var coverage = new HistoryCoverage(0, 31);

        Assert.Equal("0/31 days of history", PanelStatisticsFormatter.CoverageNote(coverage));
    }
}
