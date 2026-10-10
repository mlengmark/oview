using System.IO;
using Avalonia;
using Avalonia.Controls;
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
        bool showTokenKindBreakdown = false)
    {
        File.WriteAllBytes(filePath, Render(detail, theme, utcNow, displayZone, showTokenKindBreakdown));
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
        bool showTokenKindBreakdown = false)
    {
        EnsurePlatformInitialized();

        var content = DetailWindowContentBuilder.Build(detail, utcNow, displayZone);
        var colors = LinuxWindowThemePalette.Resolve(theme);
        var visual = BuildVisual(content, colors, showTokenKindBreakdown);

        visual.Measure(new Size(Width, Height));
        visual.Arrange(new Rect(0, 0, Width, Height));

        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Width, (int)Height));
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

    private static Border BuildVisual(DetailWindowContent content, LinuxWindowThemeColors colors, bool showTokenKindBreakdown)
    {
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(BuildHeader(content, colors));
        stack.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8), Background = ToBrush(colors.Border) });
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
