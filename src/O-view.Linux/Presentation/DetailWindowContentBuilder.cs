using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// One formatted row of <see cref="ModelUsageBreakdown.Rows"/>, pre-aggregated by Core and
/// never re-totalled here (ADR-0008 D9c) — each field is one existing-formatter call
/// (<see cref="UsageFormatter"/>) over the row Core already summed.
/// </summary>
public sealed record DetailWindowModelRow(
    string ModelId,
    string Requests,
    string InputTokens,
    string OutputTokens,
    string CacheWriteTokens,
    string CacheReadTokens,
    string EstimatedSpend);

/// <summary>
/// Everything the detail window renders, built once from one <see cref="UsageDetail"/> and
/// nothing else (ADR-0008 D9c) — the window draws this record and reads no other source, so it
/// can never pair a percent from one poll with statistics from an earlier one.
/// </summary>
public sealed record DetailWindowContent(
    string Freshness,
    string SessionLine,
    string WeeklyLine,
    string ExtraUsageLine,
    string TodayLine,
    string Window31dLine,
    string Caveat,
    IReadOnlyList<DetailWindowModelRow> ModelRows,
    string ModelSectionNote);

/// <summary>
/// Builds the Linux detail window's content from a pushed <see cref="UsageDetail"/> (ADR-0008
/// slice 10, OVI-408), the Linux counterpart of <c>O-view.Tray</c>'s
/// <c>DetailWindowContentBuilder</c> (slice 6, OVI-386) — independently implemented, not shared
/// (D1). Renders only figures the detail carried — no figure here is summed, averaged or
/// re-bucketed by this skin (D9c); every per-model total is Core's own pre-aggregated
/// <see cref="ModelUsageRow"/>, formatted through the existing <see cref="UsageFormatter"/> and
/// <see cref="PanelTextFormatter"/>/<see cref="PanelStatisticsFormatter"/> the panel tile and
/// tooltip already use. Worded independently from the Windows skin throughout (ADR-0003) — the
/// same facts, a different sentence shape and a different estimated-marker convention (this
/// skin's own " (est.)" suffix, matching <see cref="TooltipFormatter"/>, not the Windows skin's
/// "~" prefix).
/// </summary>
public static class DetailWindowContentBuilder
{
    public static DetailWindowContent Build(UsageDetail detail, DateTimeOffset utcNow, TimeZoneInfo displayZone)
    {
        var snapshot = detail.Snapshot;
        var stats = detail.Statistics;

        return new DetailWindowContent(
            Freshness: PanelTextFormatter.Freshness(snapshot, utcNow, displayZone),
            SessionLine: "Session: " + PercentText(snapshot.SessionUtilizationPercent)
                + " — " + PanelTextFormatter.SessionReset(snapshot.SessionResetAt, utcNow, displayZone),
            WeeklyLine: "Week: " + PercentText(snapshot.WeeklyUtilizationPercent)
                + " — " + WeeklyResetLine(snapshot.WeeklyResetAt, utcNow, displayZone),
            ExtraUsageLine: ExtraUsageLine(snapshot.ExtraUsage, displayZone),
            TodayLine: "Today so far: " + UsageFormatter.Tokens(stats.OutputTokensToday)
                + " tokens, " + UsageFormatter.Usd(stats.EstimatedSpendToday) + " estimated",
            Window31dLine: "31-day window: " + UsageFormatter.Tokens(stats.OutputTokensWindow31d)
                + " tokens, " + UsageFormatter.Usd(stats.EstimatedValueWindow31d) + " estimated value",
            Caveat: PanelTextFormatter.Caveat(stats),
            ModelRows: BuildModelRows(detail.Models),
            ModelSectionNote: ModelSectionNote(detail.Models, stats.UnpricedModels));
    }

    /// <summary>
    /// <c>63% (est.)</c> or <c>unknown</c> for a percent Core could not establish — the same
    /// unavailable convention <see cref="UsageFormatter.Tokens"/> uses, never a fabricated 0%.
    /// The " (est.)" suffix matches <see cref="TooltipFormatter"/>'s own marker, so the tooltip
    /// and this window never disagree on which values are modelled.
    /// </summary>
    private static string PercentText(UsagePercent percent) => percent.Value is { } value
        ? string.Create(CultureInfo.InvariantCulture, $"{Round(value)}%{Marker(percent.Status)}")
        : "unknown";

    private static string WeeklyResetLine(UsageInstant resetAt, DateTimeOffset utcNow, TimeZoneInfo zone) =>
        resetAt.Status == UsageValueStatus.Unavailable || resetAt.Value is not { } reset
            ? "no reset observed yet"
            : PanelTextFormatter.WeeklyReset(reset, utcNow, zone);

    /// <summary>
    /// Omitted entirely when Claude Code's own cache did not say (<see cref="UsageSnapshot.ExtraUsage"/>
    /// is <see langword="null"/>) — never a fabricated "off" for an account setting nobody read
    /// (ADR-0001, OVI-168).
    /// </summary>
    private static string ExtraUsageLine(ExtraUsageReading? extraUsage, TimeZoneInfo zone)
    {
        if (extraUsage is not { } reading)
        {
            return "";
        }

        var state = reading.State == ExtraUsageState.Enabled ? "on" : "off";
        var at = TimeZoneInfo.ConvertTime(reading.FetchedAtUtc, zone);

        return string.Create(CultureInfo.InvariantCulture, $"Extra usage {state}, as reported {at:HH:mm}");
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Marker(UsageValueStatus status) => status == UsageValueStatus.Estimated ? " (est.)" : "";

    private static IReadOnlyList<DetailWindowModelRow> BuildModelRows(ModelUsageBreakdown models)
    {
        if (models.Status != UsageValueStatus.Real)
        {
            return Array.Empty<DetailWindowModelRow>();
        }

        var rows = new List<DetailWindowModelRow>(models.Rows.Count);
        foreach (var row in models.Rows)
        {
            rows.Add(new DetailWindowModelRow(
                ModelId: row.ModelId,
                Requests: row.RequestCount.ToString(CultureInfo.InvariantCulture),
                InputTokens: UsageFormatter.Tokens(row.InputTokens),
                OutputTokens: UsageFormatter.Tokens(row.OutputTokens),
                CacheWriteTokens: UsageFormatter.Tokens(row.CacheCreationTokens),
                CacheReadTokens: UsageFormatter.Tokens(row.CacheReadTokens),
                EstimatedSpend: UsageFormatter.Usd(row.EstimatedSpend)));
        }

        return rows;
    }

    /// <summary>
    /// States the three cases <see cref="ModelUsageBreakdown"/>'s own remarks distinguish:
    /// Core could not read the ledger at all, Core read it and found no activity, or the window
    /// is a subtotal because at least one model in it could not be priced (D9a's unpriced-model
    /// pairing with <see cref="UsageStatistics.UnpricedModels"/>). Worded independently from the
    /// Windows skin (ADR-0003).
    /// </summary>
    private static string ModelSectionNote(ModelUsageBreakdown models, UnpricedModels unpriced)
    {
        if (models.Status != UsageValueStatus.Real)
        {
            return "Per-model breakdown not available.";
        }

        if (models.Rows.Count == 0)
        {
            return "No activity in this window.";
        }

        return unpriced.Status == UsageValueStatus.Real && unpriced.ModelIds.Count > 0
            ? "Partial total — " + string.Join(", ", unpriced.ModelIds) + " has no published rate."
            : "";
    }
}
