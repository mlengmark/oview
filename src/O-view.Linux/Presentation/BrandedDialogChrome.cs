using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace OView.Linux.Presentation;

/// <summary>What a key press on a branded dialog resolves to (gate G7 parity slice P24,
/// OVI-718, source <c>ui-spec.md</c> §5: "Esc cancels; Enter takes the primary action"). Same
/// pure-function shape as <c>O-view.Tray</c>'s own <c>BrandedDialogKeyRouter</c> — independently
/// implemented, not shared (D1): nothing here is the same enum or the same type, only the same
/// decision.</summary>
internal enum BrandedDialogKeyAction
{
    None,
    Confirm,
    Cancel,
}

internal static class BrandedDialogKeyRouter
{
    public static BrandedDialogKeyAction Resolve(Key key) => key switch
    {
        Key.Escape => BrandedDialogKeyAction.Cancel,
        Key.Enter => BrandedDialogKeyAction.Confirm,
        _ => BrandedDialogKeyAction.None,
    };
}

/// <summary>
/// The chrome every branded dialog in this skin shares (gate G7 parity slice P24, OVI-718,
/// source <c>ui-spec.md</c> §5), the Linux counterpart of <c>O-view.Tray</c>'s own
/// <c>BrandedDialogChrome</c> — independently implemented, not shared (D1): the brand mark, and
/// the two weighted action visuals, an accent-filled primary and an outlined secondary.
///
/// <para>Built as plain <see cref="Border"/>/<see cref="TextBlock"/> composition with a
/// <c>PointerPressed</c> handler, not a real Avalonia <see cref="Button"/> — this skin's own
/// existing convention (<c>DetailWindow</c>'s weekly-reset row, off-plan banner link, and
/// token-kind toggle all follow the same shape) rather than restyling a <c>Button</c>'s own
/// default template.</para>
///
/// <para><b>The brand mark.</b> CONFIRMED by grep: no "BrandArc"/ellipse-arc brand-mark geometry
/// exists anywhere in this skin either — <c>DetailWindow</c>'s header is plain text, and this
/// skin's own tray-icon renderer (<c>StatusIconGlyphRenderer</c>, shared source logic ported
/// independently per-skin) draws a single filled circle, not a ring-plus-pupil mark. Same
/// reasoning as the Windows skin's own remarks: a plain ring stroke and filled pupil in the
/// window's own <see cref="LinuxWindowThemeColors.Accent"/>, not a new palette entry.</para>
/// </summary>
internal static class BrandedDialogChrome
{
    public const double ButtonHeight = 32;
    public const double ButtonCornerRadius = 6;
    public const double CardCornerRadius = 10;

    public static Control BuildBrandMark(LinuxRgbColor accent)
    {
        var brush = ToBrush(accent);
        var grid = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new Ellipse
        {
            Width = 16,
            Height = 16,
            Stroke = brush,
            StrokeThickness = 2.5,
        });
        grid.Children.Add(new Ellipse
        {
            Width = 5.5,
            Height = 5.5,
            Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return grid;
    }

    /// <summary>The accent-filled primary action — named for what it does, never "OK"/"Yes"
    /// (source <c>ui-spec.md</c> §5).</summary>
    public static Border BuildPrimaryAction(string label, LinuxRgbColor accent, LinuxRgbColor accentHover, out TextBlock labelBlock)
    {
        labelBlock = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
        var border = new Border
        {
            Height = ButtonHeight,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(ButtonCornerRadius),
            Background = ToBrush(accent),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new ContentControl { Content = labelBlock, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        border.PointerEntered += (_, _) => border.Background = ToBrush(accentHover);
        border.PointerExited += (_, _) => border.Background = ToBrush(accent);
        return border;
    }

    /// <summary>The outlined secondary action — quieter than the primary by design (source
    /// <c>ui-spec.md</c> §5). The hover fill is the window's own border colour at low opacity —
    /// this skin's <see cref="LinuxWindowThemeColors"/> carries no separate "row hover" colour
    /// yet, same reasoning <c>HoverCard</c>'s own <c>MutedBrush</c> already gives.</summary>
    public static Border BuildSecondaryAction(string label, LinuxRgbColor foreground, LinuxRgbColor border, out TextBlock labelBlock)
    {
        labelBlock = new TextBlock { Text = label, FontSize = 12, Foreground = ToBrush(foreground) };
        var borderColor = ToColor(border);
        var hoverFill = new SolidColorBrush(borderColor, 0.15);

        var visual = new Border
        {
            Height = ButtonHeight,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(ButtonCornerRadius),
            Background = Brushes.Transparent,
            BorderBrush = ToBrush(border),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new ContentControl { Content = labelBlock, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        visual.PointerEntered += (_, _) => visual.Background = hoverFill;
        visual.PointerExited += (_, _) => visual.Background = Brushes.Transparent;
        return visual;
    }

    public static SolidColorBrush ToBrush(LinuxRgbColor color) => new(ToColor(color));

    private static Color ToColor(LinuxRgbColor color) => Color.FromRgb(color.R, color.G, color.B);
}
