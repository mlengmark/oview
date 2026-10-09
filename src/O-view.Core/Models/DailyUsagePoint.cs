namespace OView.Core.Models;

/// <summary>
/// One local day's output-token total inside a <see cref="DailyUsageSeries"/> (ADR-0008 D9e).
/// The measure is output tokens, matching the statistics tiles — named, not implied.
///
/// <para><see cref="OutputTokens"/>' <see cref="UsageValueStatus.Unavailable"/> status means
/// this day is not in recorded history, and renders as a blank column with its date label
/// still drawn; <see cref="UsageValueStatus.Real"/> with a value of <c>0</c> means a recorded,
/// idle day. A skin must not reconstruct this distinction from a sparse list — every day in
/// the series window is always present (ADR-0008 D9e).</para>
/// </summary>
public sealed record DailyUsagePoint(DateOnly LocalDate, TokenCount OutputTokens);
