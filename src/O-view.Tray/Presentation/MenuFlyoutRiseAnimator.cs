namespace OView.Tray.Presentation;

/// <summary>
/// The branded tray menu flyout's rise-animation open/close race rules (gate G7 parity slice
/// P22, OVI-743), framework-neutral so they stay provable with no window involved —
/// <see cref="MenuFlyoutRiseAnimation"/> is the WPF adapter that drives real animations through
/// the three actions this type calls.
///
/// <para><b>Hiding never happens synchronously.</b> <see cref="RequestHide"/> only starts the
/// close animation; the real hide only runs once <see cref="OnCloseFinished"/> reports that
/// animation actually finished — the task brief's own "hiding the menu waits for the close
/// animation to finish" requirement.</para>
///
/// <para><b>Reopening mid-close cancels the close instead of racing it.</b> Calling
/// <see cref="RequestShow"/> while a close animation is still in flight moves state away from
/// <c>Closing</c> immediately and restarts the open animation. A late
/// <see cref="OnCloseFinished"/> call arriving afterwards — the close animation's own stale
/// completion, since nothing told the WPF adapter to literally stop it mid-flight — finds state
/// no longer <c>Closing</c> and is a no-op, so it can never hide a flyout that has since
/// reopened.</para>
/// </summary>
internal sealed class MenuFlyoutRiseAnimator
{
    private enum State
    {
        Hidden,
        Opening,
        Open,
        Closing,
    }

    private readonly Action _beginOpen;
    private readonly Action _beginClose;
    private readonly Action _hide;

    private State _state = State.Hidden;

    public MenuFlyoutRiseAnimator(Action beginOpen, Action beginClose, Action hide)
    {
        ArgumentNullException.ThrowIfNull(beginOpen);
        ArgumentNullException.ThrowIfNull(beginClose);
        ArgumentNullException.ThrowIfNull(hide);

        _beginOpen = beginOpen;
        _beginClose = beginClose;
        _hide = hide;
    }

    /// <summary>Starts the open animation unless one is already running or finished; restarts it
    /// from scratch if a close animation is currently in flight — see the type remarks.</summary>
    public void RequestShow()
    {
        if (_state is State.Opening or State.Open)
        {
            return;
        }

        _state = State.Opening;
        _beginOpen();
    }

    /// <summary>The open animation actually finished.</summary>
    public void OnOpenFinished()
    {
        if (_state == State.Opening)
        {
            _state = State.Open;
        }
    }

    /// <summary>Starts the close animation unless the flyout is already hidden or closing.
    /// Never hides synchronously — see the type remarks.</summary>
    public void RequestHide()
    {
        if (_state is State.Hidden or State.Closing)
        {
            return;
        }

        _state = State.Closing;
        _beginClose();
    }

    /// <summary>The close animation actually finished. Ignored if a reopen already moved state
    /// away from closing — see the type remarks.</summary>
    public void OnCloseFinished()
    {
        if (_state != State.Closing)
        {
            return;
        }

        _state = State.Hidden;
        _hide();
    }

    /// <summary>Sets the finished state directly, with nothing animating — a window that is
    /// rebuilding its content (a live theme change) while already shown must land back on
    /// <c>Open</c>, not the default <c>Hidden</c> a fresh instance would otherwise start from, or
    /// a later <see cref="RequestHide"/> would wrongly no-op.</summary>
    public void Reset(bool open) => _state = open ? State.Open : State.Hidden;
}
