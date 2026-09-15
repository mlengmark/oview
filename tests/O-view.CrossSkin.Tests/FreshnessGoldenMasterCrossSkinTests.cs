using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to <c>PanelTextFormatter.Freshness</c> (OVI-29,
/// Phase 1 slice 3.1): runs every <see cref="FreshnessFixture"/> against every skin's own
/// <c>Freshness</c> implementation, and fails if a skin omits or contradicts a pinned
/// content fact — not if it phrases the fact differently from another skin. Parallel to
/// <see cref="GoldenMasterCrossSkinTests"/> (the tooltip fixture family) rather than a
/// change to it — see <see cref="FreshnessFixture"/>'s doc comment for why.
/// </summary>
public class FreshnessGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<FreshnessSkinUnderTest> Skins = new[]
    {
        new FreshnessSkinUnderTest("O-view.Tray", TrayPanelTextFormatter.Freshness),
        new FreshnessSkinUnderTest("O-view.Linux", LinuxPanelTextFormatter.Freshness),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in FreshnessFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = skin.Format(fixture.Snapshot, fixture.UtcNow, fixture.DisplayZone);

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
