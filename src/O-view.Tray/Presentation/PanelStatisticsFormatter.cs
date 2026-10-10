using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds the history-coverage caveat sentence from a <see cref="HistoryCoverage"/> — the
/// one presentation leak <c>PanelStatistics.cs</c> carried (ADR-0001): the source file
/// formatted <c>"{RecordedDays} of {WindowDays} days recorded"</c> as a ready-to-display
/// sentence rather than handing over the two counts for a skin to phrase itself. This class
/// reproduces that exact wording, since it is this skin's (Windows WPF's) own historical
/// text — but the decision to say it at all, and how, now belongs here, not to Core. Not
/// shared with O-view.Linux; each skin owns its own wording (ADR-0003).
/// </summary>
public static class PanelStatisticsFormatter
{
    /// <summary>
    /// The coverage caveat, or empty when the window is fully covered by recorded history —
    /// byte-for-byte the same rule and wording as the source app's own
    /// <c>PanelStatistics.CoverageNote</c>.
    /// </summary>
    public static string CoverageNote(HistoryCoverage coverage) =>
        coverage.HasPartialHistory
            ? string.Create(CultureInfo.InvariantCulture, $"{coverage.RecordedDays} of {coverage.WindowDays} days recorded")
            : "";

    /// <summary>
    /// The statistics tiles' own coverage caption (ADR-0008 D10b, gate G7 parity slice P10):
    /// unlike <see cref="CoverageNote"/>, which hides itself once the window is fully covered,
    /// this caption always states both counts — <see cref="HistoryCoverage.RecordedDays"/>
    /// counts days Core has data <b>for</b>, never days with usage (D11b's
    /// <c>CoverageCaptionFixture</c>), so "31 of 31 days recorded" is as true a caption as a
    /// partial one.
    /// </summary>
    public static string CoverageCaption(HistoryCoverage coverage) =>
        string.Create(CultureInfo.InvariantCulture, $"{coverage.RecordedDays} of {coverage.WindowDays} days recorded");
}
