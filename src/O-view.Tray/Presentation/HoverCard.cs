using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using ToolTip = System.Windows.Controls.ToolTip;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0008 D11a/slicing-table slice P2 (OVI-621, gate G7 parity): the one styled hover card used
/// everywhere in the detail window, so no element ever falls back to the plain system tooltip.
/// Ported from the source's <c>Popup/HoverCard.cs</c> and <c>HoverCard.xaml</c>: same two shapes,
/// same three timings, same reasoning — but built in plain C# against this skin's own
/// <see cref="WindowThemeColors"/> rather than a <c>ResourceDictionary</c>, because this project
/// still has no other <c>.xaml</c> file (see <see cref="OView.Tray.DetailWindow"/>'s own remarks)
/// and a floating <see cref="System.Windows.Controls.ToolTip"/>'s content needs no markup
/// compiler either.
///
/// <para>This slice only builds the primitive and proves its timing — it does not yet wire any
/// of the detail window's own elements (sections B-H) to use it; later slices (P8-P19) do that as
/// each section lands.</para>
/// </summary>
internal static class HoverCard
{
    // Timing is applied per element, on purpose, every time — never inherited from a parent.
    // The source measured this exact bug: setting it once on a container looks right and
    // silently does not work, because the children resolve the framework's own defaults instead
    // (docs/ui-spec.md §I in the source repo). HoverTimingFixtureTests pins that each of several
    // sibling elements resolves these values independently.

    /// <summary>400 ms — the Windows convention; see the source's reasoning (measured unset: 1000 ms).</summary>
    public const int InitialDelayMs = 400;

    /// <summary>3000 ms — so sliding along adjacent marks reads as one reveal (measured unset: 100 ms).</summary>
    public const int BetweenDelayMs = 3000;

    /// <summary>20000 ms — a deliberate cap, not an extension (measured unset: <see cref="int.MaxValue"/>).</summary>
    public const int DurationMs = 20_000;

    /// <summary>Applies the shared delays. Must be called on each element that owns a card — see
    /// the type remarks.</summary>
    public static void ApplyTiming(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ToolTipService.SetInitialShowDelay(element, InitialDelayMs);
        ToolTipService.SetBetweenShowDelay(element, BetweenDelayMs);
        ToolTipService.SetShowDuration(element, DurationMs);
    }

    /// <summary>
    /// A card leading with a figure, with a quieter identifying line beneath it, and an optional
    /// colour swatch tying it to the mark being pointed at.
    /// </summary>
    /// <param name="figure">The headline — a token count, a value, a total.</param>
    /// <param name="caption">What the figure describes: a date, a model id.</param>
    /// <param name="colors">The window's current theme colours.</param>
    /// <param name="swatch">
    /// Optional. Carries identity where a card floats clear of a coloured mark and would
    /// otherwise lose its connection to the exact colour under the pointer.
    /// </param>
    public static ToolTip Figure(string figure, string caption, WindowThemeColors colors, Brush? swatch = null) =>
        Card(BuildFigureContent(figure, caption, colors, swatch), colors);

    /// <summary>A card carrying a sentence — caveats, explanations, and anything unmeasured.</summary>
    public static ToolTip Text(string text, WindowThemeColors colors) =>
        Card(BuildTextContent(text, colors), colors);

    /// <summary>
    /// Builds the figure card's visual content with no <see cref="ToolTip"/> wrapper, so the
    /// render-proof hook (<see cref="DetailWindowRenderProof"/>, P0) can capture exactly what a
    /// hovered element looks like. A <see cref="ToolTip"/> cannot be given a parent — it throws —
    /// so it can never appear in a screenshot of anything else; this is the same reason the
    /// source's <c>--tile-samples</c> rendered its cards standalone rather than inside the panel.
    /// </summary>
    internal static Border BuildFigureCard(string figure, string caption, WindowThemeColors colors, Brush? swatch = null) =>
        Chrome(BuildFigureContent(figure, caption, colors, swatch), colors);

    /// <summary>The text card's visual content with no <see cref="ToolTip"/> wrapper — see
    /// <see cref="BuildFigureCard"/>'s remarks.</summary>
    internal static Border BuildTextCard(string text, WindowThemeColors colors) =>
        Chrome(BuildTextContent(text, colors), colors);

    private static UIElement BuildFigureContent(string figure, string caption, WindowThemeColors colors, Brush? swatch)
    {
        var content = new StackPanel { MaxWidth = 220 };

        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        if (swatch is not null)
        {
            heading.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = swatch,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        heading.Children.Add(new TextBlock
        {
            Text = figure,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(swatch is null ? 0 : 6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ToBrush(colors.Foreground),
        });
        content.Children.Add(heading);

        content.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = MutedBrush(colors),
        });

        return content;
    }

    private static UIElement BuildTextContent(string text, WindowThemeColors colors) =>
        new TextBlock
        {
            Text = text,
            FontSize = 11,
            MaxWidth = 240,
            TextWrapping = TextWrapping.Wrap,
            Foreground = MutedBrush(colors),
        };

    private static ToolTip Card(UIElement content, WindowThemeColors colors) => new()
    {
        Content = Chrome(content, colors),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        HasDropShadow = false,
    };

    /// <summary>The replacement for the default WPF tooltip chrome — a pale rectangle with a
    /// hard border that has nothing to do with the rest of the panel. Every card, in either
    /// shape, is this same rounded border on the window's own palette.</summary>
    private static Border Chrome(UIElement content, WindowThemeColors colors) => new()
    {
        Background = ToBrush(colors.Background),
        BorderBrush = ToBrush(colors.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(9, 7, 9, 7),
        Child = content,
        Effect = new DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 1,
            Direction = 270,
            Opacity = 0.22,
            Color = Colors.Black,
        },
    };

    /// <summary>A quieter reading of the foreground colour for a caption/caveat line. This skin
    /// has no separate "muted" theme colour yet (<see cref="WindowThemeColors"/> carries only
    /// background/foreground/border) — widening that shared record is its own decision, out of
    /// this slice's scope, so the caption dims the same foreground brush instead.</summary>
    private static Brush MutedBrush(WindowThemeColors colors)
    {
        var brush = ToBrush(colors.Foreground).Clone();
        brush.Opacity = 0.65;
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush ToBrush(RgbColor color) => new(Color.FromRgb(color.R, color.G, color.B));
}
