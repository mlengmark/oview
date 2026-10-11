using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves <see cref="DetailWindowContentBuilder"/> (ADR-0008 slice 6, OVI-386) renders only
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
        Assert.Equal("No data", content.Freshness);
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

        Assert.Equal("unknown", content.AccountDisplayName);
        Assert.Equal("unknown", content.AccountEmail);
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
        Assert.Contains("unavailable", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
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
    public void An_unpriced_model_in_the_window_marks_the_estimate_a_subtotal_naming_the_model()
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

        Assert.Contains("subtotal", content.ModelSectionNote, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>ADR-0008 D9f, gate G7 parity slice P9: no plan data at all hides the weekly row.</summary>
    [Fact]
    public void No_plan_data_at_all_hides_the_weekly_row()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(WeeklyBarState.Hidden, content.WeeklyState);
        Assert.Equal("", content.WeeklyLine);
    }

    /// <summary>Plan data present but no weekly reset observed yet names the fix rather than
    /// describing an indefinite wait.</summary>
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
        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(400, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(4.00m, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(rowA, rowB));

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
        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(500, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(4.00m, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(priced, unpriced));

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

    /// <summary>
    /// Gate G7 parity slice P17 (OVI-702): the standing off-plan usage section's heading is
    /// fixed, not a formatted value, and always present — it does not depend on
    /// <see cref="UsageStatistics.Divergence"/> the way <see cref="DetailWindowContent.OffPlanBannerTitle"/>
    /// does.
    /// </summary>
    [Fact]
    public void Off_plan_usage_section_title_is_always_present()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("Off-plan usage · last 31 days", content.OffPlanUsageTitle);
    }

    /// <summary>
    /// Gate G7 parity slice P17 (OVI-702): no priced rate card exists yet (OVI-592), so
    /// <see cref="UsageStatistics.OffPlanUsageAmount"/> is always
    /// <see cref="UsageValueStatus.Unavailable"/> today. Proves the section renders the existing
    /// "unknown" sentinel rather than a fabricated dollar figure, whether or not credit-billed
    /// usage was recorded.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Off_plan_usage_value_never_fabricates_a_dollar_figure_without_a_rate_card(bool? hasCreditUsage)
    {
        var stats = UsageStatistics.Unavailable with
        {
            HasCreditUsage = new UsageFlag(hasCreditUsage, hasCreditUsage is null ? UsageValueStatus.Unavailable : UsageValueStatus.Real),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, ModelUsageBreakdown.Unavailable);

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("unknown", content.OffPlanUsageValue);
        Assert.DoesNotContain('$', content.OffPlanUsageValue);

        // The caption must not fabricate either: a null hasCreditUsage (Core could not look)
        // must not read as the checked negative "No credit-billed usage ... recorded"
        // (OVI-702 review).
        if (hasCreditUsage is null)
        {
            Assert.DoesNotContain("No credit-billed usage", content.OffPlanUsageCaption);
        }
    }

    /// <summary>
    /// Gate G7 parity slice P17 (OVI-702): the two-clause caption's wording tracks
    /// <see cref="UsageStatistics.HasCreditUsage"/> — the "estimated at published API rates"
    /// clause names the credit-billed models only once usage against them was actually recorded;
    /// otherwise the caption says so plainly rather than describing a spend that never happened.
    /// A review of this slice (OVI-702/PR #122) found the caption had collapsed
    /// <see cref="UsageValueStatus.Unavailable"/> ("Core could not look") into the same wording
    /// as <c>(false, Real)</c> ("Core looked and found none") — a fabricated checked negative.
    /// This test pins that the three states each read distinctly.
    /// </summary>
    [Fact]
    public void Off_plan_usage_caption_differs_by_whether_credit_usage_was_recorded()
    {
        var withUsage = UsageStatistics.Unavailable with { HasCreditUsage = new UsageFlag(true, UsageValueStatus.Real) };
        var withoutUsage = UsageStatistics.Unavailable with { HasCreditUsage = new UsageFlag(false, UsageValueStatus.Real) };
        var unknownUsage = UsageStatistics.Unavailable with { HasCreditUsage = new UsageFlag(null, UsageValueStatus.Unavailable) };

        var contentWithUsage = DetailWindowContentBuilder.Build(
            new UsageDetail(UsageSnapshot.Unavailable, withUsage, ModelUsageBreakdown.Unavailable), UtcNow, TimeZoneInfo.Utc);
        var contentWithoutUsage = DetailWindowContentBuilder.Build(
            new UsageDetail(UsageSnapshot.Unavailable, withoutUsage, ModelUsageBreakdown.Unavailable), UtcNow, TimeZoneInfo.Utc);
        var contentUnknownUsage = DetailWindowContentBuilder.Build(
            new UsageDetail(UsageSnapshot.Unavailable, unknownUsage, ModelUsageBreakdown.Unavailable), UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("published API rates", contentWithUsage.OffPlanUsageCaption);
        Assert.Contains("check your billing page", contentWithUsage.OffPlanUsageCaption);
        Assert.Contains("No credit-billed usage", contentWithoutUsage.OffPlanUsageCaption);
        Assert.DoesNotContain("No credit-billed usage", contentUnknownUsage.OffPlanUsageCaption);
        Assert.Contains("could not determine", contentUnknownUsage.OffPlanUsageCaption);
        Assert.NotEqual(contentWithUsage.OffPlanUsageCaption, contentWithoutUsage.OffPlanUsageCaption);
        Assert.NotEqual(contentWithoutUsage.OffPlanUsageCaption, contentUnknownUsage.OffPlanUsageCaption);
    }

    /// <summary>
    /// Gate G7 parity slice P18 (OVI-716): the "no usage data" banner appears exactly when
    /// <see cref="UsageSnapshot.DataSourceKind"/> is <see cref="DataSourceKind.Unavailable"/> —
    /// absent for every other reading, same as <see cref="DetailWindowContent.OffPlanBannerTitle"/>'s
    /// own empty-means-hidden convention.
    /// </summary>
    [Fact]
    public void No_usage_data_banner_is_present_only_when_the_data_source_is_unavailable()
    {
        var unavailable = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);

        Assert.NotEmpty(unavailable.NoUsageDataBannerTitle);
        Assert.NotEmpty(unavailable.NoUsageDataBannerDetail);

        var live = new UsageDetail(
            new UsageSnapshot(
                DataSourceKind.Live,
                UtcNow,
                new UsagePercent(10, UsageValueStatus.Real),
                new UsageInstant(UtcNow.AddHours(5), UsageValueStatus.Real),
                new UsagePercent(5, UsageValueStatus.Real),
                new UsageInstant(UtcNow.AddDays(1), UsageValueStatus.Real),
                UsageLevel.Green),
            UsageStatistics.Unavailable,
            ModelUsageBreakdown.Unavailable);
        var withData = DetailWindowContentBuilder.Build(live, UtcNow, TimeZoneInfo.Utc);

        Assert.Equal("", withData.NoUsageDataBannerTitle);
        Assert.Equal("", withData.NoUsageDataBannerDetail);
    }

    /// <summary>
    /// Gate G7 parity slice P18 (OVI-716, source README: "it will state which path it checked
    /// and what it found there — what it observed, never an assertion about your machine it
    /// hasn't verified"). Pins that the banner names the local sources O-view actually checks and
    /// never asserts anything about the account or the user's own activity beyond that.
    /// </summary>
    [Fact]
    public void No_usage_data_banner_states_what_was_checked_and_makes_no_machine_claim()
    {
        var content = DetailWindowContentBuilder.Build(UsageDetail.Unavailable, UtcNow, TimeZoneInfo.Utc);
        var banner = content.NoUsageDataBannerTitle + " " + content.NoUsageDataBannerDetail;

        Assert.Contains("Claude Code", banner);
        Assert.Contains("Claude Desktop", banner);
        Assert.DoesNotContain("you have", banner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("your account", banner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never used", banner, StringComparison.OrdinalIgnoreCase);
    }

    private static ModelUsageRow Row(string modelId, long tokens, decimal? value) => new(
        modelId, 1,
        new TokenCount(tokens, UsageValueStatus.Real),
        new TokenCount(tokens, UsageValueStatus.Real),
        new TokenCount(0, UsageValueStatus.Real),
        new TokenCount(0, UsageValueStatus.Real),
        value is { } v ? new EstimatedUsd(v, UsageValueStatus.Estimated) : new EstimatedUsd(null, UsageValueStatus.Unavailable));

    /// <summary>ADR-0008 D10b, gate G7 parity slice P11's own <c>ModelColourOrderFixture</c>:
    /// colour is decided once for the whole panel from the widest window
    /// (<see cref="ModelUsageBreakdown.Rows"/>), and both 31-day tiles colour by that one order,
    /// at every model count from a single model up to five — three named slots at 1-3, two named
    /// plus a folded "Other" at 4 and 5.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Models_keep_the_same_colour_slot_on_every_tile_at_this_model_count(int modelCount)
    {
        var rows = new ModelUsageRow[modelCount];
        long outputTotal = 0;
        decimal valueTotal = 0m;
        for (var i = 0; i < modelCount; i++)
        {
            var tokens = (modelCount - i) * 10_000L;
            var value = (modelCount - i) * 4.00m;
            outputTotal += tokens;
            valueTotal += value;
            rows[i] = Row($"model-{i}", tokens, value);
        }

        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(outputTotal, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(valueTotal, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(rows));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var tokenTile = content.StatisticsTiles[2];
        var valueTile = content.StatisticsTiles[3];

        var tokenSlots = tokenTile.Breakdown.ToDictionary(s => s.ModelId, s => s.ColourSlot);
        var valueSlots = valueTile.Breakdown.ToDictionary(s => s.ModelId, s => s.ColourSlot);
        foreach (var modelId in tokenSlots.Keys.Intersect(valueSlots.Keys))
        {
            Assert.Equal(tokenSlots[modelId], valueSlots[modelId]);
        }

        var expectedSegments = modelCount <= 3 ? modelCount : 3;
        Assert.Equal(expectedSegments, tokenTile.Breakdown.Count);
        Assert.Equal(expectedSegments, valueTile.Breakdown.Count);

        if (modelCount > 3)
        {
            Assert.Contains(tokenTile.Breakdown, s => s.ModelId == "Other" && s.ColourSlot == -1);
            Assert.Contains(valueTile.Breakdown, s => s.ModelId == "Other" && s.ColourSlot == -1);
        }
    }

    /// <summary>Reproduces the source's own first-round bug directly (source <c>ui-spec.md</c>
    /// §2): one model ranks first by tokens and second by value — its colour must not follow
    /// either tile's own ranking, only the panel-wide order Core's 31-day token ranking already
    /// establishes.</summary>
    [Fact]
    public void A_models_colour_does_not_follow_its_own_ranking_within_a_single_tile()
    {
        var rowA = Row("model-a", tokens: 100_000, value: 1.00m); // ranks first by tokens, second by value
        var rowB = Row("model-b", tokens: 50_000, value: 9.00m); // ranks second by tokens, first by value

        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(150_000, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(10.00m, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(rowA, rowB));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var tokenBreakdown = content.StatisticsTiles[2].Breakdown;
        var valueBreakdown = content.StatisticsTiles[3].Breakdown;

        var tokenSlotA = tokenBreakdown.Single(s => s.ModelId == "model-a").ColourSlot;
        var valueSlotA = valueBreakdown.Single(s => s.ModelId == "model-a").ColourSlot;
        Assert.Equal(tokenSlotA, valueSlotA);

        var tokenSlotB = tokenBreakdown.Single(s => s.ModelId == "model-b").ColourSlot;
        var valueSlotB = valueBreakdown.Single(s => s.ModelId == "model-b").ColourSlot;
        Assert.Equal(tokenSlotB, valueSlotB);

        Assert.NotEqual(tokenSlotA, tokenSlotB);
    }

    /// <summary>Gate G7 parity slice P11: a fourth and fifth model fold into a neutral "Other"
    /// segment; an unpriced model elsewhere in the window is excluded from the value breakdown
    /// and counted into this tile's own "excl. N unpriced" note rather than silently vanishing.
    /// An unrecognised model id renders exactly as it appears in the data — never renamed.</summary>
    [Fact]
    public void A_fourth_and_later_model_folds_into_other_and_an_unpriced_model_is_noted_not_hidden()
    {
        var rows = new[]
        {
            Row("model-top", tokens: 100_000, value: 40.00m),
            Row("model-second", tokens: 50_000, value: 20.00m),
            Row("model-third", tokens: 30_000, value: 12.00m),
            Row("model-fourth", tokens: 20_000, value: 8.00m),
            Row("weird-vendor-id-07", tokens: 10_000, value: null),
        };

        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(210_000, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(80.00m, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(rows));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var tokenTile = content.StatisticsTiles[2];
        var valueTile = content.StatisticsTiles[3];

        // Token tile: nothing is unpriced, so all five contribute — two named, three folded.
        Assert.Equal("", tokenTile.Note);
        Assert.Equal(3, tokenTile.Breakdown.Count);
        Assert.Contains(tokenTile.Breakdown, s => s.ModelId == "Other");

        // Value tile: the unrecognised model is unpriced — excluded and counted, never hidden.
        Assert.Equal("excl. 1 unpriced", valueTile.Note);
        Assert.Equal(3, valueTile.Breakdown.Count);
        var other = valueTile.Breakdown.Single(s => s.ModelId == "Other");
        Assert.Equal(-1, other.ColourSlot);

        // Three models are in the global "rest" group (model-third, model-fourth and the
        // unpriced weird-vendor-id-07), but the unpriced one has nothing to show on this tile —
        // the caption counts what is actually folded in here, not the group's raw size.
        Assert.Equal("2 more models", other.HoverCaption);

        Assert.Contains(content.ModelRows, r => r.ModelId == "weird-vendor-id-07");
    }

    /// <summary>A single model left outside the colour order keeps its own name — "Other · 1
    /// more models" would be a clumsy way to say the one model it names (source
    /// <c>ui-spec.md</c> §2) — but still takes the neutral colour, since it is not one of the
    /// panel's coloured models. This is a per-tile decision: the same window has four models
    /// globally, but the value tile's second "rest" model is unpriced, leaving only one visible
    /// leftover on that tile specifically.</summary>
    [Fact]
    public void A_single_visible_leftover_keeps_its_own_name_with_the_neutral_colour()
    {
        var rows = new[]
        {
            Row("model-top", tokens: 100_000, value: 40.00m),
            Row("model-second", tokens: 50_000, value: 20.00m),
            Row("model-third-alone", tokens: 30_000, value: 12.00m),
            Row("model-fourth-unpriced", tokens: 20_000, value: null),
        };

        var stats = UsageStatistics.Unavailable with
        {
            OutputTokensWindow31d = new TokenCount(200_000, UsageValueStatus.Real),
            EstimatedValueWindow31d = new EstimatedUsd(72.00m, UsageValueStatus.Estimated),
        };
        var detail = new UsageDetail(UsageSnapshot.Unavailable, stats, BreakdownWith(rows));

        var content = DetailWindowContentBuilder.Build(detail, UtcNow, TimeZoneInfo.Utc);

        var valueTile = content.StatisticsTiles[3];
        Assert.DoesNotContain(valueTile.Breakdown, s => s.ModelId == "Other");
        var leftover = valueTile.Breakdown.Single(s => s.ModelId == "model-third-alone");
        Assert.Equal(-1, leftover.ColourSlot);
        Assert.Equal("model-third-alone", leftover.LegendLabel);
        Assert.Equal("excl. 1 unpriced", valueTile.Note);
    }
}
