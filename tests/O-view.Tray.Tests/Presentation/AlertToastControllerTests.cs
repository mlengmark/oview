using OView.App;
using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0008 slice 7's wiring (OVI-391), proved against a fake sink only — no
/// NotifyIcon. The "once per raised event" contract is the point of this test: each call to
/// <see cref="AlertToastController.OnEventRaised"/> produces exactly one toast call, never
/// zero, never more.</summary>
public class AlertToastControllerTests
{
    [Fact]
    public void OnEventRaisedShowsExactlyOneToastWithTheFormatterOutput()
    {
        var shown = new List<(string Title, string Body)>();
        var controller = new AlertToastController((title, body) => shown.Add((title, body)));
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        controller.OnEventRaised(usageEvent);

        var toast = Assert.Single(shown);
        var expected = AlertToastFormatter.Format(usageEvent);
        Assert.Equal((expected.Title, expected.Body), toast);
    }

    [Fact]
    public void EachRaisedEventShowsItsOwnToastAndNothingIsCoalesced()
    {
        var shown = new List<(string Title, string Body)>();
        var controller = new AlertToastController((title, body) => shown.Add((title, body)));

        controller.OnEventRaised(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Amber });
        controller.OnEventRaised(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Amber });
        controller.OnEventRaised(new UsageEvent(UsageEventKind.UpdateAvailable));

        Assert.Equal(3, shown.Count);
    }

    [Fact]
    public void ConstructorRejectsNullSink()
    {
        Assert.Throws<ArgumentNullException>(() => new AlertToastController(null!));
    }
}
