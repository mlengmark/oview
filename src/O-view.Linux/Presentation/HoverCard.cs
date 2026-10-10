using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 D11a/slicing-table slice P2 (OVI-621, gate G7 parity), the Linux counterpart of
/// <c>O-view.Tray</c>'s own <c>HoverCard</c> — independently implemented, not shared (D1): the one
/// styled hover card used everywhere in the detail window, so no element ever falls back to the
/// plain system tooltip. Same two shapes as the Windows skin, worded identically because they
/// carry no user-facing copy of their own (the caller supplies every string); the chrome and the
/// timing are each this skin's own, following Avalonia's own tooltip model rather than WPF's.
///
/// <para><b>The 20 s show duration is a named platform limit, not ported (waiver candidate W1,
/// <c>docs/parity/g7-detail-window-parity.md</c>).</b> Avalonia's <see cref="ToolTip"/> exposes
/// <see cref="ToolTip.ShowDelayProperty"/> and <see cref="ToolTip.BetweenShowDelayProperty"/> —
/// both applied below — but no show-duration equivalent (CONFIRMED by reading
/// <c>Avalonia.Controls.dll</c>'s own attached-property surface: no <c>ShowDuration</c>,
/// <c>GetShowDuration</c> or <c>SetShowDuration</c> member exists anywhere in it, 2026-10-10).
/// Avalonia tooltips instead close when the pointer leaves the owning element, with no exposed
/// hook to force an earlier or later close. A custom popup-and-timer reimplementation could fake
/// the 20 s cap, but W1 asks the board first rather than have a skin quietly diverge from what
/// it actually says it does — this slice does not build that reimplementation. See
/// <see cref="ApplyTiming"/>.</para>
///
/// <para>This slice only builds the primitive and proves its timing — it does not yet wire any
/// of the detail window's own elements (sections B-H) to use it; later slices (P8-P19) do that as
/// each section lands.</para>
/// </summary>
internal static class HoverCard
{
    /// <summary>400 ms — matches the Windows skin's own constant; both resolve independently
    /// (D1), not from a shared source, and happen to agree because the design calls for one
    /// delay regardless of platform.</summary>
    public const int InitialDelayMs = 400;

    /// <summary>3000 ms — matches the Windows skin's own constant; see <see cref="InitialDelayMs"/>.</summary>
    public const int BetweenDelayMs = 3000;

    /// <summary>
    /// 20000 ms — the design value, carried here for the one caller
    /// (<c>HoverTimingFixtureTests</c>) that needs to state what the Windows skin actually
    /// achieves and this skin does not (yet; see the type remarks and W1). Not applied by
    /// <see cref="ApplyTiming"/>, because no Avalonia property exists to apply it to.
    /// </summary>
    public const int DurationMs = 20_000;

    /// <summary>
    /// Applies the shared delays this toolkit actually exposes. Must be called on each element
    /// that owns a card — timing set on a container and relied on to inherit is exactly the bug
    /// the source found on Windows (see <c>O-view.Tray.Presentation.HoverCard</c>'s remarks);
    /// nothing here assumes Avalonia's attached properties behave any differently, so the same
    /// per-element discipline applies.
    /// </summary>
    public static void ApplyTiming(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ToolTip.SetShowDelay(element, InitialDelayMs);
        ToolTip.SetBetweenShowDelay(element, BetweenDelayMs);
    }

    /// <summary>
    /// A card leading with a figure, with a quieter identifying line beneath it, and an optional
    /// colour swatch tying it to the mark being pointed at. Sets the card as <paramref name="owner"/>'s
    /// tip and applies the shared timing in one call — Avalonia attaches a tooltip's content
    /// directly to the element that shows it, unlike WPF's separate floating <c>ToolTip</c> object.
    /// </summary>
    public static void Figure(Control owner, string figure, string caption, LinuxWindowThemeColors colors, IBrush? swatch = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        ToolTip.SetTip(owner, Chrome(BuildFigureContent(figure, caption, colors, swatch), colors));
        ApplyTiming(owner);
    }

    /// <summary>A card carrying a sentence — caveats, explanations, and anything unmeasured. See
    /// <see cref="Figure"/>'s remarks for why this sets <paramref name="owner"/> directly.</summary>
    public static void Text(Control owner, string text, LinuxWindowThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(owner);

        ToolTip.SetTip(owner, Chrome(BuildTextContent(text, colors), colors));
        ApplyTiming(owner);
    }

    /// <summary>
    /// Builds the figure card's visual content with no <see cref="ToolTip"/> attachment, so the
    /// render-proof hook (<see cref="DetailWindowRenderProof"/>, P0) can capture exactly what a
    /// hovered element looks like without a live pointer or a running compositor.
    /// </summary>
    internal static Border BuildFigureCard(string figure, string caption, LinuxWindowThemeColors colors, IBrush? swatch = null) =>
        Chrome(BuildFigureContent(figure, caption, colors, swatch), colors);

    /// <summary>The text card's visual content with no <see cref="ToolTip"/> attachment — see
    /// <see cref="BuildFigureCard"/>'s remarks.</summary>
    internal static Border BuildTextCard(string text, LinuxWindowThemeColors colors) =>
        Chrome(BuildTextContent(text, colors), colors);

    private static Control BuildFigureContent(string figure, string caption, LinuxWindowThemeColors colors, IBrush? swatch)
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
            FontWeight = FontWeight.SemiBold,
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

    private static Control BuildTextContent(string text, LinuxWindowThemeColors colors) =>
        new TextBlock
        {
            Text = text,
            FontSize = 11,
            MaxWidth = 240,
            TextWrapping = TextWrapping.Wrap,
            Foreground = MutedBrush(colors),
        };

    /// <summary>The replacement for Avalonia's default tooltip chrome. Every card, in either
    /// shape, is this same rounded border on the window's own palette.</summary>
    private static Border Chrome(Control content, LinuxWindowThemeColors colors) => new()
    {
        Background = ToBrush(colors.Background),
        BorderBrush = ToBrush(colors.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(9, 7, 9, 7),
        Child = content,
    };

    /// <summary>A quieter reading of the foreground colour for a caption/caveat line. This skin
    /// has no separate "muted" theme colour yet (<see cref="LinuxWindowThemeColors"/> carries only
    /// background/foreground/border) — widening that shared record is its own decision, out of
    /// this slice's scope, so the caption dims the same foreground brush instead.</summary>
    private static IBrush MutedBrush(LinuxWindowThemeColors colors)
    {
        var brush = new SolidColorBrush(ToColor(colors.Foreground), 0.65);
        return brush;
    }

    private static SolidColorBrush ToBrush(LinuxRgbColor color) => new(ToColor(color));

    private static Color ToColor(LinuxRgbColor color) => Color.FromRgb(color.R, color.G, color.B);
}
