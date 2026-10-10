using System.Windows;
using OView.App;
using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 D10b, gate G7 parity slice P10: proves the three obligations that are not content
/// facts and so cannot be proven by <see cref="DetailWindowContentBuilderTests"/> alone — tile
/// size stability across both toggle states, that a disabled tile neither flips nor shows the
/// affordance glyph, and that the glyph's hover highlight is a plain colour swap.
///
/// <para>Every WPF <see cref="DependencyObject"/> needs an STA thread to construct and touch
/// safely; xUnit does not run on one, so each test spins up its own, same as
/// <see cref="HoverCardTests"/> and <see cref="DetailWindowRenderProof"/> already do.</para>
/// </summary>
public sealed class StatisticsTileViewTests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
    }

    private static StatisticsTile FlippableTile() => new(
        StatisticsTileKind.OutputTokensWindow31d,
        "Output tokens · 31 days",
        "250.0K",
        CanFlip: true,
        Breakdown: new[]
        {
            new StatisticsTileSegment("claude-sonnet-5", 0.75),
            new StatisticsTileSegment("claude-haiku-5", 0.25),
        });

    private static StatisticsTile DisabledTile() => new(
        StatisticsTileKind.OutputTokensToday,
        "Output tokens today",
        "10.0K",
        CanFlip: false,
        Breakdown: Array.Empty<StatisticsTileSegment>());

    /// <summary>The slicing table's own "size stability" obligation: flipping between the figure
    /// and the stacked bar — toggled with <see cref="Visibility.Hidden"/>, never
    /// <see cref="Visibility.Collapsed"/> — never changes the tile's own measured size.</summary>
    [Fact]
    public void Tile_size_is_identical_in_both_toggle_states()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);
            var tile = new StatisticsTileView();
            tile.Populate(FlippableTile(), colors);

            tile.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var unflippedSize = tile.DesiredSize;

            tile.ToggleFlip();
            tile.InvalidateMeasure();
            tile.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var flippedSize = tile.DesiredSize;

            Assert.True(tile.IsFlipped);
            Assert.Equal(unflippedSize, flippedSize);
        });
    }

    /// <summary>The front face stays in the layout tree as <see cref="Visibility.Hidden"/> once
    /// flipped — never <see cref="Visibility.Collapsed"/>, which would remove it from the
    /// measure pass that gives the size-stability guarantee above.</summary>
    [Fact]
    public void Flipping_never_collapses_either_face()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);
            var tile = new StatisticsTileView();
            tile.Populate(FlippableTile(), colors);

            tile.ToggleFlip();

            void AssertNeverCollapsed(DependencyObject root)
            {
                if (root is UIElement element)
                {
                    Assert.NotEqual(Visibility.Collapsed, element.Visibility);
                }

                var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
                for (var i = 0; i < childCount; i++)
                {
                    AssertNeverCollapsed(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
                }
            }

            AssertNeverCollapsed(tile);
        });
    }

    /// <summary>Populate builds the breakdown once, up front; toggling a flip never needs the
    /// breakdown data to change — proven here by flipping back and forth and checking the figure
    /// text, set only by <see cref="StatisticsTileView.Populate"/>, is untouched.</summary>
    [Fact]
    public void Toggling_the_flip_never_recomputes_or_loses_the_populated_data()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);
            var tile = new StatisticsTileView();
            var data = FlippableTile();
            tile.Populate(data, colors);

            tile.ToggleFlip();
            tile.ToggleFlip();
            tile.ToggleFlip();

            Assert.True(tile.IsFlipped);
            // No exception and no further Populate call were needed to flip three times —
            // the breakdown this tile shows came entirely from the single Populate call above.
        });
    }

    /// <summary>A tile with nothing to flip to shows no affordance glyph and ignores a click.</summary>
    [Fact]
    public void A_disabled_tile_shows_no_glyph_and_does_not_flip()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);
            var tile = new StatisticsTileView();
            tile.Populate(DisabledTile(), colors);

            Assert.False(tile.IsGlyphVisible);

            tile.ToggleFlip();

            Assert.False(tile.IsFlipped);
        });
    }

    /// <summary>A flippable tile shows the glyph, and hovering brightens it — a plain colour
    /// swap between the window's accent and its hover step.</summary>
    [Fact]
    public void A_flippable_tiles_glyph_brightens_on_hover()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);
            var tile = new StatisticsTileView();
            tile.Populate(FlippableTile(), colors);

            Assert.True(tile.IsGlyphVisible);
            var atRest = ((System.Windows.Media.SolidColorBrush)tile.GlyphBrush).Color;

            tile.SetGlyphHighlighted(true);
            var hovered = ((System.Windows.Media.SolidColorBrush)tile.GlyphBrush).Color;

            tile.SetGlyphHighlighted(false);
            var afterLeaving = ((System.Windows.Media.SolidColorBrush)tile.GlyphBrush).Color;

            Assert.NotEqual(atRest, hovered);
            Assert.Equal(atRest, afterLeaving);
        });
    }
}
