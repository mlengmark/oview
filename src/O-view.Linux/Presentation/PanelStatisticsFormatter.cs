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
}
