namespace OView.Core.Models;

/// <summary>
/// How much of the usage-history window Core actually has data for (ADR-0001). Replaces
/// the leaked <c>PanelStatistics.CoverageNote</c> sentence: Core hands over the two counts,
/// and a skin writes its own sentence from them — never Core.
///
/// <para><see cref="RecordedDays"/> counts days Core has data <b>for</b>, not days with
/// usage on them: a day inside the recorded era with no usage is a genuine zero and still
/// counts. Both counts are always real — history coverage is measured, never modelled or
/// unavailable.</para>
/// </summary>
public sealed record HistoryCoverage(int RecordedDays, int WindowDays)
{
    /// <summary>True when the window is not yet fully covered by recorded history.</summary>
    public bool HasPartialHistory => RecordedDays < WindowDays;
}
