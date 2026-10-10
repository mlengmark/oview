using OView.CrossSkin.Tests.Fixtures;
using TrayPanelStatisticsFormatter = OView.Tray.Presentation.PanelStatisticsFormatter;
using LinuxPanelStatisticsFormatter = OView.Linux.Presentation.PanelStatisticsFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the statistics tiles' coverage caption (ADR-0008
/// D11b, gate G7 parity slice P10): runs every <see cref="CoverageCaptionFixture"/> against
/// every skin's own implementation, and fails if a skin omits or contradicts a pinned content
/// fact — not if it phrases the fact differently from another skin. Parallel to
/// <see cref="UsageCaveatGoldenMasterCrossSkinTests"/>.
/// </summary>
public class CoverageCaptionGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<CoverageCaptionSkinUnderTest> Skins = new[]
    {
        new CoverageCaptionSkinUnderTest("O-view.Tray", TrayPanelStatisticsFormatter.CoverageCaption),
        new CoverageCaptionSkinUnderTest("O-view.Linux", LinuxPanelStatisticsFormatter.CoverageCaption),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in CoverageCaptionFixtures.All)
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
