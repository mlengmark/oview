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
/// <para><b>Waiver W1 (accepted by the board on OVI-593, 2026-10-10): only the 400 ms initial
/// delay is a cross-platform guarantee. The other two timings stay Windows-only; this skin uses
/// whatever the toolkit itself offers, not a forced match to the Windows numbers.</b> Reflecting
/// <c>Avalonia.Controls.dll</c> 12.1.3 (confirmed by a unit test against the real default) shows
/// <see cref="ToolTip.BetweenShowDelayProperty"/> exists and defaults, unset, to <b>100 ms</b> —
/// not the Windows skin's 3000 ms — so <see cref="ApplyTiming"/> leaves it unset rather than
/// overriding it to a number this toolkit does not naturally produce. No
/// <c>ShowDuration</c>/<c>GetShowDuration</c>/<c>SetShowDuration</c> member exists anywhere in the
/// assembly (CONFIRMED 2026-10-10), and Avalonia tooltips close when the pointer leaves the owning
/// element instead, with no exposed hook to force an earlier or later close. A custom
/// popup-and-timer reimplementation could fake either Windows number, but W1 asks for the real
/// toolkit shortfall to be recorded rather than have a skin quietly diverge from what it actually
/// does — this slice does not build that reimplementation. See <see cref="ApplyTiming"/> and
/// <c>docs/adr/0002-cross-platform-capability-matrix.md</c>'s "Hover card timing" row.</para>
///
/// <para>This slice only builds the primitive and proves its timing — it does not yet wire any
/// of the detail window's own elements (sections B-H) to use it; later slices (P8-P19) do that as
/// each section lands.</para>
/// </summary>
internal static class HoverCard
{
    /// <summary>400 ms — matches the Windows skin's own constant and is applied on both
    /// platforms per waiver W1; both resolve independently (D1), not from a shared source.</summary>
    public const int InitialDelayMs = 400;

    /// <summary>
    /// 3000 ms — the Windows skin's own constant, carried here only for the caller
    /// (<c>HoverTimingFixtureTests</c>, <c>HoverCardTests</c>) that needs to state what Windows
    /// achieves and this skin deliberately does not match, per waiver W1. Not applied by
    /// <see cref="ApplyTiming"/>.
    /// </summary>
    public const int BetweenDelayMs = 3000;

    /// <summary>
    /// 100 ms — this toolkit's own unset default for <see cref="ToolTip.BetweenShowDelayProperty"/>
    /// (CONFIRMED by reflecting <c>Avalonia.Controls.dll</c> 12.1.3 and by a unit test against the
    /// real default, 2026-10-10). This is what Linux actually shows between tips, per waiver W1 —
    /// "Linux uses what the toolkit offers" — rather than the Windows 3000 ms.
    /// </summary>
    public const int LinuxBetweenShowDelayDefaultMs = 100;

    /// <summary>
    /// 20000 ms — the design value, carried here for the one caller
    /// (<c>HoverTimingFixtureTests</c>) that needs to state what the Windows skin actually
    /// achieves and this skin does not (yet; see the type remarks and W1). Not applied by
    /// <see cref="ApplyTiming"/>, because no Avalonia property exists to apply it to.
    /// </summary>
    public const int DurationMs = 20_000;

    /// <summary>
    /// Applies only the timing waiver W1 actually guarantees on this platform: the 400 ms
    /// initial delay, per element — timing set on a container and relied on to inherit is
    /// exactly the bug the source found on Windows (see
    /// <c>O-view.Tray.Presentation.HoverCard</c>'s remarks), so the same per-element discipline
    /// applies here too. The between-show delay is deliberately left unset, so it resolves to
    /// this toolkit's own default (<see cref="LinuxBetweenShowDelayDefaultMs"/>) rather than
    /// being forced to the Windows number — see the type remarks.
    /// </summary>
    public static void ApplyTiming(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ToolTip.SetShowDelay(element, InitialDelayMs);
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
