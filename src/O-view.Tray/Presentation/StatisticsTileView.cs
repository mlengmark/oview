using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;

namespace OView.Tray.Presentation;

/// <summary>
/// One of the detail window's 2x2 statistics tiles (ADR-0008 D10b, gate G7 parity slice P10): a
/// fixed-size card that flips between its own figure and a per-model stacked bar, both of which
/// <see cref="Populate"/> builds from the pushed <see cref="StatisticsTile"/> up front — a click
/// only ever toggles which face is visible, never recomputes the breakdown (the "no I/O on
/// click" requirement).
///
/// <para>Both faces stay in this control's visual tree at all times; only their
/// <see cref="UIElement.Visibility"/> moves between <see cref="Visibility.Visible"/> and
/// <see cref="Visibility.Hidden"/> — never <see cref="Visibility.Collapsed"/>. A
/// <see cref="Visibility.Hidden"/> element still takes part in its parent's measure pass, so this
/// tile's own measured size is identical whichever face is showing (the slicing table's "size
/// stability" obligation) — <see cref="Visibility.Collapsed"/> would not give that guarantee,
/// since a collapsed element is removed from layout entirely. The tile's own outer
/// <see cref="Border"/> is additionally pinned to a fixed <see cref="FrameworkElement.Width"/>/
/// <see cref="FrameworkElement.Height"/> rather than auto-sized to either face, so the
/// obligation holds doubly here.</para>
///
/// <para>A tile with nothing to flip to (<see cref="StatisticsTile.CanFlip"/> is
/// <see langword="false"/>) shows no affordance glyph at all and does not respond to a click —
/// the window's own disabled-tile rule.</para>
/// </summary>
internal sealed class StatisticsTileView : Border
{
    public const double TileWidth = 180;
    public const double TileHeight = 72;
    private const double BreakdownWidth = 150;
    private const double BreakdownHeight = 16;

    private readonly TextBlock _label = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _value = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
    private readonly StackPanel _front;
    private readonly StackPanel _breakdown = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _glyph;
    private WindowThemeColors _colors;
    private bool _canFlip;

    /// <summary>Whether the back (stacked-bar) face is currently showing.</summary>
    public bool IsFlipped { get; private set; }

    public StatisticsTileView()
    {
        Width = TileWidth;
        Height = TileHeight;
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(10, 8, 10, 8);

        _front = new StackPanel();
        _front.Children.Add(_label);
        _front.Children.Add(_value);

        _breakdown.Visibility = Visibility.Hidden;

        var faces = new Grid();
        faces.Children.Add(_front);
        faces.Children.Add(_breakdown);

        _glyph = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Hidden,
        };

        var root = new Grid();
        root.Children.Add(faces);
        root.Children.Add(_glyph);
        Child = root;

        MouseLeftButtonDown += OnClicked;
        MouseEnter += (_, _) => SetGlyphHighlighted(true);
        MouseLeave += (_, _) => SetGlyphHighlighted(false);
    }

    /// <summary>
    /// Builds both faces from <paramref name="tile"/> — the one place this view ever reads a
    /// breakdown. Always resets to the front face, since a newly pushed detail's figures no
    /// longer belong to whatever flip state the previous one left behind.
    /// </summary>
    public void Populate(StatisticsTile tile, WindowThemeColors colors)
    {
        _colors = colors;
        _label.Text = tile.Label;
        _value.Text = tile.Value;
        _canFlip = tile.CanFlip;
        _glyph.Visibility = tile.CanFlip ? Visibility.Visible : Visibility.Hidden;
        Cursor = tile.CanFlip ? Cursors.Hand : Cursors.Arrow;
        ToolTip = null;

        _breakdown.Children.Clear();
        foreach (var segment in tile.Breakdown)
        {
            _breakdown.Children.Add(new Border
            {
                Width = Math.Max(2.0, BreakdownWidth * segment.Fraction),
                Height = BreakdownHeight,
                Margin = new Thickness(0, 0, 1, 0),
                Background = ToBrush(colors.Accent),
            });
        }

        IsFlipped = false;
        _front.Visibility = Visibility.Visible;
        _breakdown.Visibility = Visibility.Hidden;
        ApplyTheme(colors);
    }

    public void ApplyTheme(WindowThemeColors colors)
    {
        _colors = colors;
        Background = ToBrush(colors.Background);
        BorderBrush = ToBrush(colors.Border);
        _label.Foreground = ToBrush(colors.Foreground);
        _value.Foreground = ToBrush(colors.Foreground);
        SetGlyphHighlighted(false);
    }

    private void OnClicked(object sender, MouseButtonEventArgs e)
    {
        if (!_canFlip)
        {
            return;
        }

        ToggleFlip();
        e.Handled = true;
    }

    /// <summary>Toggles which face is visible — see the type remarks for why this never touches
    /// <see cref="StatisticsTile.Breakdown"/> again. A no-op on a tile that cannot flip, same
    /// guard the click handler applies, so a test (or a future keyboard activation) can call
    /// this directly without first simulating a mouse event.</summary>
    public void ToggleFlip()
    {
        if (!_canFlip)
        {
            return;
        }

        IsFlipped = !IsFlipped;
        _front.Visibility = IsFlipped ? Visibility.Hidden : Visibility.Visible;
        _breakdown.Visibility = IsFlipped ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>The affordance glyph brightens on hover (gate G7 parity slice P10) — a plain
    /// colour swap between the window's own accent and its hover step, same two colours
    /// <see cref="DetailWindow"/>'s branded surfaces already use (ADR-0008 slicing-table slice
    /// P3). Has no visible effect while the glyph itself is hidden (a disabled tile). Internal,
    /// not private, so a test can drive the same hover transition the mouse handlers trigger
    /// without simulating a real mouse event.</summary>
    internal void SetGlyphHighlighted(bool highlighted) =>
        _glyph.Background = ToBrush(highlighted ? _colors.AccentHover : _colors.Accent);

    /// <summary>Whether the affordance glyph is currently shown at all — <see langword="false"/>
    /// for a disabled tile (<see cref="StatisticsTile.CanFlip"/> was <see langword="false"/>),
    /// regardless of hover state.</summary>
    internal bool IsGlyphVisible => _glyph.Visibility == Visibility.Visible;

    /// <summary>The glyph's current fill — exposed so a test can prove <see cref="SetGlyphHighlighted"/>
    /// actually changes it, without reaching into this control's own visual tree.</summary>
    internal Brush GlyphBrush => _glyph.Background;

    private static SolidColorBrush ToBrush(RgbColor color) => new(Color.FromRgb(color.R, color.G, color.B));
}
