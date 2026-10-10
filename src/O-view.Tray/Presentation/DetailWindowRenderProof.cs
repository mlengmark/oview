using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OView.App;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook for this skin (OVI-598, slice P0): renders
/// <see cref="DetailWindowContentBuilder"/>'s output to an offscreen PNG, in a given theme,
/// without ever showing or activating <see cref="DetailWindow"/> itself. Every later parity
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

    public static void RenderToFile(
        UsageDetail detail, ThemePreference theme, string filePath, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        bool showTokenKindBreakdown = false)
    {
        var bytes = Render(detail, theme, utcNow, displayZone, showTokenKindBreakdown);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>
    /// ADR-0008 slicing-table slice P2 (OVI-621): captures the hovered state — both
    /// <see cref="HoverCard"/> shapes, stacked — in a given theme. A <see cref="ToolTip"/> cannot
    /// be given a parent (it throws), so it can never appear inside a screenshot of anything
    /// else; this renders the two cards' own content directly, the same reason the source's
    /// <c>--tile-samples</c> wrote its hover cards to a standalone file rather than inside the
    /// panel screenshot.
    /// </summary>
    public static void RenderHoverCardsToFile(ThemePreference theme, string filePath)
    {
        File.WriteAllBytes(filePath, RenderHoverCards(theme));
    }

    public static byte[] RenderHoverCards(ThemePreference theme)
    {
        byte[]? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = RenderHoverCardsOnStaThread(theme);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    private static byte[] RenderHoverCardsOnStaThread(ThemePreference theme)
    {
        var colors = WindowThemePalette.Resolve(theme);

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(HoverCard.BuildFigureCard("47%", "session · resets 16:32", colors));
        stack.Children.Add(new System.Windows.Controls.Border { Height = 16 });
        stack.Children.Add(HoverCard.BuildTextCard("Local estimate — based on parsed transcripts, not vendor totals.", colors));

        var root = new Border
        {
            Background = ToBrush(colors.Background),
            Child = stack,
        };

        root.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)root.DesiredSize.Width), Math.Max(1, (int)root.DesiredSize.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Every WPF type below <see cref="FrameworkElement"/> asserts it was created on an STA
    /// thread (not just <see cref="Window"/>) — xUnit does not run tests on one, so this spins up
    /// a dedicated STA thread for the whole build-measure-arrange-render sequence and joins it,
    /// rather than pushing that requirement onto every caller.
    /// </summary>
    public static byte[] Render(
        UsageDetail detail, ThemePreference theme, DateTimeOffset utcNow, TimeZoneInfo displayZone,
        bool showTokenKindBreakdown = false)
    {
        byte[]? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = RenderOnStaThread(detail, theme, utcNow, displayZone, showTokenKindBreakdown);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    private static byte[] RenderOnStaThread(
        UsageDetail detail, ThemePreference theme, DateTimeOffset utcNow, TimeZoneInfo displayZone, bool showTokenKindBreakdown)
    {
        var content = DetailWindowContentBuilder.Build(detail, utcNow, displayZone);
        var colors = WindowThemePalette.Resolve(theme);
        var visual = BuildVisual(content, colors, showTokenKindBreakdown);

        visual.Measure(new System.Windows.Size(Width, Height));
        visual.Arrange(new Rect(0, 0, Width, Height));

        var bitmap = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>The usage bars' track width (gate G7 parity slice P9) — matches
    /// <see cref="DetailWindow"/>'s own constant so the render proof shows the same proportions
    /// the real window would.</summary>
    private const double BarTrackWidth = 350;
    private const double BarTrackHeight = 8;

    private static Border BuildVisual(DetailWindowContent content, WindowThemeColors colors, bool showTokenKindBreakdown)
    {
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(BuildHeader(content, colors));
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
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

        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        stack.Children.Add(Line(content.TodayLine, colors));
        stack.Children.Add(Line(content.Window31dLine, colors));
        if (!string.IsNullOrEmpty(content.Caveat))
        {
            stack.Children.Add(Line(content.Caveat, colors));
        }

        stack.Children.Add(BuildTokenKindSection(content, colors, showTokenKindBreakdown));

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
            Height = Height,
            Background = ToBrush(colors.Background),
            BorderBrush = ToBrush(colors.Border),
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    /// <summary>The bar track + proportional fill (ADR-0008 D10b, gate G7 parity slice P9):
    /// the band colour is <see cref="WindowThemePalette.BandColor"/>, the width is
    /// <paramref name="fraction"/> of <see cref="BarTrackWidth"/> — the same computation
    /// <see cref="DetailWindow.ShowDetail"/> does against its own live track.</summary>
    private static Border BuildBar(double fraction, UsageBarBand band, WindowThemeColors colors) => new()
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
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(BarTrackHeight / 2),
            Background = ToBrush(WindowThemePalette.BandColor(band)),
        },
    };

    /// <summary>
    /// The token-kind bars section (gate G7 parity slice P12), mirroring
    /// <see cref="DetailWindow.BuildTokenKindSection"/> — hand-duplicated, not shared, per this
    /// class's own remarks. <paramref name="showTokenKindBreakdown"/> captures the view switch's
    /// two states, the same way <c>flipWindowTiles</c> captures slice P10's own flip.
    /// </summary>
    private static StackPanel BuildTokenKindSection(DetailWindowContent content, WindowThemeColors colors, bool showTokenKindBreakdown)
    {
        var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        section.Children.Add(new TextBlock { Text = "Token usage by kind", FontWeight = FontWeights.Bold, Foreground = ToBrush(colors.Foreground) });
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
    /// colour (<see cref="WindowThemePalette.TokenKindColor"/>) — a proportion of
    /// <see cref="TokenKindBar.Total"/> already computed by <see cref="TokenKindBarFormatter"/>,
    /// never re-summed here.</summary>
    private static Border BuildTokenKindBar(TokenKindBar bar, WindowThemeColors colors)
    {
        var segments = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var segment in bar.Segments)
        {
            segments.Children.Add(new Border
            {
                Width = BarTrackWidth * segment.Fraction,
                Height = BarTrackHeight,
                Background = ToBrush(WindowThemePalette.TokenKindColor(segment.Kind)),
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
    /// <see cref="DetailWindow.BuildHeader"/> — hand-duplicated, not shared, per this class's own
    /// remarks: the title and freshness line at the left, the account block (display name,
    /// email, tier badge) at the right.
    /// </summary>
    private static Grid BuildHeader(DetailWindowContent content, WindowThemeColors colors)
    {
        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = "O-view", FontWeight = FontWeights.Bold, FontSize = 14, Foreground = ToBrush(colors.Foreground) });
        left.Children.Add(Line(content.Freshness, colors));

        var right = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        right.Children.Add(Line(content.AccountDisplayName, colors));
        right.Children.Add(Line(content.AccountEmail, colors));

        if (!string.IsNullOrEmpty(content.AccountTierBadge))
        {
            right.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Background = ToBrush(colors.Accent),
                Child = new TextBlock
                {
                    Text = content.AccountTierBadge,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.White,
                },
            });
        }

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(right, 1);
        header.Children.Add(left);
        header.Children.Add(right);

        return header;
    }

    private static TextBlock Line(string text, WindowThemeColors colors) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 2),
        Foreground = ToBrush(colors.Foreground),
    };

    private static string ModelRowText(DetailWindowModelRow row) =>
        $"{row.ModelId} — {row.Requests} req · in {row.InputTokens} · out {row.OutputTokens} " +
        $"· cache w {row.CacheWriteTokens} · cache r {row.CacheReadTokens} · {row.EstimatedSpend}";

    private static SolidColorBrush ToBrush(RgbColor color) =>
        new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
}
