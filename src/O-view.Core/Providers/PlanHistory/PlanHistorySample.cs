namespace OView.Core.Providers.PlanHistory;

/// <summary>
/// One validated sample from Claude Desktop's <c>plan-usage-history.json</c> — carried
/// forward from the source repository's <c>PlanHistorySample</c> (CONFIRMED at
/// <c>897777b</c>). Only a sample with every required field present and in range survives
/// parsing; anything partial is skipped by <see cref="PlanHistoryProvider"/>.
/// </summary>
/// <param name="AtUtc">Sample time — the file stores this as Unix epoch milliseconds under
/// the <c>t</c> key.</param>
/// <param name="FiveHourPercent"><c>u.fh</c> — five-hour rolling-window utilization, 0-100.</param>
/// <param name="SevenDayPercent"><c>u.sd</c> — seven-day rolling-window utilization, 0-100.</param>
public sealed record PlanHistorySample(
    DateTimeOffset AtUtc,
    int FiveHourPercent,
    int SevenDayPercent);
