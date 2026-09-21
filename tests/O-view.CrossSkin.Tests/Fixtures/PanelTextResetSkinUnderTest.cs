namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <c>Countdown</c>/<c>SessionReset</c>/<c>WeeklyReset</c>/
/// <c>WeeklyResetConflict</c> entry points, wired into the harness together because — unlike
/// <see cref="SkinUnderTest"/> and <see cref="FreshnessSkinUnderTest"/> — none of these four
/// take a <see cref="OView.Core.Models.UsageSnapshot"/> at all, and no two of them share the
/// same signature either (Kit's OVI-29 signature survey; Quinn's OVI-45 sign-off). Rather
/// than force four incompatible shapes into one shared delegate, each
/// <see cref="PanelTextResetFixture"/> closes over whichever one member it exercises.
/// </summary>
public sealed record PanelTextResetSkinUnderTest(
    string Name,
    Func<TimeSpan, string> Countdown,
    Func<DateTimeOffset?, DateTimeOffset, TimeZoneInfo, TimeSpan?, string> SessionReset,
    Func<DateTimeOffset, DateTimeOffset, TimeZoneInfo, string> WeeklyReset,
    Func<DateTimeOffset, TimeZoneInfo, string> WeeklyResetConflict);
