using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// Proves <see cref="DetailWindowContentBuilder"/> (ADR-0008 slice 10, OVI-408) renders only
/// what the pushed <see cref="UsageDetail"/> carried (D9c): no figure here is a fabricated
/// zero, no per-model row is re-totalled, and the three distinct "what happened to the
/// per-model window" cases each get their own, non-interchangeable wording.
/// </summary>
public sealed class DetailWindowContentBuilderTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 8, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unavailable_detail_never_fabricates_a_percent_or_an_extra_usage_state()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.DoesNotContain("0%", content.SessionLine);
        Assert.DoesNotContain("0%", content.WeeklyLine);
        Assert.Equal("", content.ExtraUsageLine);
        Assert.Equal("No usage data", content.Freshness);
    }

    [Fact]
    public void Unavailable_model_breakdown_admits_it_could_not_be_read_not_a_fabricated_empty_window()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Empty(content.ModelRows);
        Assert.Contains("not available", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no activity", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Real_but_empty_model_breakdown_states_no_activity_rather_than_unavailable()
    {
        var detail = new UsageDetail(
            UsageSnapshot.Unavailable,
            UsageStatistics.Unavailable,
            new ModelUsageBreakdown(
                new DateOnly(2026, 8, 9),
                new DateOnly(2026, 9, 8),
                Array.Empty<ModelUsageRow>(),
                new HistoryCoverage(31, 31),
                new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                UsageValueStatus.Real));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Empty(content.ModelRows);
        Assert.Contains("no activity", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Model_rows_carry_only_cores_own_pre_aggregated_figures_through_the_existing_formatters()
    {
        var row = new ModelUsageRow(
            ModelId: "claude-sonnet-5",
            RequestCount: 120,
            InputTokens: new TokenCount(500_000, UsageValueStatus.Real),
            OutputTokens: new TokenCount(250_000, UsageValueStatus.Real),
            CacheCreationTokens: new TokenCount(10_000, UsageValueStatus.Real),
            CacheReadTokens: new TokenCount(20_000, UsageValueStatus.Real),
            EstimatedSpend: new EstimatedUsd(40.00m, UsageValueStatus.Estimated));
        var detail = new UsageDetail(
            UsageSnapshot.Unavailable,
            UsageStatistics.Unavailable,
            new ModelUsageBreakdown(
                new DateOnly(2026, 8, 9),
                new DateOnly(2026, 9, 8),
                new[] { row },
                new HistoryCoverage(31, 31),
                new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                UsageValueStatus.Real));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var rendered = Assert.Single(content.ModelRows);
        Assert.Equal("claude-sonnet-5", rendered.ModelId);
        Assert.Equal("120", rendered.Requests);
        Assert.Equal(UsageFormatter.Tokens(row.OutputTokens), rendered.OutputTokens);
        Assert.Equal(UsageFormatter.Usd(row.EstimatedSpend), rendered.EstimatedSpend);
    }

    [Fact]
    public void An_unpriced_model_in_the_window_marks_the_estimate_a_partial_total_naming_the_model()
    {
        var row = new ModelUsageRow(
            "some-new-experimental-model", 5,
            new TokenCount(1_000, UsageValueStatus.Real),
            new TokenCount(500, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(null, UsageValueStatus.Unavailable));
        var stats = new UsageStatistics(
            new TokenCount(10_000, UsageValueStatus.Real),
            new EstimatedUsd(2.50m, UsageValueStatus.Estimated),
            new TokenCount(250_000, UsageValueStatus.Real),
            new EstimatedUsd(40.00m, UsageValueStatus.Estimated),
            new HistoryCoverage(31, 31))
        {
            UnpricedModels = new UnpricedModels(new[] { "some-new-experimental-model" }),
        };
        var detail = new UsageDetail(
            UsageSnapshot.Unavailable,
            stats,
            new ModelUsageBreakdown(
                new DateOnly(2026, 8, 9),
                new DateOnly(2026, 9, 8),
                new[] { row },
                new HistoryCoverage(31, 31),
                new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                UsageValueStatus.Real));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("partial total", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("some-new-experimental-model", content.ModelSectionNote);
    }

    [Fact]
    public void Extra_usage_state_is_omitted_entirely_when_the_cache_did_not_say()
    {
        var snapshot = UsageSnapshot.Unavailable with { DataSourceKind = DataSourceKind.Live };
        var detail = new UsageDetail(snapshot, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("", content.ExtraUsageLine);
    }

    [Fact]
    public void Extra_usage_state_names_on_or_off_when_the_cache_did_say()
    {
        var snapshot = UsageSnapshot.Unavailable with
        {
            DataSourceKind = DataSourceKind.Live,
            ExtraUsage = new ExtraUsageReading(ExtraUsageState.Disabled, UtcNow.AddMinutes(-5)),
        };
        var detail = new UsageDetail(snapshot, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("off", content.ExtraUsageLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_estimated_percent_carries_this_skins_own_marker_not_the_windows_skins()
    {
        var snapshot = UsageSnapshot.Unavailable with
        {
            DataSourceKind = DataSourceKind.Estimate,
            SessionUtilizationPercent = new UsagePercent(63, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(snapshot, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("63% (est.)", content.SessionLine);
        Assert.DoesNotContain("~", content.SessionLine);
    }

    [Fact]
    public void A_percent_core_could_not_establish_says_unknown_not_a_fabricated_zero()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("unknown", content.SessionLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknown", content.WeeklyLine, StringComparison.OrdinalIgnoreCase);
    }
}
