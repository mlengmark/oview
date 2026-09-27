using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the off-plan banner's five entry points (OVI-168,
/// Phase 1 sub-slice 5): runs every <see cref="OffPlanFixture"/> against every skin's own
/// implementation, and fails if a skin omits or contradicts a pinned content fact — not if it
/// phrases the fact differently from another skin. Parallel to
/// <see cref="BoostNoticeGoldenMasterCrossSkinTests"/> rather than a change to it.
/// </summary>
public class OffPlanGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<OffPlanSkinUnderTest> Skins = new[]
    {
        new OffPlanSkinUnderTest(
            "O-view.Tray",
            TrayPanelTextFormatter.OffPlanTitle,
            TrayPanelTextFormatter.OffPlanDetail,
            TrayPanelTextFormatter.OffPlanNote,
            TrayPanelTextFormatter.EstTodayLabel,
            TrayPanelTextFormatter.OffPlanHint,
            TrayPanelTextFormatter.UsageSettingsUrl),
        new OffPlanSkinUnderTest(
            "O-view.Linux",
            LinuxPanelTextFormatter.OffPlanTitle,
            LinuxPanelTextFormatter.OffPlanDetail,
            LinuxPanelTextFormatter.OffPlanNote,
            LinuxPanelTextFormatter.EstTodayLabel,
            LinuxPanelTextFormatter.OffPlanHint,
            LinuxPanelTextFormatter.UsageSettingsUrl),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in OffPlanFixtures.All)
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

    /// <summary>
    /// ADR-0001's amendment deliberately leaves <c>UsageSettingsUrl</c> skin-owned and
    /// duplicated rather than promoting it to a Core constant. Duplication without a check is
    /// exactly the drift this harness exists to catch, so it is pinned here directly rather
    /// than only via a fixture's content facts.
    /// </summary>
    [Fact]
    public void EverySkinUsesTheSameUsageSettingsUrl()
    {
        Assert.All(Skins, skin => Assert.Equal(Skins[0].UsageSettingsUrl, skin.UsageSettingsUrl));
    }
}
