using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the <c>Countdown</c>/<c>SessionReset</c>/
/// <c>WeeklyReset</c>/<c>WeeklyResetConflict</c> raw-scalar family (OVI-29, Phase 1 slice
/// 3.1): runs every <see cref="PanelTextResetFixture"/> against every skin's own
/// implementation, and fails if a skin omits or contradicts a pinned content fact. Parallel
/// to <see cref="GoldenMasterCrossSkinTests"/> and <see cref="FreshnessGoldenMasterCrossSkinTests"/>
/// rather than a change to either — see <see cref="PanelTextResetFixture"/>'s doc comment
/// for why this family needed its own shape.
/// </summary>
public class PanelTextResetGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<PanelTextResetSkinUnderTest> Skins = new[]
    {
        new PanelTextResetSkinUnderTest(
            "O-view.Tray",
            TrayPanelTextFormatter.Countdown,
            TrayPanelTextFormatter.SessionReset,
            TrayPanelTextFormatter.WeeklyReset,
            TrayPanelTextFormatter.WeeklyResetConflict),
        new PanelTextResetSkinUnderTest(
            "O-view.Linux",
            LinuxPanelTextFormatter.Countdown,
            LinuxPanelTextFormatter.SessionReset,
            LinuxPanelTextFormatter.WeeklyReset,
            LinuxPanelTextFormatter.WeeklyResetConflict),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in PanelTextResetFixtures.All)
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
