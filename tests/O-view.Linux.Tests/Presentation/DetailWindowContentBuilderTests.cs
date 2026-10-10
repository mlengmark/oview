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

    /// <summary>
    /// Gate G7 parity slice P8 (OVI-645): the header's account block never guesses a name,
    /// email or tier when <see cref="UsageDetail.Account"/> is
    /// <see cref="AccountIdentity.Unavailable"/> — the sentinel this skin's own
    /// <see cref="PanelTextFormatter.AccountDisplayName"/>/<see cref="PanelTextFormatter.AccountEmail"/>
    /// already fall back to.
    /// </summary>
    [Fact]
    public void Unavailable_account_identity_shows_the_unavailable_sentinel_never_a_guess()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("n/a", content.AccountDisplayName);
        Assert.Equal("n/a", content.AccountEmail);
        Assert.Equal("", content.AccountTierBadge);
    }

    [Fact]
    public void A_known_account_identity_is_relayed_onto_the_header_fields()
    {
        var detail = UsageDetail.Unavailable with
        {
            Account = new AccountIdentity("Jane Doe", "jane@example.com", "claude_max", UsageValueStatus.Real),
        };

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("Jane Doe", content.AccountDisplayName);
        Assert.Equal("jane@example.com", content.AccountEmail);
        Assert.Equal("claude_max", content.AccountTierBadge);
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
    }

    private static UsageDetail DetailWithPercent(double sessionPercent) =>
        new(
            UsageSnapshot.Unavailable with
            {
                DataSourceKind = DataSourceKind.Live,
                SessionUtilizationPercent = new UsagePercent(sessionPercent, UsageValueStatus.Real),
                WeeklyResetAt = new UsageInstant(UtcNow.AddDays(3), UsageValueStatus.Real),
            },
            UsageStatistics.Unavailable,
            ModelUsageBreakdown.Unavailable);

    /// <summary>ADR-0008 D10b, gate G7 parity slice P9: the band boundaries are exact, not
    /// rounded — 49 stays green, 50 and 69 are amber, 70 is red.</summary>
    [Theory]
    [InlineData(0, UsageBarBand.Green)]
    [InlineData(49, UsageBarBand.Green)]
    [InlineData(50, UsageBarBand.Amber)]
    [InlineData(69, UsageBarBand.Amber)]
    [InlineData(70, UsageBarBand.Red)]
    [InlineData(100, UsageBarBand.Red)]
    public void Session_bar_band_boundaries_are_exact(double percent, UsageBarBand expected)
    {
        var content = DetailWindowContentBuilder.Build(DetailWithPercent(percent), UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(expected, content.SessionBarBand);
        Assert.Equal(percent / 100.0, content.SessionBarFraction, precision: 10);
    }

    [Fact]
    public void An_unavailable_percent_renders_an_empty_green_bar_rather_than_a_fabricated_reading()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(0.0, content.SessionBarFraction);
        Assert.Equal(UsageBarBand.Green, content.SessionBarBand);
        Assert.Equal(0.0, content.WeeklyBarFraction);
        Assert.Equal(UsageBarBand.Green, content.WeeklyBarBand);
    }

    /// <summary>ADR-0008 D9f, gate G7 parity slice P9: no plan data at all hides the weekly
    /// row entirely, replacing the earlier "unknown" line this skin used to render for the
    /// fully unavailable detail.</summary>
    [Fact]
    public void No_plan_data_at_all_hides_the_weekly_row()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(WeeklyBarState.Hidden, content.WeeklyState);
        Assert.Equal("", content.WeeklyLine);
    }

    /// <summary>Plan data present but no weekly reset observed yet names the fix, worded
    /// independently from the Windows skin (ADR-0003).</summary>
    [Fact]
    public void Plan_data_with_no_weekly_reset_observed_names_the_fix_rather_than_an_indefinite_wait()
    {
        var snapshot = UsageSnapshot.Unavailable with
        {
            DataSourceKind = DataSourceKind.Live,
            WeeklyUtilizationPercent = new UsagePercent(22, UsageValueStatus.Real),
        };
        var detail = new UsageDetail(snapshot, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(WeeklyBarState.NotKnown, content.WeeklyState);
        Assert.Contains("not known", content.WeeklyLine, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("waiting", content.WeeklyLine, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("", content.WeeklyUnknownHint);
        Assert.Contains("/usage", content.WeeklyUnknownHint);
    }

    /// <summary>A reported weekly reset renders the existing <c>WeeklyReset</c> line, unchanged
    /// by this slice, and carries no hint (the row is not in the <c>NotKnown</c> state).</summary>
    [Fact]
    public void A_known_weekly_reset_carries_no_unknown_hint()
    {
        var snapshot = UsageSnapshot.Unavailable with
        {
            DataSourceKind = DataSourceKind.Live,
            WeeklyUtilizationPercent = new UsagePercent(22, UsageValueStatus.Real),
            WeeklyResetAt = new UsageInstant(UtcNow.AddDays(3), UsageValueStatus.Real),
        };
        var detail = new UsageDetail(snapshot, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(WeeklyBarState.Known, content.WeeklyState);
        Assert.Contains("Resets in", content.WeeklyLine);
        Assert.Equal("", content.WeeklyUnknownHint);
    }

    private static ModelUsageBreakdown BreakdownWith(params ModelUsageRow[] rows) => new(
        new DateOnly(2026, 8, 9),
        new DateOnly(2026, 9, 8),
        rows,
        new HistoryCoverage(31, 31),
        new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
        UsageValueStatus.Real);

    /// <summary>ADR-0008 D10b, gate G7 parity slice P10: all four tiles always build, in order,
    /// with Core's own figures run through the existing formatters.</summary>
    [Fact]
    public void Builds_four_statistics_tiles_in_order_with_cores_own_figures()
    {
        var stats = new UsageStatistics(
            new TokenCount(10_000, UsageValueStatus.Real),
            new EstimatedUsd(2.50m, UsageValueStatus.Estimated),
            new TokenCount(250_000, UsageValueStatus.Real),
            new EstimatedUsd(40.00m, UsageValueStatus.Estimated),
            new HistoryCoverage(31, 31));
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(4, content.StatisticsTiles.Count);
        Assert.Equal(StatisticsTileKind.OutputTokensToday, content.StatisticsTiles[0].Kind);
        Assert.Equal(UsageFormatter.Tokens(stats.OutputTokensToday), content.StatisticsTiles[0].Value);
        Assert.Equal(StatisticsTileKind.EstimatedValueToday, content.StatisticsTiles[1].Kind);
        Assert.Equal(UsageFormatter.Usd(stats.EstimatedSpendToday), content.StatisticsTiles[1].Value);
        Assert.Equal(StatisticsTileKind.OutputTokensWindow31d, content.StatisticsTiles[2].Kind);
        Assert.Equal(UsageFormatter.Tokens(stats.OutputTokensWindow31d), content.StatisticsTiles[2].Value);
        Assert.Equal(StatisticsTileKind.EstimatedValueWindow31d, content.StatisticsTiles[3].Kind);
        Assert.Equal(UsageFormatter.Usd(stats.EstimatedValueWindow31d), content.StatisticsTiles[3].Value);
    }

    /// <summary>Neither "today" tile can ever flip — Core only aggregates the per-model
    /// breakdown over the 31-day window (<see cref="ModelUsageBreakdown"/>'s own remarks), so
    /// there is nothing a "today" tile could show a proportion of.</summary>
    [Fact]
    public void Today_tiles_never_flip_even_with_a_real_populated_breakdown()
    {
        var row = new ModelUsageRow(
            "claude-sonnet-5", 10,
            new TokenCount(1_000, UsageValueStatus.Real),
            new TokenCount(500, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(1.00m, UsageValueStatus.Estimated));
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, BreakdownWith(row));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.False(content.StatisticsTiles[0].CanFlip);
        Assert.Empty(content.StatisticsTiles[0].Breakdown);
        Assert.False(content.StatisticsTiles[1].CanFlip);
        Assert.Empty(content.StatisticsTiles[1].Breakdown);
    }

    /// <summary>A 31-day tile with a real, populated breakdown can flip, and its segments are a
    /// proportion of Core's own rows — never a sum this skin performs (D10b) — so they sum to 1.</summary>
    [Fact]
    public void A_31d_tile_with_a_real_breakdown_can_flip_and_its_segments_sum_to_one()
    {
        var rowA = new ModelUsageRow(
            "claude-sonnet-5", 10,
            new TokenCount(1_000, UsageValueStatus.Real),
            new TokenCount(300, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(3.00m, UsageValueStatus.Estimated));
        var rowB = new ModelUsageRow(
            "claude-haiku-5", 5,
            new TokenCount(500, UsageValueStatus.Real),
            new TokenCount(100, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(1.00m, UsageValueStatus.Estimated));
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, BreakdownWith(rowA, rowB));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var outputTokensTile = content.StatisticsTiles[2];
        Assert.True(outputTokensTile.CanFlip);
        Assert.Equal(2, outputTokensTile.Breakdown.Count);
        Assert.Equal(1.0, outputTokensTile.Breakdown.Sum(s => s.Fraction), precision: 10);
        Assert.Equal(0.75, outputTokensTile.Breakdown[0].Fraction, precision: 10);

        var estimatedValueTile = content.StatisticsTiles[3];
        Assert.True(estimatedValueTile.CanFlip);
        Assert.Equal(1.0, estimatedValueTile.Breakdown.Sum(s => s.Fraction), precision: 10);
    }

    /// <summary>A 31-day tile with nothing recorded is disabled — "nothing to break down" means
    /// no glyph and no click, not an empty but flippable breakdown.</summary>
    [Fact]
    public void A_31d_tile_with_an_empty_real_breakdown_cannot_flip()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, BreakdownWith());

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.False(content.StatisticsTiles[2].CanFlip);
        Assert.False(content.StatisticsTiles[3].CanFlip);
    }

    /// <summary>A 31-day tile cannot flip when Core could not read the ledger at all — the same
    /// "unavailable, not empty" distinction <see cref="ModelUsageBreakdown"/>'s own remarks draw.</summary>
    [Fact]
    public void A_31d_tile_cannot_flip_when_the_breakdown_is_unavailable()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.False(content.StatisticsTiles[2].CanFlip);
        Assert.False(content.StatisticsTiles[3].CanFlip);
    }

    /// <summary>An unpriced model contributes nothing to the "Est. value" tile's breakdown (its
    /// own <see cref="ModelUsageRow.EstimatedSpend"/> is unavailable) but still appears in the
    /// "Output tokens" tile's breakdown, since output tokens were recorded either way.</summary>
    [Fact]
    public void An_unpriced_model_is_excluded_from_the_value_breakdown_but_not_the_token_breakdown()
    {
        var priced = new ModelUsageRow(
            "claude-sonnet-5", 10,
            new TokenCount(1_000, UsageValueStatus.Real),
            new TokenCount(400, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(4.00m, UsageValueStatus.Estimated));
        var unpriced = new ModelUsageRow(
            "some-new-model", 2,
            new TokenCount(100, UsageValueStatus.Real),
            new TokenCount(100, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new TokenCount(0, UsageValueStatus.Real),
            new EstimatedUsd(null, UsageValueStatus.Unavailable));
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, BreakdownWith(priced, unpriced));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.True(content.StatisticsTiles[2].CanFlip);
        Assert.Equal(2, content.StatisticsTiles[2].Breakdown.Count);

        Assert.True(content.StatisticsTiles[3].CanFlip);
        Assert.Single(content.StatisticsTiles[3].Breakdown);
        Assert.Equal("claude-sonnet-5", content.StatisticsTiles[3].Breakdown[0].ModelId);
    }

    /// <summary>ADR-0008 D11b's <c>CoverageCaptionFixture</c> pin: the caption always states
    /// both counts, even for a fully-covered window — unlike <see cref="PanelStatisticsFormatter.CoverageNote"/>,
    /// which hides itself in that case.</summary>
    [Fact]
    public void Coverage_caption_always_states_both_counts_even_when_fully_covered()
    {
        var stats = UsageStatistics.Unavailable with { HistoryCoverage = new HistoryCoverage(31, 31) };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("31", content.CoverageCaption);
        Assert.Contains("31 of 31", content.CoverageCaption);
    }

    /// <summary>The caption counts days Core has data <b>for</b>, not days with usage — the
    /// same distinction <see cref="HistoryCoverage"/>'s own remarks draw.</summary>
    [Fact]
    public void Coverage_caption_states_a_partial_count()
    {
        var stats = UsageStatistics.Unavailable with { HistoryCoverage = new HistoryCoverage(12, 31) };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("12 of 31 days recorded", content.CoverageCaption);
    }
}
