using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OView.Tray.Presentation;

/// <summary>What a key press on a branded dialog resolves to (gate G7 parity slice P24,
/// OVI-718, source <c>ui-spec.md</c> §5: "Esc cancels; Enter takes the primary action"). A plain
/// enum rather than acting directly, so <see cref="BrandedDialogKeyRouter.Resolve"/> is a pure
/// function a test can call with no window, no HWND, and no dispatcher involved.</summary>
internal enum BrandedDialogKeyAction
{
    None,
    Confirm,
    Cancel,
}

/// <summary>
/// The key-routing decision every branded dialog in this skin shares (<see cref="BrandedDialogWindow"/>
/// and <see cref="WeeklyResetEntryDialog"/> both call this from their own <c>PreviewKeyDown</c>
/// handler) — pulled out so it is testable without a real window, the same split
/// <see cref="OView.Tray.Platform.ForegroundWindowTaker"/> already uses between "decide" and
/// "carry out".
/// </summary>
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
/// source <c>ui-spec.md</c> §5): the brand mark, and the two weighted action visuals — an
/// accent-filled primary and an outlined secondary, "rather than two identical buttons the user
/// must read both of to tell apart".
///
/// <para>Built as plain <see cref="Border"/>/<see cref="TextBlock"/> composition with a
/// <c>MouseLeftButtonDown</c> handler, not <see cref="System.Windows.Controls.Button"/> — this
/// skin's own existing convention for a clickable region
/// (<c>DetailWindow</c>'s weekly-reset row, off-plan banner link, and token-kind toggle are all
/// built the same way) rather than fighting a <c>Button</c>'s own default chrome with a
/// <c>ControlTemplate</c>.</para>
///
/// <para><b>The brand mark.</b> CONFIRMED by grep: no "BrandArc"/ellipse-arc brand-mark geometry
/// exists anywhere in this skin today — <c>DetailWindow</c>'s header is plain text ("O-view"),
/// and <c>StatusIconGlyphRenderer</c> draws only a single filled circle for the tray icon, not a
/// ring-plus-pupil mark. Rather than inventing a second, ungoverned accent gradient to match the
/// source's own two-stop <c>BrandArc</c> brush, this mark reuses the one accent
/// <see cref="WindowThemePalette"/> already measured and ships everywhere else in this window
/// (<see cref="WindowThemeColors.Accent"/>) — a plain ring stroke and a filled pupil in that same
/// colour, not a new palette entry.</para>
/// </summary>
internal static class BrandedDialogChrome
{
    public const double ButtonHeight = 32;
    public const double ButtonCornerRadius = 6;
    public const double CardCornerRadius = 10;

    /// <summary>The ring-plus-pupil brand mark, in the window's own accent colour. See the type
    /// remarks for why this draws a plain mark rather than reusing a gradient that does not exist
    /// in this skin.</summary>
    public static UIElement BuildBrandMark(RgbColor accent)
    {
        var brush = ToBrush(accent);
        var grid = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Stroke = brush,
            StrokeThickness = 2.5,
        });
        grid.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 5.5,
            Height = 5.5,
            Fill = brush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return grid;
    }

    /// <summary>The accent-filled primary action — named for what it does
    /// (<c>Update now</c>, <c>Save</c>), never "OK"/"Yes" (source <c>ui-spec.md</c> §5).</summary>
    public static Border BuildPrimaryAction(string label, RgbColor accent, RgbColor accentHover, out TextBlock labelBlock)
    {
        labelBlock = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White };
        var border = new Border
        {
            Height = ButtonHeight,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(ButtonCornerRadius),
            Background = ToBrush(accent),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new ContentControl { Content = labelBlock, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        border.MouseEnter += (_, _) => border.Background = ToBrush(accentHover);
        border.MouseLeave += (_, _) => border.Background = ToBrush(accent);
        return border;
    }

    /// <summary>The outlined secondary action — quieter than the primary by design, so only one
    /// thing on the dialog reads as loud (source <c>ui-spec.md</c> §5). The hover fill is the
    /// window's own border colour at low opacity rather than a new palette entry — this skin's
    /// <see cref="WindowThemeColors"/> carries no separate "row hover" colour yet, same reasoning
    /// <see cref="HoverCard"/>'s own <c>MutedBrush</c> already gives for dimming an existing
    /// colour instead of widening the shared record for one caller.</summary>
    public static Border BuildSecondaryAction(string label, RgbColor foreground, RgbColor border, out TextBlock labelBlock)
    {
        labelBlock = new TextBlock { Text = label, FontSize = 12, Foreground = ToBrush(foreground) };
        var hoverFill = ToBrush(border);
        hoverFill.Opacity = 0.15;
        hoverFill.Freeze();

        var visual = new Border
        {
            Height = ButtonHeight,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(ButtonCornerRadius),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = ToBrush(border),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new ContentControl { Content = labelBlock, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        visual.MouseEnter += (_, _) => visual.Background = hoverFill;
        visual.MouseLeave += (_, _) => visual.Background = System.Windows.Media.Brushes.Transparent;
        return visual;
    }

    public static SolidColorBrush ToBrush(RgbColor color) => new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
}
