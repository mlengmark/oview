using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="BoostNoticeFixture"/>s (ADR-0003, OVI-92). Add a new
/// fixture as a new entry in <see cref="All"/>, following <c>PanelTextResetFixtures</c>'
/// existing pattern — one entry per member/scenario, since <c>BoostChip</c> and
/// <c>BoostCard</c> check different, differently-scoped content facts even though they share
/// one input shape.
/// </summary>
public static class BoostNoticeFixtures
{
    // Reuses the source app's own worked example (PanelText.cs's BoostChip doc comment):
    // ends-on 31 Aug, "now" 18 days 14 hours earlier, renders "2w 4d 14h" remaining.
    private static readonly DateOnly EndsOn = new(2026, 8, 31);
    private static readonly DateTimeOffset UtcNowEighteenDaysFourteenHoursBeforeEnd =
        new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

    public static readonly BoostNoticeFixture ChipWithPercentAndEndDateSpellsOutTheFullCountdown = new(
        Name: "chip-with-percent-and-end-date-spells-out-the-full-countdown",
        Render: skin => skin.BoostChip(
            new BoostNotice("Get 50% more usage until Aug 31st!", 50, EndsOn),
            UtcNowEighteenDaysFourteenHoursBeforeEnd,
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("50"),
            ContentFact.Contains("Boosted"),
            ContentFact.Contains("31"),
            ContentFact.Contains("Aug"),
            ContentFact.Contains("2w"),
            ContentFact.Contains("4d"),
            ContentFact.Contains("14h"),
        });

    public static readonly BoostNoticeFixture ChipWithNeitherFigureParsedFallsBackToBoostedAlone = new(
        Name: "chip-with-neither-figure-parsed-falls-back-to-boosted-alone",
        Render: skin => skin.BoostChip(
            new BoostNotice("There's a usage boost active on your account.", null, null),
            UtcNowEighteenDaysFourteenHoursBeforeEnd,
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("Boosted"),
            new ContentFact("does not render any digit (no percent parsed, no date parsed)", rendered => !rendered.Any(char.IsDigit)),
        });

    public static readonly BoostNoticeFixture ChipWithPercentButNoEndDateOmitsTheCountdown = new(
        Name: "chip-with-percent-but-no-end-date-omits-the-countdown",
        Render: skin => skin.BoostChip(
            new BoostNotice("Get 75% more usage, no end date given.", 75, null),
            UtcNowEighteenDaysFourteenHoursBeforeEnd,
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("75"),
            ContentFact.Contains("Boosted"),
        });

    public static readonly BoostNoticeFixture CardRelaysTheMessageVerbatimAndNamesTheEndDate = new(
        Name: "card-relays-the-message-verbatim-and-names-the-end-date",
        Render: skin => skin.BoostCard(
            new BoostNotice("Enjoy 50% more usage on your plan until August 31st.", 50, EndsOn),
            new DateTimeOffset(2026, 8, 20, 14, 0, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("Enjoy 50% more usage on your plan until August 31st."),
            ContentFact.Contains("14:00"),
            ContentFact.Contains("31"),
            ContentFact.Contains("Aug"),
        });

    public static readonly BoostNoticeFixture CardWithNoEndDateStillRelaysTheMessageAndReadTime = new(
        Name: "card-with-no-end-date-still-relays-the-message-and-read-time",
        Render: skin => skin.BoostCard(
            new BoostNotice("You're getting a usage boost right now.", null, null),
            new DateTimeOffset(2026, 8, 20, 9, 5, 0, TimeSpan.Zero),
            TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("You're getting a usage boost right now."),
            ContentFact.Contains("09:05"),
        });

    public static IReadOnlyList<BoostNoticeFixture> All { get; } = new[]
    {
        ChipWithPercentAndEndDateSpellsOutTheFullCountdown,
        ChipWithNeitherFigureParsedFallsBackToBoostedAlone,
        ChipWithPercentButNoEndDateOmitsTheCountdown,
        CardRelaysTheMessageVerbatimAndNamesTheEndDate,
        CardWithNoEndDateStillRelaysTheMessageAndReadTime,
    };
}
