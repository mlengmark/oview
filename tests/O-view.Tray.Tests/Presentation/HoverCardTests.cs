using System.Windows;
using System.Windows.Controls;
using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P2 (OVI-621): the <c>HoverTimingFixture</c> ADR-0008 D11b
/// requires, proving <see cref="HoverCard.ApplyTiming"/>'s three delays resolve on each element
/// that calls it, independently of its siblings — the exact bug the source found by measurement
/// (timing set on a container silently does not inherit to its children).
///
/// <para>Every WPF <see cref="DependencyObject"/> needs an STA thread to construct and touch
/// safely; xUnit does not run on one, so each test spins up its own, same as
/// <see cref="DetailWindowRenderProof"/> already does.</para>
/// </summary>
public sealed class HoverCardTests
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

    [Fact]
    public void ApplyTimingSetsAllThreeDelaysOnThreeDistinctElementsIndependently()
    {
        OnSta(() =>
        {
            var first = new TextBlock();
            var second = new TextBlock();
            var third = new TextBlock();
            var untouched = new TextBlock();

            HoverCard.ApplyTiming(first);
            HoverCard.ApplyTiming(second);
            HoverCard.ApplyTiming(third);

            foreach (var element in new[] { first, second, third })
            {
                Assert.Equal(HoverCard.InitialDelayMs, ToolTipService.GetInitialShowDelay(element));
                Assert.Equal(HoverCard.BetweenDelayMs, ToolTipService.GetBetweenShowDelay(element));
                Assert.Equal(HoverCard.DurationMs, ToolTipService.GetShowDuration(element));
            }

            // An element nobody called ApplyTiming on resolves the framework's own defaults, not
            // the shared timing — proving the three elements above carry it on themselves, not
            // through any shared container or static default this test accidentally changed.
            Assert.NotEqual(HoverCard.InitialDelayMs, ToolTipService.GetInitialShowDelay(untouched));
            Assert.NotEqual(HoverCard.BetweenDelayMs, ToolTipService.GetBetweenShowDelay(untouched));
            Assert.NotEqual(HoverCard.DurationMs, ToolTipService.GetShowDuration(untouched));
        });
    }

    [Fact]
    public void SettingTimingOnAContainerDoesNotReachAChildElement()
    {
        // The exact defect the source measured (docs/ui-spec.md §I in the source repo): timing
        // applied once to a parent silently does not flow down to its children through
        // ToolTipService's own property-value inheritance.
        OnSta(() =>
        {
            var container = new StackPanel();
            var child = new TextBlock();
            container.Children.Add(child);

            HoverCard.ApplyTiming(container);

            Assert.NotEqual(HoverCard.InitialDelayMs, ToolTipService.GetInitialShowDelay(child));
            Assert.NotEqual(HoverCard.BetweenDelayMs, ToolTipService.GetBetweenShowDelay(child));
            Assert.NotEqual(HoverCard.DurationMs, ToolTipService.GetShowDuration(child));
        });
    }

    [Fact]
    public void FigureCardCarriesTheFigureAndCaptionText()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);

            var card = HoverCard.Figure("47%", "session · resets 16:32", colors);

            Assert.NotNull(card.Content);
        });
    }

    [Fact]
    public void TextCardCarriesTheSentence()
    {
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Dark);

            var card = HoverCard.Text("Local estimate — not a vendor total.", colors);

            Assert.NotNull(card.Content);
        });
    }

    [Fact]
    public void CardHasNoSystemChrome()
    {
        // "No bare system tooltips anywhere in the panel" (OVI-621) — the default ToolTip chrome
        // is collapsed to nothing so only the styled border inside paints.
        OnSta(() =>
        {
            var colors = WindowThemePalette.Resolve(ThemePreference.Light);

            var card = HoverCard.Text("x", colors);

            Assert.Equal(new Thickness(0), card.BorderThickness);
            Assert.False(card.HasDropShadow);
        });
    }
}
