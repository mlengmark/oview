using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OView.Tray.Presentation;

/// <summary>
/// The "Why so large?" disclosure fold's WPF wiring (ADR-0008 D10, gate G7 parity slice P19,
/// OVI-717) — the one surface where this window's otherwise-fixed frame
/// (<see cref="OView.Tray.DetailWindow.DefaultHeight"/>, slice P3) is allowed to grow, because
/// D10 explicitly assigns "timings, curve, chevron, grow-upward geometry" to this skin for this
/// fold alone. Timing and curve are <see cref="DisclosureFoldTiming"/>'s; which edge the window
/// grows from is <see cref="DisclosureFoldGeometry"/>'s — this class is the only thing that
/// touches a real <see cref="Window"/>, a real body, and a real chevron, so those two stay
/// provable with neither.
///
/// <para>No fade: only <paramref name="body"/>'s <see cref="FrameworkElement.Height"/> and the
/// chevron's <see cref="RotateTransform.Angle"/> animate, on the same curve and duration. The
/// caller must clip <paramref name="body"/> to its own bounds (a fixed-size container with
/// <c>ClipToBounds</c>) so its content slides out from behind the fold edge rather than
/// appearing or disappearing in place.</para>
///
/// <para>Not unit-tested, same boundary as <see cref="OView.Tray.DetailWindow"/> itself (no
/// interactive Windows desktop in this environment) — every decision this class merely carries
/// out is <see cref="DisclosureFoldGeometry"/>'s and <see cref="DisclosureFoldTiming"/>'s, both
/// proven against plain numbers with no window involved.</para>
/// </summary>
internal sealed class DisclosureFold
{
    private readonly Window _host;
    private readonly FrameworkElement _body;
    private readonly RotateTransform _chevron;
    private readonly Func<double> _workAreaBottom;

    private SizeChangedEventHandler? _tracking;
    private DispatcherTimer? _guard;
    private DisclosureFoldDirection _direction;
    private double _collapsedTop;
    private double _collapsedWindowHeight;

    public bool IsExpanded { get; private set; }

    public DisclosureFold(Window host, FrameworkElement body, RotateTransform chevron, Func<double> workAreaBottom)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(chevron);
        ArgumentNullException.ThrowIfNull(workAreaBottom);

        _host = host;
        _body = body;
        _chevron = chevron;
        _workAreaBottom = workAreaBottom;
    }

    /// <summary>Puts the fold in its finished state immediately, with nothing animating — the
    /// initial build, and the state a reused window must reset to on every open (<c>ShowDetail</c>
    /// resets this fold the same way it resets every other toggle).</summary>
    public void Apply(bool expanded)
    {
        Stop();

        IsExpanded = expanded;
        _body.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _body.Height = double.NaN;
        _chevron.Angle = expanded ? DisclosureFoldTiming.ExpandedChevronAngleDegrees : 0;

        if (_host.Height < DetailWindow.DefaultHeight)
        {
            _host.Height = DetailWindow.DefaultHeight;
        }
    }

    /// <summary>
    /// Folds the body open or closed, starting from wherever it currently is. The direction is
    /// decided once, at the start of an open, from the collapsed window's current position and
    /// the height it will reach fully open (<see cref="DisclosureFoldGeometry.DecideDirection"/>)
    /// — a close replays the same direction an open decided, so the window retraces exactly the
    /// path it grew along.
    /// </summary>
    /// <param name="naturalHeight">The body's fully open height, measured by the caller.</param>
    public void Toggle(double naturalHeight)
    {
        var expand = !IsExpanded;
        Run(expand, naturalHeight);
    }

    private void Run(bool expand, double naturalHeight)
    {
        Stop();
        IsExpanded = expand;

        if (expand)
        {
            _collapsedTop = _host.Top;
            _collapsedWindowHeight = _host.Height;
            _direction = DisclosureFoldGeometry.DecideDirection(
                _collapsedTop, _collapsedWindowHeight, _collapsedWindowHeight + naturalHeight, _workAreaBottom());
        }

        var durationMs = expand ? DisclosureFoldTiming.ExpandDurationMs : DisclosureFoldTiming.CollapseDurationMs;
        var curve = expand ? DisclosureFoldTiming.Decelerate : DisclosureFoldTiming.Accelerate;
        var spline = new KeySpline(curve.X1, curve.Y1, curve.X2, curve.Y2);
        var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

        var fromHeight = double.IsNaN(_body.Height) ? _body.ActualHeight : _body.Height;
        var toHeight = expand ? naturalHeight : 0;

        _body.Visibility = Visibility.Visible;

        var collapsedTop = _collapsedTop;
        var collapsedWindowHeight = _collapsedWindowHeight;
        var direction = _direction;

        _tracking = (_, _) =>
        {
            var windowHeight = collapsedWindowHeight + _body.ActualHeight;
            var frame = DisclosureFoldGeometry.FrameForHeight(direction, collapsedTop, collapsedWindowHeight, windowHeight);
            _host.Height = frame.Height;
            _host.Top = frame.Top;
        };
        _body.SizeChanged += _tracking;

        var heightAnimation = Spline(fromHeight, toHeight, duration, spline);

        // Completed MUST be attached before BeginAnimation — WPF takes its own copy of the
        // timeline when the animation starts, so a handler added afterwards is attached to an
        // object nothing will ever raise (the exact mistake the source's own DisclosureAnimation
        // remarks warn about, for the same reason).
        heightAnimation.Completed += (_, _) => Finish(expand);
        _body.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);

        _chevron.BeginAnimation(RotateTransform.AngleProperty,
            Spline(_chevron.Angle, expand ? DisclosureFoldTiming.ExpandedChevronAngleDegrees : 0, duration, spline));

        // Backstop, same reason the source's own fold and entrance animations carry one: the
        // symptom of a missed Completed is a fold stuck mid-height with a window that no longer
        // tracks its own content. Firing twice is harmless — Finish is idempotent.
        _guard = new DispatcherTimer { Interval = duration.TimeSpan + TimeSpan.FromMilliseconds(150) };
        _guard.Tick += (_, _) => Finish(expand);
        _guard.Start();
    }

    private void Finish(bool expand)
    {
        if (_tracking is null)
        {
            return;
        }

        _body.SizeChanged -= _tracking;
        _tracking = null;
        _guard?.Stop();
        _guard = null;

        _body.BeginAnimation(FrameworkElement.HeightProperty, null);
        _chevron.BeginAnimation(RotateTransform.AngleProperty, null);

        _body.Height = double.NaN;
        _body.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        _chevron.Angle = expand ? DisclosureFoldTiming.ExpandedChevronAngleDegrees : 0;

        var finalHeight = _collapsedWindowHeight + (expand ? _body.ActualHeight : 0);
        var frame = DisclosureFoldGeometry.FrameForHeight(_direction, _collapsedTop, _collapsedWindowHeight, finalHeight);
        _host.Height = frame.Height;
        _host.Top = frame.Top;
    }

    private void Stop()
    {
        if (_tracking is not null)
        {
            _body.SizeChanged -= _tracking;
            _tracking = null;
        }

        _guard?.Stop();
        _guard = null;

        _body.BeginAnimation(FrameworkElement.HeightProperty, null);
        _chevron.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private static DoubleAnimationUsingKeyFrames Spline(double from, double to, Duration duration, KeySpline spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromPercent(1), spline));
        return animation;
    }
}
