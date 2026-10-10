using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OView.App;
using OView.Core.Models;
using OView.Tray.Platform;
using OView.Tray.Presentation;

namespace OView.Tray;

/// <summary>
/// The Windows detail window (ADR-0008 slice 6, OVI-386): a draggable, borderless widget that
/// renders only the last <see cref="UsageDetail"/> the shell pushed through
/// <see cref="IShellToSkin.ShowDetail"/> — it totals or formats nothing itself (D9c); every
/// figure on screen is <see cref="DetailWindowContentBuilder.Build"/>'s output, already run
/// through the existing formatters. Built in plain C# rather than XAML: this project has no
/// other <c>.xaml</c> file yet (<c>TrayStatusIcon</c>'s GDI icon is the only other rendering
/// surface), and a dozen bound <c>TextBlock</c>s do not need a markup compiler.
///
/// <para>Not unit-tested (no interactive Windows desktop in this environment — the same
/// "not verified" boundary slices 3/4/5 already recorded for their own OS adapters). Every
/// decision this window merely carries out — which position to open at, when that position
/// changed, what to say — is pulled out into <see cref="DetailWindowContentBuilder"/> and
/// <see cref="DetailWindowPositionController"/>, both proven against fakes with no window
/// involved.</para>
///
/// <para>ADR-0009 slice 7 (OVI-489) adds <see cref="ApplyTheme"/>: a
/// <see cref="ThemeRepaintController"/> applies the current <see cref="IThemeSource"/> reading
/// immediately on construction and again on every live change, converting
/// <see cref="WindowThemePalette"/>'s plain RGB values into the <see cref="SolidColorBrush"/>es
/// this window actually paints with — a conversion that belongs here, in the WPF adapter, not in
/// the shared palette.</para>
/// </summary>
internal sealed class DetailWindow : Window
{
    /// <summary>This window's fixed size, named so the composition root can feed the same
    /// figures into <see cref="DetailWindowPlacement.Compute"/> without constructing the
    /// window first (the position controller is built before the window is, since the
    /// window's constructor takes the controller). ADR-0008 slicing-table slice P3 (OVI-622,
    /// G7 parity): ~400 px per the source's own <c>ui-spec.md</c> §2 ("Roughly 400 px wide") —
    /// the 281 px figure Adrian's correction (OVI-585) traced to a text-row truncation budget
    /// <i>inside</i> the window, not the window's own width.</summary>
    public const double DefaultWidth = 400;
    public const double DefaultHeight = 360;

    /// <summary>The usage bars' track width (ADR-0008 D10b, gate G7 parity slice P9) — this
    /// window's content width (<see cref="DefaultWidth"/> less its 12px margin on each side)
    /// less a little more room than the text rows need, so the bar never touches the scrollbar.</summary>
    private const double BarTrackWidth = 350;
    private const double BarTrackHeight = 8;

    private readonly ISkinToShell _skinToShell;
    private readonly DetailWindowPositionController _position;
    private readonly ThemeRepaintController _theme;
    private readonly ForegroundWindowTaker _foreground = new();
    private readonly TextBlock _freshness = NewLine();
    private readonly TextBlock _accountDisplayName = NewLine();
    private readonly TextBlock _accountEmail = NewLine();
    private readonly TextBlock _accountTierBadgeText = new() { FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White };
    private readonly Border _accountTierBadge;
    private readonly TextBlock _session = NewLine();
    private readonly Border _sessionBarFill = NewBarFill();
    private readonly StackPanel _weeklyRow = new();
    private readonly TextBlock _weekly = NewLine();
    private readonly Border _weeklyBarFill = NewBarFill();
    private readonly TextBlock _extraUsage = NewLine();
    private readonly TextBlock _today = NewLine();
    private readonly TextBlock _window31d = NewLine();
    private readonly TextBlock _caveat = NewLine();
    private readonly TextBlock _modelNote = NewLine();
    private readonly ItemsControl _modelRows = new();
    private WindowThemeColors _colors;

    /// <summary>Whether the weekly row's click currently copies <c>/usage</c> (gate G7 parity
    /// slice P9) — only while <see cref="WeeklyBarState.NotKnown"/>; a click anywhere else on
    /// this row does nothing but start a window drag, same as every other row.</summary>
    private bool _weeklyRowCopiesUsageCommand;

    private bool _dragging;
    private System.Windows.Point _dragStartMouse;
    private System.Windows.Point _dragStartWindow;

    public DetailWindow(ISkinToShell skinToShell, DetailWindowPositionController position, IThemeSource themeSource)
    {
        ArgumentNullException.ThrowIfNull(skinToShell);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(themeSource);

        _skinToShell = skinToShell;
        _position = position;
        _accountTierBadge = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Child = _accountTierBadgeText,
        };

        Title = "O-view";
        Width = DefaultWidth;
        Height = DefaultHeight;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        BorderThickness = new Thickness(1);
        Content = BuildLayout();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        Deactivated += (_, _) => _skinToShell.RequestWidget(false);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) _skinToShell.RequestWidget(false); };
        _weeklyRow.MouseLeftButtonDown += OnWeeklyRowClicked;

        _theme = new ThemeRepaintController(themeSource, ApplyTheme);
    }

    /// <summary>Repaints this window's background, border and text colour from a live
    /// <see cref="IThemeSource"/> reading (ADR-0009 slice 7). <see cref="TextBlock.Foreground"/>
    /// is set once here, at the window root, and reaches every child <see cref="TextBlock"/> in
    /// <see cref="BuildLayout"/> through WPF's own property-value inheritance — none of them set
    /// a local <c>Foreground</c> that would shadow it. Also repaints the bar tracks (gate G7
    /// parity slice P9), which are plain <see cref="Border"/>s with no inherited brush to read.</summary>
    private void ApplyTheme(WindowThemeColors colors)
    {
        _colors = colors;
        Background = ToBrush(colors.Background);
        BorderBrush = ToBrush(colors.Border);
        Foreground = ToBrush(colors.Foreground);
        _accountTierBadge.Background = ToBrush(colors.Accent);

        var track = ToBrush(colors.Border);
        if (_sessionBarFill.Parent is Border sessionTrack)
        {
            sessionTrack.Background = track;
        }

        if (_weeklyBarFill.Parent is Border weeklyTrack)
        {
            weeklyTrack.Background = track;
        }
    }

    private static SolidColorBrush ToBrush(RgbColor color) =>
        new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));

    /// <summary>
    /// Shows the window at the position <see cref="DetailWindowPositionController.ResolveShowPosition"/>
    /// resolves (the first-run corner, or the last dragged spot). Called from
    /// <see cref="TrayShellToSkin.VisibilityChanged"/> when the shell answers
    /// <see langword="true"/> — never called by this window on itself (ADR-0008 D9b).
    ///
    /// <para>Takes the foreground explicitly (<see cref="ForegroundWindowTaker"/>, ADR-0008
    /// D9b amended OVI-601) rather than relying on <see cref="Activate"/> alone: a tray-resident
    /// app owns no already-activated window, so <c>Activate()</c>'s underlying
    /// <c>SetForegroundWindow</c> call is not guaranteed to succeed, and a window shown but
    /// never actually foregrounded never raises <see cref="Window.Deactivated"/> — it would
    /// stay on screen with no way to dismiss it.</para>
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            Hide();
            return;
        }

        var (x, y) = _position.ResolveShowPosition();
        Left = x;
        Top = y;
        Show();
        Activate();
        _foreground.Take(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Renders the exact pushed detail and nothing else — see the type remarks.</summary>
    public void ShowDetail(UsageDetail detail)
    {
        var content = DetailWindowContentBuilder.Build(detail, DateTimeOffset.UtcNow, TimeZoneInfo.Local);

        _freshness.Text = content.Freshness;
        _accountDisplayName.Text = content.AccountDisplayName;
        _accountEmail.Text = content.AccountEmail;
        _accountTierBadgeText.Text = content.AccountTierBadge;
        _accountTierBadge.Visibility = string.IsNullOrEmpty(content.AccountTierBadge) ? Visibility.Collapsed : Visibility.Visible;
        _session.Text = content.SessionLine;
        _sessionBarFill.Width = BarTrackWidth * content.SessionBarFraction;
        _sessionBarFill.Background = ToBrush(WindowThemePalette.BandColor(content.SessionBarBand));

        _weekly.Text = content.WeeklyLine;
        _weeklyBarFill.Width = BarTrackWidth * content.WeeklyBarFraction;
        _weeklyBarFill.Background = ToBrush(WindowThemePalette.BandColor(content.WeeklyBarBand));
        _weeklyRow.Visibility = content.WeeklyState == WeeklyBarState.Hidden ? Visibility.Collapsed : Visibility.Visible;

        _weeklyRowCopiesUsageCommand = content.WeeklyState == WeeklyBarState.NotKnown;
        if (_weeklyRowCopiesUsageCommand)
        {
            _weeklyRow.ToolTip = HoverCard.Text(content.WeeklyUnknownHint, _colors);
            HoverCard.ApplyTiming(_weeklyRow);
            _weeklyRow.Cursor = System.Windows.Input.Cursors.Hand;
        }
        else
        {
            _weeklyRow.ToolTip = null;
            _weeklyRow.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        _extraUsage.Text = content.ExtraUsageLine;
        _extraUsage.Visibility = string.IsNullOrEmpty(content.ExtraUsageLine) ? Visibility.Collapsed : Visibility.Visible;
        _today.Text = content.TodayLine;
        _window31d.Text = content.Window31dLine;
        _caveat.Text = content.Caveat;
        _caveat.Visibility = string.IsNullOrEmpty(content.Caveat) ? Visibility.Collapsed : Visibility.Visible;
        _modelNote.Text = content.ModelSectionNote;
        _modelNote.Visibility = string.IsNullOrEmpty(content.ModelSectionNote) ? Visibility.Collapsed : Visibility.Visible;
        _modelRows.ItemsSource = content.ModelRows.Select(ModelRowText).ToList();
    }

    /// <summary>
    /// Copies <see cref="PanelTextFormatter.RunUsageCommand"/> to the clipboard when the weekly
    /// reset is <see cref="WeeklyBarState.NotKnown"/> (gate G7 parity slice P9, source
    /// <c>ui-spec.md</c> §"Weekly reset": "clicking copies <c>/usage</c>"). Marks the event
    /// handled so the window-level drag handler (<see cref="OnMouseLeftButtonDown"/>) does not
    /// also start a drag from the same click.
    /// </summary>
    private void OnWeeklyRowClicked(object sender, MouseButtonEventArgs e)
    {
        if (!_weeklyRowCopiesUsageCommand)
        {
            return;
        }

        System.Windows.Clipboard.SetText(PanelTextFormatter.RunUsageCommand);
        e.Handled = true;
    }

    private static string ModelRowText(DetailWindowModelRow row) =>
        $"{row.ModelId} — {row.Requests} req · in {row.InputTokens} · out {row.OutputTokens} " +
        $"· cache w {row.CacheWriteTokens} · cache r {row.CacheReadTokens} · {row.EstimatedSpend}";

    /// <summary>
    /// The header (ADR-0008 D2 §B, gate G7 parity slice P8): the <c>O-view</c> title and the
    /// freshness line at the left, the account block (display name, email, tier badge) at the
    /// right — <c>ui-spec.md</c>'s own "top left"/"top right" placement, built from two
    /// vertical stacks side by side rather than a <see cref="System.Windows.Controls.Grid"/>,
    /// matching every other layout in this window.
    /// </summary>
    private UIElement BuildHeader()
    {
        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = "O-view", FontWeight = FontWeights.Bold, FontSize = 14 });
        left.Children.Add(_freshness);

        var right = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        right.Children.Add(_accountDisplayName);
        right.Children.Add(_accountEmail);
        right.Children.Add(_accountTierBadge);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(right, 1);
        header.Children.Add(left);
        header.Children.Add(right);

        return header;
    }

    private UIElement BuildLayout()
    {
        var modelSection = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        modelSection.Children.Add(new TextBlock { Text = "By model", FontWeight = FontWeights.Bold });
        modelSection.Children.Add(_modelNote);
        modelSection.Children.Add(_modelRows);

        var sessionRow = new StackPanel();
        sessionRow.Children.Add(BuildBarTrack(_sessionBarFill));
        sessionRow.Children.Add(_session);

        _weeklyRow.Children.Add(BuildBarTrack(_weeklyBarFill));
        _weeklyRow.Children.Add(_weekly);

        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(BuildHeader());
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        stack.Children.Add(sessionRow);
        stack.Children.Add(_weeklyRow);
        stack.Children.Add(_extraUsage);
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        stack.Children.Add(_today);
        stack.Children.Add(_window31d);
        stack.Children.Add(_caveat);
        stack.Children.Add(modelSection);

        return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>The bar track (gate G7 parity slice P9): a fixed-width background strip holding
    /// <paramref name="fill"/>, whose width <see cref="ShowDetail"/> sets to the proportional
    /// fill — the track's own background is the only thing <see cref="ApplyTheme"/> repaints on
    /// it, since <paramref name="fill"/> paints its own band colour.</summary>
    private static Border BuildBarTrack(Border fill) => new()
    {
        Width = BarTrackWidth,
        Height = BarTrackHeight,
        CornerRadius = new CornerRadius(BarTrackHeight / 2),
        Margin = new Thickness(0, 4, 0, 2),
        Child = fill,
        ClipToBounds = true,
    };

    private static Border NewBarFill() => new()
    {
        Height = BarTrackHeight,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(BarTrackHeight / 2),
    };

    private static TextBlock NewLine() => new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStartMouse = PointToScreen(e.GetPosition(this));
        _dragStartWindow = new System.Windows.Point(Left, Top);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        Left = _dragStartWindow.X + (current.X - _dragStartMouse.X);
        Top = _dragStartWindow.Y + (current.Y - _dragStartMouse.Y);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        _position.OnDragEnd(Left, Top);
    }
}
