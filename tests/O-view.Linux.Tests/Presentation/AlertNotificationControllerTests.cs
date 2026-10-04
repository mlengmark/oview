using OView.App;
using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0008 slice 11's wiring (OVI-417), proved against a fake notifier seam only — no
/// D-Bus connection. The "once per raised event" contract is the point of this test: each call
/// to <see cref="AlertNotificationController.OnEventRaised"/> sends exactly one notification,
/// never zero, never more — the same contract <c>O-view.Tray.Tests</c>'s own
/// <c>AlertToastControllerTests</c> (slice 7) pins for its toast wiring.</summary>
public class AlertNotificationControllerTests
{
    [Fact]
    public void OnEventRaisedSendsExactlyOneNotificationWithTheFormatterOutput()
    {
        var sent = new List<(string Summary, string Body)>();
        var controller = new AlertNotificationController((summary, body) =>
        {
            sent.Add((summary, body));
            return Task.CompletedTask;
        });
        var usageEvent = new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Red };

        controller.OnEventRaised(usageEvent);

        var notification = Assert.Single(sent);
        var expected = AlertNotificationFormatter.Format(usageEvent);
        Assert.Equal((expected.Summary, expected.Body), notification);
    }

    [Fact]
    public void EachRaisedEventSendsItsOwnNotificationAndNothingIsCoalesced()
    {
        var sent = new List<(string Summary, string Body)>();
        var controller = new AlertNotificationController((summary, body) =>
        {
            sent.Add((summary, body));
            return Task.CompletedTask;
        });

        controller.OnEventRaised(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Amber });
        controller.OnEventRaised(new UsageEvent(UsageEventKind.ThresholdCrossed) { UsageLevel = UsageLevel.Amber });
        controller.OnEventRaised(new UsageEvent(UsageEventKind.UpdateAvailable));

        Assert.Equal(3, sent.Count);
    }

    [Fact]
    public void ASendFailureDoesNotThrowFromOnEventRaised()
    {
        var controller = new AlertNotificationController((_, _) => Task.FromException(new InvalidOperationException("no session bus")));

        var exception = Record.Exception(() => controller.OnEventRaised(new UsageEvent(UsageEventKind.UpdateAvailable)));

        Assert.Null(exception);
    }

    [Fact]
    public void ConstructorRejectsNullSender()
    {
        Assert.Throws<ArgumentNullException>(() => new AlertNotificationController(null!));
    }
}
