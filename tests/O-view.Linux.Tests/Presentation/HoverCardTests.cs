using Avalonia.Controls;
using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// ADR-0008 slicing-table slice P2 (OVI-621): the <c>HoverTimingFixture</c> ADR-0008 D11b
/// requires, for this skin's own timing surface — the two Avalonia exposes
/// (<see cref="ToolTip.ShowDelayProperty"/>, <see cref="ToolTip.BetweenShowDelayProperty"/>),
/// proven to resolve on each of several distinct elements independently, the same per-element
/// discipline the Windows skin's own test proves. The 20 s show duration is not asserted here —
/// see <see cref="HoverCard"/>'s remarks and waiver candidate W1
/// (<c>docs/parity/g7-detail-window-parity.md</c>): no Avalonia property exists to carry it, so
/// there is nothing for this fixture to resolve.
///
/// <para>Plain property get/set on an unattached <see cref="Control"/> needs no running Avalonia
/// application — only rendering does (see <c>DetailWindowRenderProof</c>'s own
/// <c>EnsurePlatformInitialized</c>), so this fixture constructs controls directly.</para>
/// </summary>
public sealed class HoverCardTests
{
    [Fact]
    public void ApplyTimingSetsBothDelaysOnThreeDistinctElementsIndependently()
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
            Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(element));
            Assert.Equal(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(element));
        }

        // An element nobody called ApplyTiming on resolves the toolkit's own default for
        // BetweenShowDelay (measured 100 ms, CONFIRMED by reflection against Avalonia.Controls
        // 12.1.3), proving the three elements above carry it on themselves, not through a shared
        // default this test accidentally changed. ShowDelay's own unset default happens to equal
        // this skin's chosen value (measured 400 ms) and so cannot tell "set" apart from
        // "defaulted" by itself — BetweenShowDelay is the property that actually exercises this.
        Assert.NotEqual(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(untouched));
    }

    [Fact]
    public void SettingTimingOnAContainerDoesNotReachAChildElement()
    {
        // The exact defect the source measured on WPF (docs/ui-spec.md §I in the source repo):
        // timing applied once to a parent silently does not flow down to its children. Proven
        // here via BetweenShowDelay — see the remark above on why ShowDelay's own default cannot
        // distinguish "inherited" from "never set".
        var container = new StackPanel();
        var child = new TextBlock();
        container.Children.Add(child);

        HoverCard.ApplyTiming(container);

        Assert.NotEqual(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(child));
    }

    [Fact]
    public void FigureSetsATipAndTheSharedTimingOnTheSameOwner()
    {
        var colors = LinuxWindowThemePalette.Resolve(ThemePreference.Light);
        var owner = new TextBlock();

        HoverCard.Figure(owner, "47%", "session · resets 16:32", colors);

        Assert.NotNull(ToolTip.GetTip(owner));
        Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(owner));
        Assert.Equal(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(owner));
    }

    [Fact]
    public void TextSetsATipAndTheSharedTimingOnTheSameOwner()
    {
        var colors = LinuxWindowThemePalette.Resolve(ThemePreference.Dark);
        var owner = new TextBlock();

        HoverCard.Text(owner, "Local estimate — not a vendor total.", colors);

        Assert.NotNull(ToolTip.GetTip(owner));
        Assert.Equal(HoverCard.InitialDelayMs, ToolTip.GetShowDelay(owner));
        Assert.Equal(HoverCard.BetweenDelayMs, ToolTip.GetBetweenShowDelay(owner));
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
