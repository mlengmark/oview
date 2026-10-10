using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

    /// <summary>The usage bars' track width (ADR-0008 D10b, gate G7 parity slice P9) — this
    /// window's content width (<see cref="DefaultWidth"/> less its 12px margin on each side)
    /// less a little more room than the text rows need, so the bar never touches the scrollbar.</summary>
    private const double BarTrackWidth = 350;
    private const double BarTrackHeight = 8;

    private readonly ISkinToShell _skinToShell;
    private readonly DetailWindowPositionController _position;
    private readonly Action<string> _log;
    private readonly TextBlock _freshness = NewLine();
    private readonly TextBlock _accountDisplayName = NewLine();
    private readonly TextBlock _accountEmail = NewLine();
    private readonly TextBlock _accountTierBadgeText = new() { FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
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
    private readonly TextBlock _tokenKindTodayLabel = NewLine();
    private readonly StackPanel _tokenKindTodayBar = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _tokenKindWindow31dLabel = NewLine();
    private readonly StackPanel _tokenKindWindow31dBar = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _tokenKindNote = NewLine();
    private readonly TextBlock _tokenKindToggle = new()
    {
        Margin = new Thickness(0, 4, 0, 0),
        Cursor = new Cursor(StandardCursorType.Hand),
        TextDecorations = Avalonia.Media.TextDecorations.Underline,
    };
    private readonly ItemsControl _tokenKindBreakdownRows = new() { IsVisible = false };
    private readonly TextBlock _modelNote = NewLine();
    private readonly ItemsControl _modelRows = new();
    private LinuxWindowThemeColors _colors;

    /// <summary>Whether the weekly row's click currently copies <c>/usage</c> (gate G7 parity
    /// slice P9) — only while <see cref="WeeklyBarState.NotKnown"/>.</summary>
    private bool _weeklyRowCopiesUsageCommand;

    /// <summary>The token-kind breakdown table's view switch (gate G7 parity slice P12): toggles
    /// <see cref="_tokenKindBreakdownRows"/>' visibility only — the rows themselves are built once
    /// in <see cref="ShowDetail"/>, never recomputed by this toggle (the same "no I/O on click"
    /// rule slice P10 established for a flipped statistics tile).</summary>
    private bool _tokenKindBreakdownVisible;

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
        _accountTierBadge = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = _accountTierBadgeText,
        };

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
        _weeklyRow.PointerPressed += OnWeeklyRowPointerPressed;
        _tokenKindToggle.PointerPressed += OnTokenKindTogglePointerPressed;
        _tokenKindToggle.Text = ToggleText(_tokenKindBreakdownVisible);
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
        _colors = colors;
        Background = ToBrush(colors.Background);
        Foreground = ToBrush(colors.Foreground);
        var border = ToBrush(colors.Border);
        _accountTierBadge.Background = ToBrush(colors.Accent);

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

        if (_sessionBarFill.Parent is Border sessionTrack)
        {
            sessionTrack.Background = border;
        }

        if (_weeklyBarFill.Parent is Border weeklyTrack)
        {
            weeklyTrack.Background = border;
        }

        if (_tokenKindTodayBar.Parent is Border tokenKindTodayTrack)
        {
            tokenKindTodayTrack.Background = border;
        }

        if (_tokenKindWindow31dBar.Parent is Border tokenKindWindow31dTrack)
        {
            tokenKindWindow31dTrack.Background = border;
        }
    }

    private static SolidColorBrush ToBrush(LinuxRgbColor color) => new(Color.FromRgb(color.R, color.G, color.B));

    /// <summary>Renders the exact pushed detail and nothing else — see the type remarks.</summary>
    public void ShowDetail(UsageDetail detail)
    {
        var content = DetailWindowContentBuilder.Build(detail, DateTimeOffset.UtcNow, TimeZoneInfo.Local);

        _freshness.Text = content.Freshness;
        _accountDisplayName.Text = content.AccountDisplayName;
        _accountEmail.Text = content.AccountEmail;
        _accountTierBadgeText.Text = content.AccountTierBadge;
        _accountTierBadge.IsVisible = !string.IsNullOrEmpty(content.AccountTierBadge);
        _session.Text = content.SessionLine;
        _sessionBarFill.Width = BarTrackWidth * content.SessionBarFraction;
        _sessionBarFill.Background = ToBrush(LinuxWindowThemePalette.BandColor(content.SessionBarBand));

        _weekly.Text = content.WeeklyLine;
        _weeklyBarFill.Width = BarTrackWidth * content.WeeklyBarFraction;
        _weeklyBarFill.Background = ToBrush(LinuxWindowThemePalette.BandColor(content.WeeklyBarBand));
        _weeklyRow.IsVisible = content.WeeklyState != WeeklyBarState.Hidden;

        _weeklyRowCopiesUsageCommand = content.WeeklyState == WeeklyBarState.NotKnown;
        if (_weeklyRowCopiesUsageCommand)
        {
            HoverCard.Text(_weeklyRow, content.WeeklyUnknownHint, _colors);
            _weeklyRow.Cursor = new Cursor(StandardCursorType.Hand);
        }
        else
        {
            ToolTip.SetTip(_weeklyRow, null);
            _weeklyRow.Cursor = Cursor.Default;
        }

        _extraUsage.Text = content.ExtraUsageLine;
        _extraUsage.IsVisible = !string.IsNullOrEmpty(content.ExtraUsageLine);
        _today.Text = content.TodayLine;
        _window31d.Text = content.Window31dLine;
        _caveat.Text = content.Caveat;
        _caveat.IsVisible = !string.IsNullOrEmpty(content.Caveat);

        _tokenKindTodayLabel.Text = content.TokenKindBarToday.Label + ": " + content.TokenKindBarToday.Total;
        FillTokenKindBar(_tokenKindTodayBar, content.TokenKindBarToday);
        _tokenKindWindow31dLabel.Text = content.TokenKindBarWindow31d.Label + ": " + content.TokenKindBarWindow31d.Total;
        FillTokenKindBar(_tokenKindWindow31dBar, content.TokenKindBarWindow31d);
        _tokenKindNote.Text = content.TokenKindSectionNote;
        _tokenKindNote.IsVisible = !string.IsNullOrEmpty(content.TokenKindSectionNote);
        _tokenKindBreakdownRows.ItemsSource = content.TokenKindBreakdownRows.Select(TokenKindBreakdownRowText).ToList();
        _tokenKindBreakdownVisible = false;
        _tokenKindBreakdownRows.IsVisible = false;
        _tokenKindToggle.Text = ToggleText(_tokenKindBreakdownVisible);
        _tokenKindToggle.IsVisible = content.TokenKindBreakdownRows.Count > 0;

        _modelNote.Text = content.ModelSectionNote;
        _modelNote.IsVisible = !string.IsNullOrEmpty(content.ModelSectionNote);
        _modelRows.ItemsSource = content.ModelRows.Select(ModelRowText).ToList();
    }

    /// <summary>
    /// Copies <see cref="PanelTextFormatter.RunUsageCommand"/> to the clipboard when the weekly
    /// reset is <see cref="WeeklyBarState.NotKnown"/> (gate G7 parity slice P9, source
    /// <c>ui-spec.md</c> §"Weekly reset": "clicking copies <c>/usage</c>"). <see cref="TopLevel.Clipboard"/>
    /// can be <see langword="null"/> on a desktop with no clipboard portal — the click then does
    /// nothing rather than throwing. Marks the event handled so the window-level drag handler
    /// (<see cref="OnPointerPressed"/>) does not also start a drag from the same click.
    /// </summary>
    private void OnWeeklyRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_weeklyRowCopiesUsageCommand || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        _ = Clipboard?.SetTextAsync(PanelTextFormatter.RunUsageCommand);
    }

    /// <summary>
    /// The breakdown table's view switch (gate G7 parity slice P12): toggles visibility only —
    /// <see cref="_tokenKindBreakdownRows"/> was already built in <see cref="ShowDetail"/>, so
    /// this click triggers no read. Marks the event handled so the window-level drag handler
    /// does not also start a drag from the same click.
    /// </summary>
    private void OnTokenKindTogglePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _tokenKindBreakdownVisible = !_tokenKindBreakdownVisible;
        _tokenKindBreakdownRows.IsVisible = _tokenKindBreakdownVisible;
        _tokenKindToggle.Text = ToggleText(_tokenKindBreakdownVisible);
        e.Handled = true;
    }

    private static string ToggleText(bool breakdownVisible) => breakdownVisible ? "Hide breakdown ▲" : "Show breakdown ▼";

    /// <summary>
    /// Rebuilds <paramref name="bar"/>'s segment children from an already-built
    /// <see cref="TokenKindBar"/> (gate G7 parity slice P12) — the segments themselves were
    /// computed once in <see cref="DetailWindowContentBuilder.Build"/>; this only turns each one
    /// into a coloured, hoverable <see cref="Border"/>. A bar with no data renders as a plain
    /// empty track (no children), never a fabricated full or empty-looking real reading.
    /// </summary>
    private void FillTokenKindBar(StackPanel bar, TokenKindBar content)
    {
        bar.Children.Clear();
        foreach (var segment in content.Segments)
        {
            var color = ToBrush(LinuxWindowThemePalette.TokenKindColor(segment.Kind));
            var segmentBorder = new Border
            {
                Width = Math.Max(0, BarTrackWidth * segment.Fraction),
                Height = BarTrackHeight,
                Background = color,
            };
            HoverCard.Figure(segmentBorder, segment.HoverFigure, segment.HoverCaption, _colors, color);
            bar.Children.Add(segmentBorder);
        }
    }

    private static string TokenKindBreakdownRowText(TokenKindBreakdownRow row) =>
        $"{row.Label} — today {row.TodayTokens} tokens ({row.TodayValue}) · 31 days {row.Window31dTokens} tokens ({row.Window31dValue})";

    private static string ModelRowText(DetailWindowModelRow row) =>
        $"{row.ModelId} — {row.Requests} req · in {row.InputTokens} · out {row.OutputTokens} " +
        $"· cache w {row.CacheWriteTokens} · cache r {row.CacheReadTokens} · {row.EstimatedSpend}";

    /// <summary>
    /// The header (ADR-0008 D2 §B, gate G7 parity slice P8), the Linux counterpart of
    /// <c>O-view.Tray</c>'s <c>DetailWindow.BuildHeader</c> — independently implemented, not
    /// shared (D1): the <c>O-view</c> title and the freshness line at the left, the account
    /// block (display name, email, tier badge) at the right.
    /// </summary>
    private Control BuildHeader()
    {
        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = "O-view", FontWeight = FontWeight.Bold, FontSize = 14 });
        left.Children.Add(_freshness);

        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(_accountDisplayName);
        right.Children.Add(_accountEmail);
        right.Children.Add(_accountTierBadge);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(right, 1);
        header.Children.Add(left);
        header.Children.Add(right);

        return header;
    }

    private Control BuildLayout()
    {
        var modelSection = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        modelSection.Children.Add(new TextBlock { Text = "By model", FontWeight = FontWeight.Bold });
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
        stack.Children.Add(BuildTokenKindSection());
        stack.Children.Add(modelSection);

        return new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
    }

    /// <summary>
    /// The token-kind bars section (gate G7 parity slice P12, ADR-0008 D9e), the Linux
    /// counterpart of <c>O-view.Tray</c>'s own <c>BuildTokenKindSection</c> — independently
    /// implemented, not shared (D1): two segmented bars (today, 31 days) split by token kind,
    /// with a breakdown table behind the view switch below them. Presented as its own section —
    /// a <i>superset</i> of the statistics tiles (slice P10), not a breakdown of them.
    /// </summary>
    private Control BuildTokenKindSection()
    {
        var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        section.Children.Add(new TextBlock { Text = "Token usage by kind", FontWeight = FontWeight.Bold });
        section.Children.Add(_tokenKindNote);
        section.Children.Add(_tokenKindTodayLabel);
        section.Children.Add(BuildSegmentedBarTrack(_tokenKindTodayBar));
        section.Children.Add(_tokenKindWindow31dLabel);
        section.Children.Add(BuildSegmentedBarTrack(_tokenKindWindow31dBar));
        section.Children.Add(_tokenKindToggle);
        section.Children.Add(_tokenKindBreakdownRows);
        section.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        return section;
    }

    private static Border BuildSegmentedBarTrack(StackPanel segments) => new()
    {
        Width = BarTrackWidth,
        Height = BarTrackHeight,
        CornerRadius = new CornerRadius(BarTrackHeight / 2),
        Margin = new Thickness(0, 4, 0, 2),
        Child = segments,
        ClipToBounds = true,
    };

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
        HorizontalAlignment = HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(BarTrackHeight / 2),
    };

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
