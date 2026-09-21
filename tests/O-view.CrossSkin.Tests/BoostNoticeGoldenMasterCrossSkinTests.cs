using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to <c>BoostChip</c>/<c>BoostCard</c> (OVI-92, Phase
/// 1 slice 3.3): runs every <see cref="BoostNoticeFixture"/> against every skin's own
/// implementation, and fails if a skin omits or contradicts a pinned content fact — not if it
/// phrases the fact differently from another skin. Parallel to
/// <see cref="PanelTextResetGoldenMasterCrossSkinTests"/> rather than a change to it — see
/// <see cref="BoostNoticeFixture"/>'s doc comment for why this family needed its own shape.
/// </summary>
public class BoostNoticeGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<BoostNoticeSkinUnderTest> Skins = new[]
    {
        new BoostNoticeSkinUnderTest(
            "O-view.Tray",
            TrayPanelTextFormatter.BoostChip,
            TrayPanelTextFormatter.BoostCard),
        new BoostNoticeSkinUnderTest(
            "O-view.Linux",
            LinuxPanelTextFormatter.BoostChip,
            LinuxPanelTextFormatter.BoostCard),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in BoostNoticeFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = fixture.Render(skin);

                foreach (var fact in fixture.ContentFacts)
                {
                    if (!fact.IsSatisfiedBy(rendered))
                    {
                        failures.Add(
                            $"[{fixture.Name}] {skin.Name} rendered \"{rendered}\", " +
                            $"which does not satisfy content fact: {fact.Description}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, "Cross-skin golden-master mismatch(es):\n" + string.Join("\n", failures));
    }
}
