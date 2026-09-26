using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// The versioned set of <see cref="PanelTextResetFixture"/>s (ADR-0003, OVI-29). Add a new
/// fixture as a new entry in <see cref="All"/>, following <c>GoldenMasterFixtures</c>'
/// existing pattern — one entry per member/scenario, since these four members do not share
/// an input shape.
/// </summary>
public static class PanelTextResetFixtures
{
    private static readonly DateTimeOffset UtcNow = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionResetAt = new(2026, 9, 11, 12, 14, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeeklyResetAt = new(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

    public static readonly PanelTextResetFixture CountdownOverAnHour = new(
        Name: "countdown-over-an-hour",
        Render: skin => skin.Countdown(TimeSpan.FromMinutes(134)),
        ContentFacts: new[]
        {
            ContentFact.Contains("2h"),
            ContentFact.Contains("14m"),
        });

    public static readonly PanelTextResetFixture CountdownUnderAMinute = new(
        Name: "countdown-under-a-minute",
        Render: skin => skin.Countdown(TimeSpan.FromSeconds(20)),
        ContentFacts: new[]
        {
            ContentFact.Contains("minute"),
        });

    public static readonly PanelTextResetFixture SessionResetKnownAndExact = new(
        Name: "session-reset-known-and-exact",
        Render: skin => skin.SessionReset(new UsageInstant(SessionResetAt, UsageValueStatus.Real), UtcNow, TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("12:14"),
            ContentFact.Contains("2h"),
        });

    /// <summary>
    /// An <see cref="UsageValueStatus.Estimated"/> reset still states its time and countdown —
    /// the approximate marker qualifies the time, it never replaces it. The marker itself is
    /// not pinned here: each skin words it differently (<c>~</c> / <c>(approx.)</c>), and
    /// whether it appears is checked per skin, against that skin's own tooltip, in
    /// <c>O-view.{Tray,Linux}.Tests</c>' <c>SessionResetAndTheTooltipAgreeOnWhetherTheResetIsMarked</c>
    /// (ADR-0003, 2026-09-25 note).
    /// </summary>
    public static readonly PanelTextResetFixture SessionResetEstimatedStillStatesTheTime = new(
        Name: "session-reset-estimated-still-states-the-time",
        Render: skin => skin.SessionReset(new UsageInstant(SessionResetAt, UsageValueStatus.Estimated), UtcNow, TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("12:14"),
            ContentFact.Contains("2h"),
        });

    public static readonly PanelTextResetFixture SessionResetUnknown = new(
        Name: "session-reset-unknown",
        Render: skin => skin.SessionReset(new UsageInstant(null, UsageValueStatus.Unavailable), UtcNow, TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            new ContentFact("does not render a clock time", rendered => !rendered.Contains(':')),
        });

    public static readonly PanelTextResetFixture WeeklyResetIncludesWeekday = new(
        Name: "weekly-reset-includes-weekday",
        Render: skin => skin.WeeklyReset(WeeklyResetAt, UtcNow, TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("Mon"),
            ContentFact.Contains("23:00"),
        });

    public static readonly PanelTextResetFixture WeeklyResetConflictStatesTheReportedTime = new(
        Name: "weekly-reset-conflict-states-the-reported-time",
        Render: skin => skin.WeeklyResetConflict(WeeklyResetAt, TimeZoneInfo.Utc),
        ContentFacts: new[]
        {
            ContentFact.Contains("Mon 23:00"),
        });

    public static IReadOnlyList<PanelTextResetFixture> All { get; } = new[]
    {
        CountdownOverAnHour,
        CountdownUnderAMinute,
        SessionResetKnownAndExact,
        SessionResetEstimatedStillStatesTheTime,
        SessionResetUnknown,
        WeeklyResetIncludesWeekday,
        WeeklyResetConflictStatesTheReportedTime,
    };
}
