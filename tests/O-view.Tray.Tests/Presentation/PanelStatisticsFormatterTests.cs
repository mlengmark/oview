using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Regression target ported from the source app's own
/// <c>PanelStatisticsTests.CoverageNote_StatesPartialHistory_AndIsSilentWhenComplete</c>
/// (`mlengmark/O-view`, `tests/O-view.Core.Tests/PanelStatisticsTests.cs`): the exact wording
/// and the exact silent-when-complete rule this skin's caveat reproduces.
/// </summary>
public class PanelStatisticsFormatterTests
{
    [Fact]
    public void PartialHistoryStatesBothCounts()
    {
        var coverage = new HistoryCoverage(18, 31);

        Assert.Equal("18 of 31 days recorded", PanelStatisticsFormatter.CoverageNote(coverage));
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

        Assert.Equal("0 of 31 days recorded", PanelStatisticsFormatter.CoverageNote(coverage));
    }
}
