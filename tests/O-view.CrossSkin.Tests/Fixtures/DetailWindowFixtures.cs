using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="DetailWindowFixture"/>s (ADR-0003, ADR-0008 D2/D9, OVI-324,
/// OVI-329). Covers the full <see cref="UsageDetail"/> contract: ordinary live usage with extra
/// usage known on, a stale reading with extra usage known off, a jsonl-fallback reading with
/// extra usage unknown, the fully unavailable "no data" case, a recorded-but-empty model window
/// versus a fully unavailable breakdown, an unpriced model alongside
/// <see cref="UsageStatistics.UnpricedModels"/>, and a stale detail whose
/// <see cref="UsageSnapshot.LastIngestAt"/> is old.
/// </summary>
public static class DetailWindowFixtures
{
    private static readonly DateTimeOffset SessionReset = new(2026, 9, 8, 20, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeeklyReset = new(2026, 9, 7, 23, 0, 0, TimeSpan.Zero); // a Monday

    /// <summary>
    /// Ordinary live usage, extra usage known on, fetched the same day as ingest. Pins the
    /// percentages, both reset instants (as <see cref="GoldenMasterFixture"/> already does for
    /// the tooltip's slice of this same snapshot shape), and the extra-usage reading's own
    /// fetched-at time. Statistics and models are unavailable here — this fixture pins only the
    /// snapshot slice; see the dedicated fixtures below for the statistics/model cases.
    /// </summary>
    public static readonly DetailWindowFixture OrdinaryLiveReadingExtraUsageOn = new(
        Name: "ordinary-live-reading-extra-usage-on",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(63, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(22, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Amber)
            {
                ExtraUsage = new ExtraUsageReading(ExtraUsageState.Enabled, new DateTimeOffset(2026, 9, 8, 20, 40, 0, TimeSpan.Zero)),
            },
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("63%"),
            ContentFact.Contains("22%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            ContentFact.Contains("20:40"),
        });

    /// <summary>
    /// A reading whose <see cref="UsageSnapshot.LastIngestAt"/> is days old — the detail window's
    /// own staleness signal, distinct from <see cref="DataSourceKind.Stale"/> (which names where
    /// the reading came from, not how old it is).
    /// </summary>
    public static readonly DetailWindowFixture StaleDetailOldLastIngestAt = new(
        Name: "stale-detail-old-last-ingest-at",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
                new UsagePercent(30, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(15, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("30%"),
            new ContentFact(
                "names the earlier-day stamp for a last-ingest reading that is days old",
                rendered => rendered.Contains("Sep", StringComparison.Ordinal)),
        });

    /// <summary>
    /// A stale reading, extra usage known off, fetched on an earlier day than the ingest —
    /// mirrors <c>OffPlanFixtures</c>' earlier-day convention (a month-abbreviation stamp, not
    /// a bare time, for a reading that is not from today).
    /// </summary>
    public static readonly DetailWindowFixture StaleReadingExtraUsageOff = new(
        Name: "stale-reading-extra-usage-off",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Stale,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(40, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(10, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green)
            {
                ExtraUsage = new ExtraUsageReading(ExtraUsageState.Disabled, new DateTimeOffset(2026, 9, 4, 10, 15, 0, TimeSpan.Zero)),
            },
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("40%"),
            ContentFact.Contains("10%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            new ContentFact(
                "names the earlier-day stamp, not a bare time, for a reading from a prior day",
                rendered => rendered.Contains("Sep", StringComparison.Ordinal)),
            new ContentFact(
                "states extra usage is switched off",
                rendered => rendered.Contains("off", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// A jsonl-fallback reading where Claude Code's own cache did not say whether extra usage
    /// is on or off (<see cref="UsageSnapshot.ExtraUsage"/> is <c>null</c>) — the "cache did
    /// not say" case ADR-0001 (OVI-168) distinguishes from a fabricated <c>Disabled</c>.
    /// </summary>
    public static readonly DetailWindowFixture JsonlFallbackReadingExtraUsageUnknown = new(
        Name: "jsonl-fallback-reading-extra-usage-unknown",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.JsonlFallback,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(5, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(1, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("5%"),
            ContentFact.Contains("1%"),
            ContentFact.Contains("20:59"),
            ContentFact.Contains("Mon 23:00"),
            new ContentFact(
                "never claims extra usage is definitely on or off when the cache did not say",
                rendered => !rendered.Contains("Enabled", StringComparison.Ordinal)
                    && !rendered.Contains("Disabled", StringComparison.Ordinal)),
        });

    /// <summary>
    /// A model window Core read successfully but found no recorded activity in —
    /// <see cref="UsageValueStatus.Real"/> with an empty <see cref="ModelUsageBreakdown.Rows"/>,
    /// the "window recorded, no activity" case <see cref="ModelUsageBreakdown"/>'s remarks
    /// distinguish from <see cref="ModelBreakdownUnavailable"/> below.
    /// </summary>
    public static readonly DetailWindowFixture RecordedWindowNoActivity = new(
        Name: "recorded-window-no-activity",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(2, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(0, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: new ModelUsageBreakdown(
                FromLocalDate: new DateOnly(2026, 8, 9),
                ToLocalDate: new DateOnly(2026, 9, 8),
                Rows: Array.Empty<ModelUsageRow>(),
                Coverage: new HistoryCoverage(31, 31),
                Rates: new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                Status: UsageValueStatus.Real)),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "states the window was recorded with no activity, not that data is missing",
                rendered => rendered.Contains("no activity", StringComparison.OrdinalIgnoreCase)
                    || rendered.Contains("no usage", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// The contrasting case to <see cref="RecordedWindowNoActivity"/>: Core could not read the
    /// ledger at all, so the breakdown is fully <see cref="ModelUsageBreakdown.Unavailable"/>
    /// rather than a real-but-empty one. A skin owes the user different words for each.
    /// </summary>
    public static readonly DetailWindowFixture ModelBreakdownUnavailable = new(
        Name: "model-breakdown-unavailable",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(2, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(0, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "admits the per-model breakdown could not be read, not a fabricated empty window",
                rendered => !rendered.Contains("no activity", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// A model in the window Core could not price — <see cref="ModelUsageRow.EstimatedSpend"/>
    /// unavailable for that row — alongside <see cref="UsageStatistics.UnpricedModels"/> naming
    /// the same model id, the pairing D9a requires a skin to read together.
    /// </summary>
    public static readonly DetailWindowFixture UnpricedModelInWindow = new(
        Name: "unpriced-model-in-window",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(55, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(18, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Amber),
            Statistics: new UsageStatistics(
                OutputTokensToday: new TokenCount(10_000, UsageValueStatus.Real),
                EstimatedSpendToday: new EstimatedUsd(2.50m, UsageValueStatus.Estimated),
                OutputTokensWindow31d: new TokenCount(250_000, UsageValueStatus.Real),
                EstimatedValueWindow31d: new EstimatedUsd(40.00m, UsageValueStatus.Estimated),
                HistoryCoverage: new HistoryCoverage(31, 31))
            {
                UnpricedModels = new UnpricedModels(new[] { "some-new-experimental-model" }),
            },
            Models: new ModelUsageBreakdown(
                FromLocalDate: new DateOnly(2026, 8, 9),
                ToLocalDate: new DateOnly(2026, 9, 8),
                Rows: new[]
                {
                    new ModelUsageRow(
                        ModelId: "claude-sonnet-5",
                        RequestCount: 120,
                        InputTokens: new TokenCount(500_000, UsageValueStatus.Real),
                        OutputTokens: new TokenCount(250_000, UsageValueStatus.Real),
                        CacheCreationTokens: new TokenCount(10_000, UsageValueStatus.Real),
                        CacheReadTokens: new TokenCount(20_000, UsageValueStatus.Real),
                        EstimatedSpend: new EstimatedUsd(40.00m, UsageValueStatus.Estimated)),
                    new ModelUsageRow(
                        ModelId: "some-new-experimental-model",
                        RequestCount: 5,
                        InputTokens: new TokenCount(1_000, UsageValueStatus.Real),
                        OutputTokens: new TokenCount(500, UsageValueStatus.Real),
                        CacheCreationTokens: new TokenCount(0, UsageValueStatus.Real),
                        CacheReadTokens: new TokenCount(0, UsageValueStatus.Real),
                        EstimatedSpend: new EstimatedUsd(null, UsageValueStatus.Unavailable)),
                },
                Coverage: new HistoryCoverage(31, 31),
                Rates: new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                Status: UsageValueStatus.Real)),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("claude-sonnet-5"),
            ContentFact.Contains("some-new-experimental-model"),
            new ContentFact(
                "names the unpriced model rather than fabricating a figure for it",
                rendered => rendered.Contains("some-new-experimental-model", StringComparison.Ordinal)),
            new ContentFact(
                "marks the 31-day estimate a subtotal, not a total, when a model is unpriced",
                rendered => rendered.Contains("subtotal", StringComparison.OrdinalIgnoreCase)
                    || rendered.Contains("unpriced", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// Gate G7 parity slice P12 (OVI-667): a detail with real <see cref="UsageDetail.TokensToday"/>
    /// and <see cref="UsageDetail.Tokens31d"/> — the two <see cref="TokenKindTotals"/> no fixture
    /// above this one ever populates (both default to <see cref="TokenKindTotals.Unavailable"/>).
    /// Reuses <see cref="UnpricedModelInWindow"/>'s own snapshot/statistics/model slice so this
    /// fixture only adds what P12 itself needs.
    /// </summary>
    public static readonly DetailWindowFixture TokenKindTotalsPopulated = new(
        Name: "token-kind-totals-populated",
        Detail: UnpricedModelInWindow.Detail with
        {
            TokensToday = new TokenKindTotals(
                new DateOnly(2026, 9, 8),
                new DateOnly(2026, 9, 8),
                Input: new TokenKindAmount(new TokenCount(4_000, UsageValueStatus.Real), new EstimatedUsd(0.40m, UsageValueStatus.Estimated)),
                Output: new TokenKindAmount(new TokenCount(3_000, UsageValueStatus.Real), new EstimatedUsd(0.90m, UsageValueStatus.Estimated)),
                CacheCreation: new TokenKindAmount(new TokenCount(2_000, UsageValueStatus.Real), new EstimatedUsd(0.08m, UsageValueStatus.Estimated)),
                CacheRead: new TokenKindAmount(new TokenCount(1_000, UsageValueStatus.Real), new EstimatedUsd(0.02m, UsageValueStatus.Estimated)),
                Total: new TokenCount(10_000, UsageValueStatus.Real),
                Rates: new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                Status: UsageValueStatus.Real),
            Tokens31d = new TokenKindTotals(
                new DateOnly(2026, 8, 9),
                new DateOnly(2026, 9, 8),
                Input: new TokenKindAmount(new TokenCount(100_000, UsageValueStatus.Real), new EstimatedUsd(10.00m, UsageValueStatus.Estimated)),
                Output: new TokenKindAmount(new TokenCount(80_000, UsageValueStatus.Real), new EstimatedUsd(24.00m, UsageValueStatus.Estimated)),
                CacheCreation: new TokenKindAmount(new TokenCount(40_000, UsageValueStatus.Real), new EstimatedUsd(1.60m, UsageValueStatus.Estimated)),
                CacheRead: new TokenKindAmount(new TokenCount(30_000, UsageValueStatus.Real), new EstimatedUsd(0.30m, UsageValueStatus.Estimated)),
                Total: new TokenCount(250_000, UsageValueStatus.Real),
                Rates: new RateCardStamp(RateCardSource.Bundled, new DateOnly(2026, 9, 8), isStale: false),
                Status: UsageValueStatus.Real),
        },
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "today's four kinds sum to the carried total of 10000",
                _ => 4_000 + 3_000 + 2_000 + 1_000 == 10_000),
            new ContentFact(
                "the 31-day window's four kinds sum to the carried total of 250000",
                _ => 100_000 + 80_000 + 40_000 + 30_000 == 250_000),
        });

    /// <summary>
    /// The fully unavailable detail — the "no data" explanation D2 requires. Never a
    /// fabricated zero, same rule as <see cref="UsageStatisticsFixtures.Unavailable"/>.
    /// </summary>
    public static readonly DetailWindowFixture UnavailableNoDataExplanation = new(
        Name: "unavailable-no-data-explanation",
        Detail: UsageDetail.Unavailable,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "never renders a fabricated 0% session or weekly figure",
                rendered => !rendered.Contains("0%", StringComparison.Ordinal)),
            new ContentFact(
                "never claims extra usage is on or off when there is no data at all",
                rendered => !rendered.Contains("Enabled", StringComparison.Ordinal)
                    && !rendered.Contains("Disabled", StringComparison.Ordinal)),
        });

    /// <summary>
    /// Gate G7 parity slice P9 (OVI-648): the usage bars' 50/70 colour band at its exact
    /// boundaries — 49 stays green, 50 is already amber. Named per-boundary rather than one
    /// shared helper so a render-proof PNG file name states which boundary it captured.
    /// </summary>
    private static DetailWindowFixture AtSessionPercent(string name, double percent) => new(
        Name: name,
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(percent, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(10, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[] { ContentFact.Contains($"{(int)percent}%") });

    public static readonly DetailWindowFixture SessionBandBoundaryGreen49 = AtSessionPercent("session-band-boundary-green-49", 49);
    public static readonly DetailWindowFixture SessionBandBoundaryAmber50 = AtSessionPercent("session-band-boundary-amber-50", 50);
    public static readonly DetailWindowFixture SessionBandBoundaryAmber69 = AtSessionPercent("session-band-boundary-amber-69", 69);
    public static readonly DetailWindowFixture SessionBandBoundaryRed70 = AtSessionPercent("session-band-boundary-red-70", 70);

    /// <summary>
    /// Gate G7 parity slice P9 (OVI-648): plan data is present (a live session percent) but no
    /// weekly reset has ever been observed — the <c>NotKnown</c> weekly state, distinct from
    /// <see cref="UnavailableNoDataExplanation"/>'s fully-<c>Hidden</c> row below.
    /// </summary>
    public static readonly DetailWindowFixture WeeklyResetNotKnownWithPlanData = new(
        Name: "weekly-reset-not-known-with-plan-data",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(35, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(20, UsageValueStatus.Real),
                new UsageInstant(null, UsageValueStatus.Unavailable),
                UsageLevel.Green),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable),
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("35%"),
            ContentFact.Contains("20%"),
            new ContentFact(
                "names the weekly reset as not known rather than describing an indefinite wait",
                rendered => rendered.Contains("not known", StringComparison.OrdinalIgnoreCase)),
            new ContentFact(
                "never says 'waiting' for a reset ADR-0014/D9f already decided has no derivation",
                rendered => !rendered.Contains("waiting", StringComparison.OrdinalIgnoreCase)),
        });

    /// <summary>
    /// Gate G7 parity slice P8 (OVI-645): a known account identity carried on the header
    /// alongside an ordinary reading — exercises the render-proof hook's header path for the
    /// "known identity" state, both themes, both skins (ADR-0008 D11a).
    /// </summary>
    public static readonly DetailWindowFixture OrdinaryReadingWithKnownAccountIdentity = new(
        Name: "ordinary-reading-with-known-account-identity",
        Detail: new UsageDetail(
            Snapshot: new UsageSnapshot(
                DataSourceKind.Live,
                new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
                new UsagePercent(63, UsageValueStatus.Real),
                new UsageInstant(SessionReset, UsageValueStatus.Real),
                new UsagePercent(22, UsageValueStatus.Real),
                new UsageInstant(WeeklyReset, UsageValueStatus.Real),
                UsageLevel.Amber),
            Statistics: UsageStatistics.Unavailable,
            Models: ModelUsageBreakdown.Unavailable)
        {
            Account = new AccountIdentity("Jane Doe", "jane@example.com", "claude_max", UsageValueStatus.Real),
        },
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            ContentFact.Contains("Jane Doe"),
            ContentFact.Contains("claude_max"),
        });

    /// <summary>
    /// Gate G7 parity slice P8 (OVI-645): no account identity at all, alongside the fully
    /// unavailable detail — the header's own "no data" explanation must never guess a name.
    /// </summary>
    public static readonly DetailWindowFixture UnavailableAccountIdentity = new(
        Name: "unavailable-account-identity",
        Detail: UsageDetail.Unavailable,
        DisplayZone: TimeZoneInfo.Utc,
        ContentFacts: new[]
        {
            new ContentFact(
                "never renders a guessed email address for an unavailable identity",
                rendered => !rendered.Contains('@')),
        });

    public static IReadOnlyList<DetailWindowFixture> All { get; } = new[]
    {
        OrdinaryLiveReadingExtraUsageOn,
        StaleDetailOldLastIngestAt,
        StaleReadingExtraUsageOff,
        JsonlFallbackReadingExtraUsageUnknown,
        RecordedWindowNoActivity,
        ModelBreakdownUnavailable,
        UnpricedModelInWindow,
        TokenKindTotalsPopulated,
        UnavailableNoDataExplanation,
        SessionBandBoundaryGreen49,
        SessionBandBoundaryAmber50,
        SessionBandBoundaryAmber69,
        SessionBandBoundaryRed70,
        WeeklyResetNotKnownWithPlanData,
        OrdinaryReadingWithKnownAccountIdentity,
        UnavailableAccountIdentity,
    };
}
