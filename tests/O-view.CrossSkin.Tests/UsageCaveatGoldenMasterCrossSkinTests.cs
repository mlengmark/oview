using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the usage-tile <c>Caveat</c>/<c>RateAge</c>
/// (OVI-165, Phase 1 slice 4): runs every <see cref="UsageCaveatFixture"/> against every skin's
/// own implementation, and fails if a skin omits or contradicts a pinned content fact — not if
/// it phrases the fact differently from another skin. Parallel to
/// <see cref="BoostNoticeGoldenMasterCrossSkinTests"/>.
/// </summary>
public class UsageCaveatGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<UsageCaveatSkinUnderTest> Skins = new[]
    {
        new UsageCaveatSkinUnderTest(
            "O-view.Tray",
            TrayPanelTextFormatter.Caveat,
            TrayPanelTextFormatter.RateAge),
        new UsageCaveatSkinUnderTest(
            "O-view.Linux",
            LinuxPanelTextFormatter.Caveat,
            LinuxPanelTextFormatter.RateAge),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in UsageCaveatFixtures.All)
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
