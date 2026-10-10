using System.Globalization;
using OView.CrossSkin.Tests.Fixtures;
using TrayOnScreenCheck = OView.Tray.Presentation.DetailWindowOnScreenCheck;
using TrayScreenRect = OView.Tray.Presentation.ScreenRect;
using LinuxOnScreenCheck = OView.Linux.Presentation.DetailWindowOnScreenCheck;
using LinuxScreenRect = OView.Linux.Presentation.ScreenRect;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the off-screen fallback rule (ADR-0008 D4's
/// 2026-10-09 amendment, D5, slice P1): runs every <see cref="DetailWindowOnScreenFixture"/>
/// against every skin's own <c>DetailWindowOnScreenCheck.IsFullyOnScreen</c>, and fails if a
/// skin's on-screen verdict disagrees with the pinned content fact for that candidate and those
/// work areas. Deliberately does not call a shared implementation — D5 rejects one — so each
/// skin's formatter here wraps its own type. Parallel to
/// <see cref="DetailWindowPlacementGoldenMasterCrossSkinTests"/>.
/// </summary>
public class DetailWindowOnScreenGoldenMasterCrossSkinTests
{
    private static string Describe(bool onScreen) =>
        string.Create(CultureInfo.InvariantCulture, $"on-screen={onScreen}");

    private static readonly IReadOnlyList<DetailWindowOnScreenSkinUnderTest> Skins = new[]
    {
        new DetailWindowOnScreenSkinUnderTest(
            "O-view.Tray",
            (candidate, workAreas) => Describe(TrayOnScreenCheck.IsFullyOnScreen(
                new TrayScreenRect(candidate.X, candidate.Y, candidate.Width, candidate.Height),
                workAreas.Select(wa => new TrayScreenRect(wa.X, wa.Y, wa.Width, wa.Height)).ToArray()))),
        new DetailWindowOnScreenSkinUnderTest(
            "O-view.Linux",
            (candidate, workAreas) => Describe(LinuxOnScreenCheck.IsFullyOnScreen(
                new LinuxScreenRect(candidate.X, candidate.Y, candidate.Width, candidate.Height),
                workAreas.Select(wa => new LinuxScreenRect(wa.X, wa.Y, wa.Width, wa.Height)).ToArray()))),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in DetailWindowOnScreenFixtures.All)
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
