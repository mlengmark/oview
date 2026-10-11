using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OView.App;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook for this skin (OVI-598, slice P0), the Linux counterpart of
/// <c>O-view.Tray</c>'s own <c>DetailWindowRenderProof</c> — independently implemented, not
/// shared (D1): renders <see cref="DetailWindowContentBuilder"/>'s output to an offscreen PNG, in
/// a given theme, without ever showing <see cref="DetailWindow"/> itself. Every later parity
/// slice (P1-P25) reuses <see cref="RenderToFile"/> to prove the state it touches rendered
/// correctly in both themes; this slice only proves the mechanism works, for the states already
/// pinned in <c>DetailWindowFixtures</c> (ADR-0003).
///
/// <para>Builds its own small visual tree rather than reusing <see cref="DetailWindow.BuildLayout"/>
/// (a private method bound to that window's own instance fields) — duplication inside this one
/// skin, never shared across skins (D1). This tree is deliberately minimal: it exists to prove
/// that every content line renders somewhere, not to be the shipped layout.</para>
/// </summary>
public static class DetailWindowRenderProof
{
    public const double Width = DetailWindow.DefaultWidth;
    public const double Height = DetailWindow.DefaultHeight;

    private static readonly object InitLock = new();
    private static bool _platformInitialized;

    public static void RenderToFile(
        UsageDetail detail, ThemePreference theme, string filePath, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        bool showTokenKindBreakdown = false, bool flipWindowTiles = false)
    {
        File.WriteAllBytes(filePath, Render(detail, theme, utcNow, displayZone, showTokenKindBreakdown, flipWindowTiles));
    }

    /// <summary>
    /// ADR-0008 slicing-table slice P2 (OVI-621), the Linux counterpart of
    /// <c>O-view.Tray</c>'s own <c>RenderHoverCardsToFile</c> — independently implemented, not
    /// shared (D1): captures the hovered state — both <see cref="HoverCard"/> shapes, stacked —
    /// in a given theme, so review can compare it against the Windows render for the same
    /// fixture even though this skin's own timing cannot show the 20 s cap (see
    /// <see cref="HoverCard"/>'s remarks and waiver candidate W1).
    /// </summary>
    public static void RenderHoverCardsToFile(ThemePreference theme, string filePath)
    {
        File.WriteAllBytes(filePath, RenderHoverCards(theme));
    }

    public static byte[] RenderHoverCards(ThemePreference theme)
    {
        EnsurePlatformInitialized();

        var colors = LinuxWindowThemePalette.Resolve(theme);

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(HoverCard.BuildFigureCard("47%", "session · resets 16:32", colors));
        stack.Children.Add(new Border { Height = 16 });
        stack.Children.Add(HoverCard.BuildTextCard("Local estimate — based on parsed transcripts, not vendor totals.", colors));

        var root = new Border
        {
            Background = ToBrush(colors.Background),
            Child = stack,
        };

        root.Measure(Size.Infinity);
        root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)root.DesiredSize.Width), Math.Max(1, (int)root.DesiredSize.Height)));
        bitmap.Render(root);

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    public static byte[] Render(
        UsageDetail detail, ThemePreference theme, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        bool showTokenKindBreakdown = false, bool flipWindowTiles = false)
    {
        EnsurePlatformInitialized();

        var content = DetailWindowContentBuilder.Build(detail, utcNow, displayZone);
        var colors = LinuxWindowThemePalette.Resolve(theme);
        var visual = BuildVisual(content, colors, showTokenKindBreakdown, flipWindowTiles);

        // The live window scrolls past its own fixed Height (DetailWindow wraps its content in a
        // ScrollViewer) — measuring this proof at that same fixed Height silently clipped every
        // section below the scroll fold (the graph, gridlines, the model list) out of the PNG
        // while still reporting a non-empty file (OVI-686: discovered while proving this slice's
        // own gridlines, which the fixed-height render never showed at all). Height grows to fit
        // the real content instead, so the proof actually proves what later slices attach it for.
        visual.Measure(new Size(Width, double.PositiveInfinity));
        var height = Math.Max(Height, visual.DesiredSize.Height);
        visual.Arrange(new Rect(0, 0, Width, height));

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Width, (int)height));
        bitmap.Render(visual);

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    /// <summary>
    /// Avalonia's render interface (needed by <see cref="RenderTargetBitmap"/>) is registered by
    /// <see cref="AppBuilder"/>, not by constructing a <see cref="Control"/> directly — the same
    /// reason <see cref="App"/>'s own remarks give for why <c>DetailWindow</c> waits for
    /// <c>OnFrameworkInitializationCompleted</c> rather than being built in <c>Program.cs</c>.
    /// <c>SetupWithoutStarting</c> registers that platform without creating any window or
    /// entering a message loop; it may only run once per process, so this guards it with the
    /// same one-time pattern <c>Program.Main</c>'s own composition never needed (it calls
    /// <c>StartWithClassicDesktopLifetime</c> exactly once already).
    /// </summary>
    private static void EnsurePlatformInitialized()
    {
        if (_platformInitialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_platformInitialized)
            {
                return;
            }

            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            _platformInitialized = true;
        }
    }

    /// <summary>The usage bars' track width (gate G7 parity slice P9) — matches
    /// <see cref="DetailWindow"/>'s own constant so the render proof shows the same proportions
    /// the real window would.</summary>
    private const double BarTrackWidth = 350;
    private const double BarTrackHeight = 8;

    /// <summary>Matches <see cref="DetailWindow"/>'s own graph constants (gate G7 parity slice
    /// P13) so the render proof shows the same proportions the real window would.</summary>
    private const double GraphWidth = BarTrackWidth;
    private const double GraphBarAreaHeight = 52;
    private const double GraphLabelAreaHeight = 34;
    private const double GraphHeight = GraphBarAreaHeight + GraphLabelAreaHeight;

    /// <summary>
    /// Renders the 31-day graph (ADR-0008 D10b, gate G7 parity slices P13/P14), mirroring
    /// <see cref="DetailWindow.BuildGraph"/> — hand-duplicated, not shared, per this class's own
    /// remarks: one bar per <see cref="UsageGraphColumn"/>, coloured by its own
    /// <see cref="UsageGraphColumn.Intensity"/>, a measured, centred vertical date label under
    /// every column (blank or not), and the amber dotted gridlines drawn last, on top.
    /// </summary>
    private static Canvas BuildGraph(IReadOnlyList<UsageGraphColumn> columns, IReadOnlyList<UsageGraphGridline> gridlines, LinuxWindowThemeColors colors)
    {
        var canvas = new Canvas { Width = GraphWidth, Height = GraphHeight };

        if (columns.Count == 0)
        {
            return canvas;
        }

        var col = GraphWidth / columns.Count;
        var labelBrush = ToBrush(colors.Foreground);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var x = i * col;
            var centreX = UsageGraphLabelGeometry.ColumnCentre(x, col);

            if (!column.IsBlank)
            {
                var barWidth = Math.Max(2, col - 2);
                var height = Math.Max(2, GraphBarAreaHeight * column.HeightFraction);
                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = height,
                    Fill = ToBrush(LinuxWindowThemePalette.GraphColor(column.Intensity)),
                    RadiusX = 1,
                    RadiusY = 1,
                };
                Canvas.SetLeft(bar, centreX - barWidth / 2);
                Canvas.SetTop(bar, GraphBarAreaHeight - height);
                canvas.Children.Add(bar);
            }

            var label = new TextBlock
            {
                Text = column.DateLabel,
                FontSize = 8,
                Foreground = labelBrush,
                RenderTransform = new RotateTransform(90),
                Opacity = column.IsBlank ? 0.5 : 1.0,
            };
            canvas.Children.Add(label);
            label.Measure(Size.Infinity);
            var (inkLeft, inkTop, inkWidth, _) = UsageGraphLabelGeometry.RotatedInkBounds(
                label.DesiredSize.Width, label.DesiredSize.Height);
            Canvas.SetLeft(label, UsageGraphLabelGeometry.CentredLeft(centreX, inkLeft, inkWidth));
            Canvas.SetTop(label, UsageGraphLabelGeometry.Top(GraphBarAreaHeight + 6, inkTop));
        }

        var gridlineBrush = ToBrush(LinuxWindowThemePalette.BandColor(UsageBarBand.Amber));
        foreach (var gridline in gridlines)
        {
            var x = gridline.ColumnIndex * col + gridline.FractionWithinColumn * col;
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, GraphBarAreaHeight),
                Stroke = gridlineBrush,
                StrokeThickness = 1,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 },
            };
            canvas.Children.Add(line);
        }

        return canvas;
    }

    private static Border BuildVisual(DetailWindowContent content, LinuxWindowThemeColors colors, bool showTokenKindBreakdown, bool flipWindowTiles)
    {
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(BuildHeader(content, colors));
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8), Background = ToBrush(colors.Border) });
        if (!string.IsNullOrEmpty(content.OffPlanBannerTitle))
        {
            stack.Children.Add(BuildOffPlanBanner(content, colors));
        }

        stack.Children.Add(Line(content.SessionLine, colors));
        stack.Children.Add(BuildBar(content.SessionBarFraction, content.SessionBarBand, colors));
        if (content.WeeklyState != WeeklyBarState.Hidden)
        {
            stack.Children.Add(Line(content.WeeklyLine, colors));
            stack.Children.Add(BuildBar(content.WeeklyBarFraction, content.WeeklyBarBand, colors));
        }

        if (!string.IsNullOrEmpty(content.ExtraUsageLine))
        {
            stack.Children.Add(Line(content.ExtraUsageLine, colors));
        }

        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8), Background = ToBrush(colors.Border) });
        stack.Children.Add(BuildStatisticsTilesGrid(content.StatisticsTiles, colors, flipWindowTiles));
        stack.Children.Add(Line(content.CoverageCaption, colors));
        if (!string.IsNullOrEmpty(content.Caveat))
        {
            stack.Children.Add(Line(content.Caveat, colors));
        }

        stack.Children.Add(BuildTokenKindSection(content, colors, showTokenKindBreakdown));
        stack.Children.Add(BuildGraph(content.GraphColumns, content.GraphGridlines, colors));
        stack.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 10) });
        stack.Children.Add(BuildOffPlanUsageSection(content, colors));

        var modelSection = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        modelSection.Children.Add(Line("By model", colors));
        if (!string.IsNullOrEmpty(content.ModelSectionNote))
        {
            modelSection.Children.Add(Line(content.ModelSectionNote, colors));
        }

        foreach (var row in content.ModelRows)
        {
            modelSection.Children.Add(Line(ModelRowText(row), colors));
        }

        stack.Children.Add(modelSection);

        return new Border
        {
            Width = Width,
            // No explicit Height: an explicit Height on this root always wins Measure over its
            // content's natural size, which is exactly what silently clipped every section below
            // the window's own nominal height (see Render's own remarks).
            MinHeight = Height,
            Background = ToBrush(colors.Background),
            BorderBrush = ToBrush(colors.Border),
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    /// <summary>
    /// The off-plan banner (gate G7 parity slice P15), mirroring <see cref="DetailWindow"/>'s own
    /// live banner — independently implemented, not shared (D1): an amber-bordered, amber-tinted
    /// box above the usage bars carrying the heading, the relayed detail, and the link to
    /// Claude's own usage settings. Only ever built when
    /// <see cref="DetailWindowContent.OffPlanBannerTitle"/> is non-empty, same as the live
    /// window's visibility rule.
    /// </summary>
    private static Border BuildOffPlanBanner(DetailWindowContent content, LinuxWindowThemeColors colors)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = content.OffPlanBannerTitle,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ToBrush(colors.Foreground),
        });

        if (!string.IsNullOrEmpty(content.OffPlanBannerDetail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = content.OffPlanBannerDetail,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = ToBrush(colors.Foreground),
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = PanelTextFormatter.UsageSettingsLinkLabel,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            TextDecorations = Avalonia.Media.TextDecorations.Underline,
            Foreground = ToBrush(colors.Accent),
        });

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = ToBrush(LinuxWindowThemePalette.BandColors.Amber),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8),
            Background = ToBrush(colors.BannerBackground),
            Child = stack,
        };
    }

    /// <summary>
    /// The standing off-plan usage section (gate G7 parity slice P17, OVI-702), mirroring
    /// <see cref="DetailWindow.BuildOffPlanUsageSection"/> — hand-duplicated, not shared, per this
    /// class's own remarks: always rendered, unlike <see cref="BuildOffPlanBanner"/>'s live
    /// banner, which only appears when <see cref="DetailWindowContent.OffPlanBannerTitle"/> is
    /// non-empty.
    /// </summary>
    private static StackPanel BuildOffPlanUsageSection(DetailWindowContent content, LinuxWindowThemeColors colors)
    {
        var section = new StackPanel();
        section.Children.Add(new TextBlock { Text = content.OffPlanUsageTitle, FontWeight = FontWeight.SemiBold, Foreground = ToBrush(colors.Foreground) });
        section.Children.Add(new TextBlock { Text = content.OffPlanUsageValue, FontSize = 17, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 2, 0, 0), Foreground = ToBrush(colors.Foreground) });
        section.Children.Add(Line(content.OffPlanUsageCaption, colors));
        section.Children.Add(Line(content.CoverageCaption, colors));

        return section;
    }

    /// <summary>The bar track + proportional fill (ADR-0008 D10b, gate G7 parity slice P9):
    /// the band colour is <see cref="LinuxWindowThemePalette.BandColor"/>, the width is
    /// <paramref name="fraction"/> of <see cref="BarTrackWidth"/> — the same computation
    /// <see cref="DetailWindow.ShowDetail"/> does against its own live track.</summary>
    private static Border BuildBar(double fraction, UsageBarBand band, LinuxWindowThemeColors colors) => new()
    {
        Width = BarTrackWidth,
        Height = BarTrackHeight,
        CornerRadius = new CornerRadius(BarTrackHeight / 2),
        Margin = new Thickness(0, 4, 0, 2),
        ClipToBounds = true,
        Background = ToBrush(colors.Border),
        Child = new Border
        {
            Width = BarTrackWidth * fraction,
            Height = BarTrackHeight,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(BarTrackHeight / 2),
            Background = ToBrush(LinuxWindowThemePalette.BandColor(band)),
        },
    };

    /// <summary>
    /// The token-kind bars section (gate G7 parity slice P12), mirroring
    /// <see cref="DetailWindow.BuildTokenKindSection"/> — hand-duplicated, not shared, per this
    /// class's own remarks. <paramref name="showTokenKindBreakdown"/> captures the view switch's
    /// two states, the same way <c>flipWindowTiles</c> captures slice P10's own flip.
    /// </summary>
    private static StackPanel BuildTokenKindSection(DetailWindowContent content, LinuxWindowThemeColors colors, bool showTokenKindBreakdown)
    {
        var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        section.Children.Add(new TextBlock { Text = "Token usage by kind", FontWeight = FontWeight.Bold, Foreground = ToBrush(colors.Foreground) });
        if (!string.IsNullOrEmpty(content.TokenKindSectionNote))
        {
            section.Children.Add(Line(content.TokenKindSectionNote, colors));
        }

        section.Children.Add(Line(content.TokenKindBarToday.Label + ": " + content.TokenKindBarToday.Total, colors));
        section.Children.Add(BuildTokenKindBar(content.TokenKindBarToday, colors));
        section.Children.Add(Line(content.TokenKindBarWindow31d.Label + ": " + content.TokenKindBarWindow31d.Total, colors));
        section.Children.Add(BuildTokenKindBar(content.TokenKindBarWindow31d, colors));

        if (showTokenKindBreakdown)
        {
            foreach (var row in content.TokenKindBreakdownRows)
            {
                section.Children.Add(Line(TokenKindBreakdownRowText(row), colors));
            }
        }

        return section;
    }

    /// <summary>The segmented bar (gate G7 parity slice P12): each kind's own share, in a fixed
    /// colour (<see cref="LinuxWindowThemePalette.TokenKindColor"/>) — a proportion of
    /// <see cref="TokenKindBar.Total"/> already computed by <see cref="TokenKindBarFormatter"/>,
    /// never re-summed here.</summary>
    private static Border BuildTokenKindBar(TokenKindBar bar, LinuxWindowThemeColors colors)
    {
        var segments = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var segment in bar.Segments)
        {
            segments.Children.Add(new Border
            {
                Width = BarTrackWidth * segment.Fraction,
                Height = BarTrackHeight,
                Background = ToBrush(LinuxWindowThemePalette.TokenKindColor(segment.Kind)),
            });
        }

        return new Border
        {
            Width = BarTrackWidth,
            Height = BarTrackHeight,
            CornerRadius = new CornerRadius(BarTrackHeight / 2),
            Margin = new Thickness(0, 4, 0, 2),
            ClipToBounds = true,
            Background = ToBrush(colors.Border),
            Child = segments,
        };
    }

    private static string TokenKindBreakdownRowText(TokenKindBreakdownRow row) =>
        $"{row.Label} — today {row.TodayTokens} tokens ({row.TodayValue}) · 31 days {row.Window31dTokens} tokens ({row.Window31dValue})";

    /// <summary>
    /// The header (ADR-0008 D2 §B, gate G7 parity slice P8), mirroring
    /// <see cref="DetailWindow.BuildHeader"/> — hand-duplicated, not shared, per this class's
    /// own remarks: the title and freshness line at the left, the account block (display name,
    /// email, tier badge) at the right.
    /// </summary>
    private static Grid BuildHeader(DetailWindowContent content, LinuxWindowThemeColors colors)
    {
        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = "O-view", FontWeight = FontWeight.Bold, FontSize = 14, Foreground = ToBrush(colors.Foreground) });
        left.Children.Add(Line(content.Freshness, colors));

        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(Line(content.AccountDisplayName, colors));
        right.Children.Add(Line(content.AccountEmail, colors));

        if (!string.IsNullOrEmpty(content.AccountTierBadge))
        {
            right.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = ToBrush(colors.Accent),
                Child = new TextBlock
                {
                    Text = content.AccountTierBadge,
                    FontSize = 10,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                },
            });
        }

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(right, 1);
        header.Children.Add(left);
        header.Children.Add(right);

        return header;
    }

    /// <summary>
    /// Mirrors <see cref="StatisticsTileView"/>'s own front/back faces (gate G7 parity slice
    /// P10), minimally — a static picture, not an interactive control — so the render proof
    /// shows exactly what <see cref="DetailWindow.ShowDetail"/> would paint for each tile: its
    /// own figure, or (when <paramref name="flipWindowTiles"/> asks for it and the tile actually
    /// has something to show) its per-model stacked bar. A tile with nothing to flip to never
    /// shows the affordance glyph, matching the live control's own disabled-tile rule.
    /// </summary>
    private static Control BuildStatisticsTilesGrid(IReadOnlyList<StatisticsTile> tiles, LinuxWindowThemeColors colors, bool flipWindowTiles)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        for (var i = 0; i < tiles.Count; i++)
        {
            var tileVisual = BuildStatisticsTile(tiles[i], colors, flipWindowTiles && tiles[i].CanFlip);
            Grid.SetColumn(tileVisual, i % 2);
            Grid.SetRow(tileVisual, i / 2);
            grid.Children.Add(tileVisual);
        }

        return grid;
    }

    private static Border BuildStatisticsTile(StatisticsTile tile, LinuxWindowThemeColors colors, bool flipped)
    {
        var faces = new Grid();

        if (!flipped)
        {
            var front = new StackPanel();
            front.Children.Add(new TextBlock { Text = tile.Label, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = ToBrush(colors.Foreground) });
            front.Children.Add(new TextBlock { Text = tile.Value, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 2, 0, 0), Foreground = ToBrush(colors.Foreground) });
            faces.Children.Add(front);
        }
        else
        {
            var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            foreach (var segment in tile.Breakdown)
            {
                bar.Children.Add(new Border
                {
                    Width = Math.Max(2.0, 150 * segment.Fraction),
                    Height = 16,
                    Margin = new Thickness(0, 0, 1, 0),
                    Background = ToBrush(colors.Accent),
                });
            }

            faces.Children.Add(bar);
        }

        var root = new Grid();
        root.Children.Add(new Border { Padding = new Thickness(10, 8, 10, 8), Child = faces });
        if (tile.CanFlip)
        {
            root.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Background = ToBrush(colors.Accent),
            });
        }

        return new Border
        {
            Width = StatisticsTileView.TileWidth,
            Height = StatisticsTileView.TileHeight,
            Margin = new Thickness(0, 0, 4, 4),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = ToBrush(colors.Border),
            Background = ToBrush(colors.Background),
            Child = root,
        };
    }

    private static TextBlock Line(string text, LinuxWindowThemeColors colors) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 2),
        Foreground = ToBrush(colors.Foreground),
    };

    private static string ModelRowText(DetailWindowModelRow row) =>
        $"{row.ModelId} — {row.Requests} req · in {row.InputTokens} · out {row.OutputTokens} " +
        $"· cache w {row.CacheWriteTokens} · cache r {row.CacheReadTokens} · {row.EstimatedSpend}";

    private static SolidColorBrush ToBrush(LinuxRgbColor color) => new(Color.FromRgb(color.R, color.G, color.B));
}
