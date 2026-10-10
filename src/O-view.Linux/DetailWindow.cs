using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OView.App;
using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux detail window (ADR-0008 slice 10, OVI-408), the Linux counterpart of
/// <c>O-view.Tray</c>'s <c>DetailWindow</c> (slice 6, OVI-386) — independently implemented, not
/// shared (D1): a draggable, borderless Avalonia widget that renders only the last
/// <see cref="UsageDetail"/> the shell pushed through <see cref="IShellToSkin.ShowDetail"/> — it
/// totals or formats nothing itself (D9c); every figure on screen is
/// <see cref="DetailWindowContentBuilder.Build"/>'s output, already run through this skin's own
/// formatters.
///
/// <para>Not unit-tested (no interactive Linux display in this environment — the same
/// "not verified" boundary slices 8/9 already recorded for their own OS adapters). Every
/// decision this window merely carries out — which position to open at, when that position
/// changed, what to say — is pulled out into <see cref="DetailWindowContentBuilder"/> and
/// <see cref="Presentation.DetailWindowPositionController"/>, both proven against fakes with no
/// window involved.</para>
///
/// <para>Dragging is tracked manually (pointer-capture deltas), not via
/// <c>Window.BeginMoveDrag</c>: the latter hands the whole gesture to the window manager and
/// never reports where the drag ended, so <see cref="Presentation.DetailWindowPositionController.OnDragEnd"/>
/// would have nothing to persist. On Linux, window position is only ever a <i>request</i>
/// (<see cref="DetailWindowPlacement"/>'s own doc comment: an X11 window manager may ignore it,
/// Wayland refuses it outright) — <see cref="SetVisible"/> logs both the position requested and
/// the position actually granted (D4), the one requirement the Windows copy did not need.</para>
/// </summary>
internal sealed class DetailWindow : Window
{
    /// <summary>This window's fixed size, named so the composition root can feed the same
    /// figures into <see cref="DetailWindowPlacement.Compute"/> without constructing the
    /// window first (the position controller is built before the window is, since the
    /// window's constructor takes the controller). ADR-0008 slicing-table slice P3 (OVI-622,
    /// G7 parity): ~400 px per the source's own <c>ui-spec.md</c> §2 ("Roughly 400 px wide") —
    /// the 281 px figure Adrian's correction (OVI-585) traced to a text-row truncation budget
    /// <i>inside</i> the window, not the window's own width. The docked-corner placement model
    /// §2 describes does not apply here (plain centred window, ADR-0013); only the width and the
    /// shared content below it carry over.</summary>
    public const double DefaultWidth = 400;
    public const double DefaultHeight = 360;

    private readonly ISkinToShell _skinToShell;
    private readonly DetailWindowPositionController _position;
    private readonly Action<string> _log;
    private readonly TextBlock _freshness = NewLine();
    private readonly TextBlock _session = NewLine();
    private readonly TextBlock _weekly = NewLine();
    private readonly TextBlock _extraUsage = NewLine();
    private readonly TextBlock _today = NewLine();
    private readonly TextBlock _window31d = NewLine();
    private readonly TextBlock _caveat = NewLine();
    private readonly TextBlock _modelNote = NewLine();
    private readonly ItemsControl _modelRows = new();
    private bool _dragging;
    private PixelPoint _dragStartPointerScreen;
    private PixelPoint _dragStartWindow;

    public DetailWindow(ISkinToShell skinToShell, DetailWindowPositionController position, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(skinToShell);
        ArgumentNullException.ThrowIfNull(position);

        _skinToShell = skinToShell;
        _position = position;
        _log = log ?? (message => Console.Error.WriteLine(message));

        Title = "O-view";
        Width = DefaultWidth;
        Height = DefaultHeight;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.White;
        Content = BuildLayout();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        Deactivated += (_, _) => _skinToShell.RequestWidget(false);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) _skinToShell.RequestWidget(false); };
    }

    /// <summary>
    /// Shows the window at the position <see cref="DetailWindowPositionController.ResolveShowPosition"/>
    /// resolves (the first-run corner, or the last dragged spot). Called from
    /// <see cref="LinuxShellToSkin.VisibilityChanged"/> when the shell answers
    /// <see langword="true"/> — never called by this window on itself (ADR-0008 D9b). Logs the
    /// requested position and, once shown, the position actually granted — the window manager
    /// is free to ignore or clamp either one (D4).
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            Hide();
            return;
        }

        var (x, y) = _position.ResolveShowPosition();
        var requested = new PixelPoint((int)x, (int)y);
        Position = requested;
        _log($"O-view detail window: requested position {requested}");

        Show();
        Activate();

        _log($"O-view detail window: granted position {Position}");
    }

    /// <summary>
    /// The untested half of ADR-0009 slice 10's repaint: converts
    /// <see cref="LinuxWindowThemeColors"/> — plain RGB, decided by
    /// <see cref="LinuxThemeRepaintController"/> against a fake — into the Avalonia
    /// <see cref="SolidColorBrush"/>es this window actually paints with. No decision is made
    /// here; see the controller for that (the same "decision tested, adapter not" split every
    /// other control in this skin already uses). <see cref="Foreground"/> is set on the window
    /// itself rather than on each <see cref="TextBlock"/> individually, since it is an inherited
    /// styled property every child text element already reads from its nearest ancestor.
    /// </summary>
    public void ApplyTheme(LinuxWindowThemeColors colors)
    {
        Background = ToBrush(colors.Background);
        Foreground = ToBrush(colors.Foreground);
        var border = ToBrush(colors.Border);

        if (Content is ScrollViewer { Content: StackPanel outer })
        {
            foreach (var separator in outer.Children)
            {
                if (separator is Separator separatorControl)
                {
                    separatorControl.Background = border;
                }
            }
        }
    }

    private static SolidColorBrush ToBrush(LinuxRgbColor color) => new(Color.FromRgb(color.R, color.G, color.B));

    /// <summary>Renders the exact pushed detail and nothing else — see the type remarks.</summary>
    public void ShowDetail(UsageDetail detail)
    {
        var content = DetailWindowContentBuilder.Build(detail, DateTimeOffset.UtcNow, TimeZoneInfo.Local);

        _freshness.Text = content.Freshness;
        _session.Text = content.SessionLine;
        _weekly.Text = content.WeeklyLine;
        _extraUsage.Text = content.ExtraUsageLine;
        _extraUsage.IsVisible = !string.IsNullOrEmpty(content.ExtraUsageLine);
        _today.Text = content.TodayLine;
        _window31d.Text = content.Window31dLine;
        _caveat.Text = content.Caveat;
        _caveat.IsVisible = !string.IsNullOrEmpty(content.Caveat);
        _modelNote.Text = content.ModelSectionNote;
        _modelNote.IsVisible = !string.IsNullOrEmpty(content.ModelSectionNote);
        _modelRows.ItemsSource = content.ModelRows.Select(ModelRowText).ToList();
    }

    private static string ModelRowText(DetailWindowModelRow row) =>
        $"{row.ModelId} — {row.Requests} req · in {row.InputTokens} · out {row.OutputTokens} " +
        $"· cache w {row.CacheWriteTokens} · cache r {row.CacheReadTokens} · {row.EstimatedSpend}";

    private Control BuildLayout()
    {
        var modelSection = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        modelSection.Children.Add(new TextBlock { Text = "By model", FontWeight = FontWeight.Bold });
        modelSection.Children.Add(_modelNote);
        modelSection.Children.Add(_modelRows);

        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(_freshness);
        stack.Children.Add(_session);
        stack.Children.Add(_weekly);
        stack.Children.Add(_extraUsage);
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        stack.Children.Add(_today);
        stack.Children.Add(_window31d);
        stack.Children.Add(_caveat);
        stack.Children.Add(modelSection);

        return new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
    }

    private static TextBlock NewLine() => new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        _dragStartPointerScreen = this.PointToScreen(e.GetPosition(this));
        _dragStartWindow = Position;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = this.PointToScreen(e.GetPosition(this));
        var dx = current.X - _dragStartPointerScreen.X;
        var dy = current.Y - _dragStartPointerScreen.Y;
        Position = new PixelPoint(_dragStartWindow.X + dx, _dragStartWindow.Y + dy);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        _position.OnDragEnd(Position.X, Position.Y);
    }
}
