using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OView.Tray.Presentation;

/// <summary>The visual tree <see cref="MenuFlyoutWindow"/> shows and
/// <see cref="MenuFlyoutRenderProof"/> renders offscreen — split out so the render proof never
/// constructs a real <see cref="Window"/>, the same shape <c>BrandedDialogLayout</c> already
/// established for the branded dialogs (gate G7 parity slice P24, OVI-718).</summary>
internal sealed class MenuFlyoutVisual
{
    public required Border Root { get; init; }
    public required TextBlock VersionText { get; init; }
    public required StackPanel Rows { get; init; }
}

/// <summary>
/// The branded tray menu flyout's chrome (gate G7 parity slice P21, OVI-738, ADR-0009): a
/// ~272 px card with a header (the brand mark, "O-view", and the running version) and a stack of
/// 34 px rows. <b>Chrome and positioning only</b> — the row list here is placeholder content; what
/// each row says and does is slice P23's job, and this type knows nothing about actions.
///
/// <para>Reuses <see cref="BrandedDialogChrome.BuildBrandMark"/> and <see cref="WindowThemeColors"/>
/// rather than inventing a second brand mark or a second palette — the issue that added this type
/// names both as the shared styling primitives to build from (P3's palette/theme work, P24's
/// branded-dialog chrome).</para>
/// </summary>
internal static class MenuFlyoutChrome
{
    /// <summary>"~272 px card" (the issue's own figure).</summary>
    public const double CardWidth = 272;

    /// <summary>"34 px rows" (the issue's own figure).</summary>
    public const double RowHeight = 34;

    public const double HeaderHeight = 44;

    /// <summary>The card's total height for a given row count — what
    /// <see cref="MenuFlyoutWindow"/> feeds into <see cref="MenuFlyoutPlacement.Compute"/> before
    /// the card has actually been measured, and what <see cref="MenuFlyoutRenderProof"/> uses to
    /// size its offscreen scene.</summary>
    public static double ContentHeight(int rowCount) => HeaderHeight + 1 + (rowCount * RowHeight);

    public static MenuFlyoutVisual Build(WindowThemeColors colors, string version, IReadOnlyList<string> rowLabels)
    {
        var brandRow = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        brandRow.Children.Add(BrandedDialogChrome.BuildBrandMark(colors.Accent));
        brandRow.Children.Add(new TextBlock
        {
            Text = "O-view",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ToBrush(colors.Foreground),
        });

        var versionText = new TextBlock
        {
            Text = version,
            FontSize = 10,
            Opacity = 0.7,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ToBrush(colors.Foreground),
        };

        var header = new Grid { Height = HeaderHeight };
        header.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 0), Child = brandRow });
        header.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 0), Child = versionText });

        var rows = new StackPanel();
        foreach (var label in rowLabels)
        {
            rows.Children.Add(new Border
            {
                Height = RowHeight,
                Padding = new Thickness(12, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = ToBrush(colors.Foreground),
                },
            });
        }

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 0), Background = ToBrush(colors.Border) });
        stack.Children.Add(rows);

        var root = new Border
        {
            Width = CardWidth,
            Background = ToBrush(colors.Background),
            BorderBrush = ToBrush(colors.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(BrandedDialogChrome.CardCornerRadius),
            Child = stack,
        };

        return new MenuFlyoutVisual { Root = root, VersionText = versionText, Rows = rows };
    }

    private static SolidColorBrush ToBrush(RgbColor color) => new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
}
