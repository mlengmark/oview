using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Gate G7 parity slice P22 (OVI-743): proves the branded menu flyout's rise animation waits for
/// its own close animation to finish before actually hiding (no visible jump), and that
/// reopening while that close is still running cancels the close instead of racing it.
/// </summary>
public sealed class MenuFlyoutRiseAnimatorTests
{
    [Fact]
    public void RequestShow_begins_the_open_animation()
    {
        var opens = 0;
        var animator = new MenuFlyoutRiseAnimator(() => opens++, () => { }, () => { });

        animator.RequestShow();

        Assert.Equal(1, opens);
    }

    [Fact]
    public void RequestShow_while_already_opening_does_not_restart_the_animation()
    {
        var opens = 0;
        var animator = new MenuFlyoutRiseAnimator(() => opens++, () => { }, () => { });
        animator.RequestShow();

        animator.RequestShow();

        Assert.Equal(1, opens);
    }

    [Fact]
    public void RequestShow_while_already_open_does_not_restart_the_animation()
    {
        var opens = 0;
        var animator = new MenuFlyoutRiseAnimator(() => opens++, () => { }, () => { });
        animator.RequestShow();
        animator.OnOpenFinished();

        animator.RequestShow();

        Assert.Equal(1, opens);
    }

    [Fact]
    public void RequestHide_begins_the_close_animation_but_does_not_hide_synchronously()
    {
        var hides = 0;
        var animator = new MenuFlyoutRiseAnimator(() => { }, () => { }, () => hides++);
        animator.RequestShow();
        animator.OnOpenFinished();

        animator.RequestHide();

        Assert.Equal(0, hides);
    }

    [Fact]
    public void Hide_only_runs_once_the_close_animation_reports_it_finished()
    {
        var hides = 0;
        var animator = new MenuFlyoutRiseAnimator(() => { }, () => { }, () => hides++);
        animator.RequestShow();
        animator.OnOpenFinished();
        animator.RequestHide();

        animator.OnCloseFinished();

        Assert.Equal(1, hides);
    }

    [Fact]
    public void Reopening_mid_close_cancels_the_close_instead_of_racing_it()
    {
        var opens = 0;
        var hides = 0;
        var animator = new MenuFlyoutRiseAnimator(() => opens++, () => { }, () => hides++);
        animator.RequestShow();
        animator.OnOpenFinished();
        animator.RequestHide();

        animator.RequestShow();
        animator.OnCloseFinished(); // the cancelled close's own completion, arriving late

        Assert.Equal(2, opens);
        Assert.Equal(0, hides);
    }

    [Fact]
    public void RequestHide_while_already_hidden_does_not_begin_a_close()
    {
        var closes = 0;
        var animator = new MenuFlyoutRiseAnimator(() => { }, () => closes++, () => { });

        animator.RequestHide();

        Assert.Equal(0, closes);
    }

    [Fact]
    public void RequestHide_while_already_closing_does_not_restart_the_close_animation()
    {
        var closes = 0;
        var animator = new MenuFlyoutRiseAnimator(() => { }, () => closes++, () => { });
        animator.RequestShow();
        animator.OnOpenFinished();
        animator.RequestHide();

        animator.RequestHide();

        Assert.Equal(1, closes);
    }

    [Fact]
    public void Reset_true_lets_a_later_hide_request_begin_a_close_from_a_fresh_instance()
    {
        var closes = 0;
        var animator = new MenuFlyoutRiseAnimator(() => { }, () => closes++, () => { });
        animator.Reset(open: true);

        animator.RequestHide();

        Assert.Equal(1, closes);
    }

    [Fact]
    public void Reset_false_lets_a_later_show_request_begin_an_open_from_a_fresh_instance()
    {
        var opens = 0;
        var animator = new MenuFlyoutRiseAnimator(() => opens++, () => { }, () => { });
        animator.Reset(open: false);

        animator.RequestShow();

        Assert.Equal(1, opens);
    }
}
