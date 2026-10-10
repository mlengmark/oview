using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// Builds this skin's own history-coverage caveat sentence from a
/// <see cref="HistoryCoverage"/>. Worded deliberately differently from
/// <c>O-view.Tray</c>'s <c>"{RecordedDays} of {WindowDays} days recorded"</c> (ADR-0003 —
/// two skins are free to phrase the same facts differently); the golden-master harness pins
/// only that both counts appear, and that a fully-covered window says nothing at all. Not
/// shared with O-view.Tray.
/// </summary>
public static class PanelStatisticsFormatter
{
    /// <summary>
    /// The coverage caveat, or empty when the window is fully covered by recorded history.
    /// </summary>
    public static string CoverageNote(HistoryCoverage coverage) =>
        coverage.HasPartialHistory
            ? string.Create(CultureInfo.InvariantCulture, $"{coverage.RecordedDays}/{coverage.WindowDays} days of history")
            : "";

    /// <summary>
    /// The statistics tiles' own coverage caption (ADR-0008 D10b, gate G7 parity slice P10):
    /// unlike <see cref="CoverageNote"/>, which hides itself once the window is fully covered,
    /// this caption always states both counts — <see cref="HistoryCoverage.RecordedDays"/>
    /// counts days Core has data <b>for</b>, never days with usage (D11b's
    /// <c>CoverageCaptionFixture</c>). Worded independently from the Windows skin (ADR-0003),
    /// but the harness pins the same content fact: both counts, out of 31.
    /// </summary>
    public static string CoverageCaption(HistoryCoverage coverage) =>
        string.Create(CultureInfo.InvariantCulture, $"{coverage.RecordedDays} of {coverage.WindowDays} days recorded");
}
