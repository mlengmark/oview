using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace OView.Linux.Presentation;

/// <summary>
/// The "Why so large?" disclosure fold's Avalonia wiring (ADR-0008 D10, gate G7 parity slice
/// P19, OVI-717) — the Linux counterpart of <c>O-view.Tray</c>'s own <see cref="DisclosureFold"/>,
/// independently implemented, not shared (D1): the one surface where this window's otherwise-
/// fixed frame (<see cref="OView.Linux.DetailWindow.DefaultHeight"/>, slice P3) is allowed to
/// grow. Timing and curve are <see cref="DisclosureFoldTiming"/>'s; which edge the window grows
/// from is <see cref="DisclosureFoldGeometry"/>'s — this class is the only thing that touches a
/// real <see cref="Window"/>, a real body, and a real chevron, so those two stay provable with
/// neither.
///
/// <para>No fade: only <paramref name="body"/>'s <c>Height</c> and the chevron's
/// <see cref="RotateTransform.Angle"/> animate, on the same curve and duration — Avalonia's
/// <see cref="SplineEasing"/> takes the same four control points WPF's <c>KeySpline</c> does, so
/// both skins run the identical curve. The caller must clip <paramref name="body"/> to its own
/// bounds (<c>ClipToBounds</c>) so its content slides out from behind the fold edge rather than
/// appearing or disappearing in place.</para>
///
/// <para>Repositioning/resizing this window is only ever a <i>request</i> on this skin — an X11
/// window manager may ignore it, Wayland refuses it outright (<c>DetailWindow.SetVisible</c>'s
/// own remarks). This class still issues the one correct request; whether the compositor grants
/// it is outside anything here can observe, and is not this slice's obligation to prove.</para>
///
/// <para>Not unit-tested, same boundary as <see cref="OView.Linux.DetailWindow"/> itself (no
/// interactive Linux display in this environment) — every decision this class merely carries
/// out is <see cref="DisclosureFoldGeometry"/>'s and <see cref="DisclosureFoldTiming"/>'s, both
/// proven against plain numbers with no window involved.</para>
/// </summary>
internal sealed class DisclosureFold
{
    private readonly Window _host;
    private readonly Border _body;
    private readonly RotateTransform _chevron;
    private readonly Func<double> _workAreaBottom;

    private EventHandler<AvaloniaPropertyChangedEventArgs>? _tracking;
    private DispatcherTimer? _guard;
    private DisclosureFoldDirection _direction;
    private double _collapsedTop;
    private double _collapsedWindowHeight;

    public bool IsExpanded { get; private set; }

    public DisclosureFold(Window host, Border body, RotateTransform chevron, Func<double> workAreaBottom)
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
        _body.IsVisible = expanded;
        _body.Height = expanded ? double.NaN : 0;
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
    /// — a close replays the same direction an open decided.
    /// </summary>
    /// <param name="naturalHeight">The body's fully open height, measured by the caller.</param>
    public void Toggle(double naturalHeight) => Run(!IsExpanded, naturalHeight);

    private void Run(bool expand, double naturalHeight)
    {
        Stop();
        IsExpanded = expand;

        if (expand)
        {
            _collapsedTop = _host.Position.Y;
            _collapsedWindowHeight = _host.Height;
            _direction = DisclosureFoldGeometry.DecideDirection(
                _collapsedTop, _collapsedWindowHeight, _collapsedWindowHeight + naturalHeight, _workAreaBottom());
        }

        var durationMs = expand ? DisclosureFoldTiming.ExpandDurationMs : DisclosureFoldTiming.CollapseDurationMs;
        var curve = expand ? DisclosureFoldTiming.Decelerate : DisclosureFoldTiming.Accelerate;
        var easing = new SplineEasing(curve.X1, curve.Y1, curve.X2, curve.Y2);
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var toHeight = expand ? naturalHeight : 0;

        _body.IsVisible = true;

        var collapsedTop = _collapsedTop;
        var collapsedWindowHeight = _collapsedWindowHeight;
        var direction = _direction;

        void Reposition()
        {
            var windowHeight = collapsedWindowHeight + _body.Bounds.Height;
            var frame = DisclosureFoldGeometry.FrameForHeight(direction, collapsedTop, collapsedWindowHeight, windowHeight);
            _host.Height = frame.Height;
            _host.Position = new PixelPoint(_host.Position.X, (int)Math.Round(frame.Top));
        }

        // Fires every frame the animated Height changes Bounds — the same per-frame reposition
        // the task asks for, driven by Avalonia's own layout invalidation rather than a manual
        // tick.
        _tracking = (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
            {
                Reposition();
            }
        };
        _body.PropertyChanged += _tracking;

        _body.Transitions = new Transitions
        {
            new DoubleTransition { Property = Border.HeightProperty, Duration = duration, Easing = easing },
        };
        _body.Height = toHeight;

        _chevron.Transitions = new Transitions
        {
            new DoubleTransition { Property = RotateTransform.AngleProperty, Duration = duration, Easing = easing },
        };
        _chevron.Angle = expand ? DisclosureFoldTiming.ExpandedChevronAngleDegrees : 0;

        // Backstop: Avalonia's property-transition system gives no reliable cross-version
        // "Completed" callback this class can depend on, so the fold's finished state is set
        // from a timer instead — same reasoning as the source's own fold and entrance animations
        // carrying one. Firing twice is harmless; Finish is idempotent.
        _guard = new DispatcherTimer { Interval = duration + TimeSpan.FromMilliseconds(150) };
        _guard.Tick += (_, _) => Finish(expand);
        _guard.Start();
    }

    private void Finish(bool expand)
    {
        if (_tracking is null)
        {
            return;
        }

        _body.PropertyChanged -= _tracking;
        _tracking = null;
        _guard?.Stop();
        _guard = null;

        _body.Transitions = null;
        _chevron.Transitions = null;

        _body.Height = expand ? double.NaN : 0;
        _body.IsVisible = expand;
        _chevron.Angle = expand ? DisclosureFoldTiming.ExpandedChevronAngleDegrees : 0;

        var finalHeight = _collapsedWindowHeight + (expand ? _body.Bounds.Height : 0);
        var frame = DisclosureFoldGeometry.FrameForHeight(_direction, _collapsedTop, _collapsedWindowHeight, finalHeight);
        _host.Height = frame.Height;
        _host.Position = new PixelPoint(_host.Position.X, (int)Math.Round(frame.Top));
    }

    private void Stop()
    {
        if (_tracking is not null)
        {
            _body.PropertyChanged -= _tracking;
            _tracking = null;
        }

        _guard?.Stop();
        _guard = null;

        _body.Transitions = null;
        _chevron.Transitions = null;
    }
}
