using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OView.Tray.Presentation;

/// <summary>
/// The branded tray menu flyout's rise animation's WPF wiring (gate G7 parity slice P22,
/// OVI-743): a clip reveal plus the last <see cref="MenuFlyoutRiseTiming.SlideDistanceDip"/> of
/// slide, timed and curved by <see cref="MenuFlyoutRiseTiming"/>. Which edge the reveal grows
/// from is <see cref="MenuFlyoutRiseGeometry"/>'s; the open/close race rules (hide waits for the
/// close animation, a mid-close reopen cancels it) are <see cref="MenuFlyoutRiseAnimator"/>'s —
/// this class is the only thing that touches a real <see cref="UIElement.Clip"/> and
/// <see cref="UIElement.RenderTransform"/>, the same split <see cref="DisclosureFold"/> already
/// established for the detail window's own fold.
///
/// <para>A close replays whichever direction the preceding open used, same convention
/// <see cref="DisclosureFold"/> already follows for its own open/close pair.</para>
///
/// <para>Not unit-tested, same boundary as every other real WPF adapter in this skin (no
/// interactive Windows desktop in this environment) — every decision this class merely carries
/// out is <see cref="MenuFlyoutRiseTiming"/>'s, <see cref="MenuFlyoutRiseGeometry"/>'s, or
/// <see cref="MenuFlyoutRiseAnimator"/>'s, all three proven against plain values with no window
/// involved.</para>
/// </summary>
internal sealed class MenuFlyoutRiseAnimation
{
    private readonly FrameworkElement _root;
    private readonly TranslateTransform _slide;
    private readonly MenuFlyoutRiseAnimator _animator;

    private DispatcherTimer? _guard;
    private MenuFlyoutRiseDirection _direction;

    public MenuFlyoutRiseAnimation(FrameworkElement root, Action hide)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(hide);

        _root = root;
        _slide = new TranslateTransform();
        _root.RenderTransform = _slide;
        _animator = new MenuFlyoutRiseAnimator(() => Run(expand: true), () => Run(expand: false), hide);
    }

    /// <summary>Starts (or, if mid-close, restarts) the rise-open animation. The caller must
    /// already have positioned and shown the window — this only animates the content inside
    /// it.</summary>
    public void Show(MenuFlyoutRiseDirection direction)
    {
        _direction = direction;
        _animator.RequestShow();
    }

    /// <summary>Starts the rise-close animation. The real hide this type was handed at
    /// construction only runs once that animation finishes (or is cancelled by a mid-close
    /// <see cref="Show"/>) — never synchronously here.</summary>
    public void RequestHide() => _animator.RequestHide();

    /// <summary>Puts the content in its finished state immediately, with nothing animating — a
    /// window rebuilding its content (a live theme change) calls this with the window's current
    /// actual visibility so the new instance lands on the right state rather than defaulting to
    /// hidden.</summary>
    public void Reset(bool open)
    {
        Stop();
        _root.Clip = open ? null : new RectangleGeometry(new Rect(0, 0, 0, 0));
        _slide.Y = 0;
        _animator.Reset(open);
    }

    private void Run(bool expand)
    {
        Stop();

        var durationMs = expand ? MenuFlyoutRiseTiming.OpenDurationMs : MenuFlyoutRiseTiming.CloseDurationMs;
        var curve = MenuFlyoutRiseTiming.Curve;
        var spline = new KeySpline(curve.X1, curve.Y1, curve.X2, curve.Y2);
        var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

        var width = _root.ActualWidth;
        var height = _root.ActualHeight;
        var downward = _direction == MenuFlyoutRiseDirection.Downward;

        Rect RectFor(double revealed) => downward
            ? new Rect(0, 0, width, revealed)
            : new Rect(0, Math.Max(0, height - revealed), width, revealed);

        var fromReveal = expand ? 0.0 : height;
        var toReveal = expand ? height : 0.0;

        var clip = new RectangleGeometry(RectFor(fromReveal));
        _root.Clip = clip;

        var clipAnimation = new RectAnimationUsingKeyFrames { Duration = duration };
        clipAnimation.KeyFrames.Add(new DiscreteRectKeyFrame(RectFor(fromReveal), KeyTime.FromPercent(0)));
        clipAnimation.KeyFrames.Add(new SplineRectKeyFrame(RectFor(toReveal), KeyTime.FromPercent(1), spline));

        var sign = downward ? -1 : 1;
        var fromOffset = sign * (expand ? MenuFlyoutRiseTiming.SlideDistanceDip : 0.0);
        var toOffset = sign * (expand ? 0.0 : MenuFlyoutRiseTiming.SlideDistanceDip);

        var slideAnimation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        slideAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame(fromOffset, KeyTime.FromPercent(0)));
        slideAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(toOffset, KeyTime.FromPercent(1), spline));

        // Completed MUST be attached before BeginAnimation — WPF takes its own copy of the
        // timeline when the animation starts, same pitfall DisclosureFold's own remarks warn
        // about, for the same reason.
        clipAnimation.Completed += (_, _) => Finish(expand);
        clip.BeginAnimation(RectangleGeometry.RectProperty, clipAnimation);
        _slide.BeginAnimation(TranslateTransform.YProperty, slideAnimation);

        // Backstop, same reason DisclosureFold carries one: the symptom of a missed Completed is
        // a flyout stuck mid-reveal, or — worse, since a hide now waits for this — never actually
        // hiding. Firing twice is harmless; Finish is idempotent.
        _guard = new DispatcherTimer { Interval = duration.TimeSpan + TimeSpan.FromMilliseconds(150) };
        _guard.Tick += (_, _) => Finish(expand);
        _guard.Start();
    }

    private void Finish(bool expand)
    {
        if (_guard is null)
        {
            return;
        }

        _guard.Stop();
        _guard = null;

        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        _slide.Y = 0;
        _root.Clip = null;

        if (expand)
        {
            _animator.OnOpenFinished();
        }
        else
        {
            _animator.OnCloseFinished();
        }
    }

    private void Stop()
    {
        _guard?.Stop();
        _guard = null;

        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        (_root.Clip as RectangleGeometry)?.BeginAnimation(RectangleGeometry.RectProperty, null);
    }
}
