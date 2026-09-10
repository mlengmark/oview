using OView.CrossSkin.Tests.Fixtures;
using TrayTooltipFormatter = OView.Tray.Presentation.TooltipFormatter;
using LinuxTooltipFormatter = OView.Linux.Presentation.TooltipFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness: runs every golden-master fixture against every skin's
/// own string-construction code from one test run, and fails if a skin omits or
/// contradicts a pinned content fact (a number, a rounding rule, a disclosure) — not if it
/// phrases the fact differently from another skin. This is the mechanism that replaces
/// <c>PanelText.cs</c>'s centralization guarantee once display strings extraction reaches
/// it (issues #55/#56 on the source repo).
/// </summary>
public class GoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<SkinUnderTest> Skins = new[]
    {
        new SkinUnderTest("O-view.Tray", (snapshot, zone) => TrayTooltipFormatter.Format(snapshot, zone)),
        new SkinUnderTest("O-view.Linux", (snapshot, zone) => LinuxTooltipFormatter.Format(snapshot, zone)),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in GoldenMasterFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = skin.Format(fixture.Snapshot, fixture.DisplayZone);

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
