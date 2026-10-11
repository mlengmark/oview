using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P24 (OVI-718), the weekly-reset entry dialog's own validation —
/// ported from the source's <c>WeeklyResetDialog.xaml.cs</c> <c>TrySave</c> reasoning: an empty
/// time field is a clear request, not an error, and a non-empty one must be a strict 24-hour
/// <c>HH:mm</c> time.
/// </summary>
public sealed class WeeklyResetEntryValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceTimeIsAClearRequest(string? time)
    {
        Assert.True(WeeklyResetEntryValidator.IsClearRequest(time));
    }

    [Fact]
    public void NonEmptyTimeIsNotAClearRequest()
    {
        Assert.False(WeeklyResetEntryValidator.IsClearRequest("22:59"));
    }

    [Fact]
    public void ValidDayAndTimeParse()
    {
        var ok = WeeklyResetEntryValidator.TryParse("Monday", "22:59", out var entry, out var error);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Monday, entry.Day);
        Assert.Equal("22:59", entry.TimeText);
        Assert.Equal("", error);
    }

    [Theory]
    [InlineData("10:59 PM")]
    [InlineData("25:00")]
    [InlineData("9:5")]
    [InlineData("not a time")]
    public void TwelveHourOrMalformedTimeFailsWithAnExplainingError(string time)
    {
        var ok = WeeklyResetEntryValidator.TryParse("Monday", time, out _, out var error);

        Assert.False(ok);
        Assert.Contains("24-hour", error);
        Assert.Contains(time, error);
    }

    [Fact]
    public void UnrecognisedDayFails()
    {
        var ok = WeeklyResetEntryValidator.TryParse("Someday", "22:59", out _, out var error);

        Assert.False(ok);
        Assert.Contains("Someday", error);
    }

    [Fact]
    public void LeadingAndTrailingWhitespaceIsTrimmedBeforeParsing()
    {
        var ok = WeeklyResetEntryValidator.TryParse("Friday", "  08:30  ", out var entry, out _);

        Assert.True(ok);
        Assert.Equal("08:30", entry.TimeText);
    }
}
