using Avalonia.Controls;
using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P2 (OVI-621), updated per waiver W1 (board-accepted on OVI-593,
/// 2026-10-10, applied here on OVI-665): this skin's own timing surface guarantees only the
/// 400 ms initial delay (<see cref="ToolTip.ShowDelayProperty"/>) on each of several distinct
/// elements independently, the same per-element discipline the Windows skin's own test proves.
/// The between-show delay and the 20 s show duration are Windows-only per the waiver — this
/// fixture proves Linux is deliberately left at the toolkit's own default
/// (<see cref="HoverCard.LinuxBetweenShowDelayDefaultMs"/>) rather than forced to match Windows,
/// and the 20 s duration is not asserted at all, since no Avalonia property exists to carry it.
///
/// <para>Plain property get/set on an unattached <see cref="Control"/> needs no running Avalonia
/// application — only rendering does (see <c>DetailWindowRenderProof</c>'s own
/// <c>EnsurePlatformInitialized</c>), so this fixture constructs controls directly.</para>
/// </summary>
public sealed class HoverCardTests
{
    [Fact]
    public void ApplyTimingSetsShowDelayOnThreeDistinctElementsIndependently()
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
            Assert.True(element.IsSet(ToolTip.ShowDelayProperty));
            Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(element));
        }

        // An element nobody called ApplyTiming on never gets a local value — proving the three
        // elements above carry their own, not a shared default this test accidentally changed.
        Assert.False(untouched.IsSet(ToolTip.ShowDelayProperty));
    }

    [Fact]
    public void ApplyTimingLeavesBetweenShowDelayAtTheToolkitsOwnDefaultPerWaiverW1()
    {
        // Waiver W1 (OVI-593): only the 400 ms initial delay is a cross-platform guarantee. The
        // 3000 ms Windows value is deliberately not forced here — this toolkit's own unset
        // default (100 ms, CONFIRMED against Avalonia.Controls 12.1.3) is what Linux actually
        // shows, so ApplyTiming must not set a local value for it at all.
        var owner = new TextBlock();

        HoverCard.ApplyTiming(owner);

        Assert.False(owner.IsSet(ToolTip.BetweenShowDelayProperty));
        Assert.Equal(HoverCard.LinuxBetweenShowDelayDefaultMs, ToolTip.GetBetweenShowDelay(owner));
        Assert.NotEqual(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(owner));
    }

    [Fact]
    public void SettingTimingOnAContainerDoesNotReachAChildElement()
    {
        // The exact defect the source measured on WPF (docs/ui-spec.md §I in the source repo):
        // timing applied once to a parent silently does not flow down to its children.
        var container = new StackPanel();
        var child = new TextBlock();
        container.Children.Add(child);

        HoverCard.ApplyTiming(container);

        Assert.False(child.IsSet(ToolTip.ShowDelayProperty));
    }

    [Fact]
    public void FigureSetsATipAndTheSharedTimingOnTheSameOwner()
    {
        var colors = LinuxWindowThemePalette.Resolve(ThemePreference.Light);
        var owner = new TextBlock();

        HoverCard.Figure(owner, "47%", "session · resets 16:32", colors);

        Assert.NotNull(ToolTip.GetTip(owner));
        Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(owner));
    }

    [Fact]
    public void TextSetsATipAndTheSharedTimingOnTheSameOwner()
    {
        var colors = LinuxWindowThemePalette.Resolve(ThemePreference.Dark);
        var owner = new TextBlock();

        HoverCard.Text(owner, "Local estimate — not a vendor total.", colors);

        Assert.NotNull(ToolTip.GetTip(owner));
        Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(owner));
    }

    [Fact]
    public void BuiltCardIsABorderNotABareTextBlock()
    {
        // "No bare system tooltips anywhere in the panel" (OVI-621) — every card is this skin's
        // own styled border, never the content handed straight to Avalonia's default tip chrome.
        var colors = LinuxWindowThemePalette.Resolve(ThemePreference.Light);

        var card = HoverCard.BuildTextCard("x", colors);

        Assert.NotNull(card.Background);
        Assert.NotNull(card.BorderBrush);
    }
}
