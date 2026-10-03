using System.Globalization;
using OView.CrossSkin.Tests.Fixtures;
using TrayPlacement = OView.Tray.Presentation.DetailWindowPlacement;
using LinuxPlacement = OView.Linux.Presentation.DetailWindowPlacement;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the detail window's first-placement rule (ADR-0008
/// D5, slice 2): runs every <see cref="DetailWindowPlacementFixture"/> against every skin's own
/// <c>DetailWindowPlacement.Compute</c>, and fails if a skin's corner, margin, or fallback choice
/// disagrees with the pinned content fact for that rectangle. Deliberately does not call a shared
/// implementation — D5 rejects one — so each skin's formatter here wraps its own type. Parallel to
/// <see cref="UsageCaveatGoldenMasterCrossSkinTests"/>.
/// </summary>
public class DetailWindowPlacementGoldenMasterCrossSkinTests
{
    private static string Describe(string corner, double x, double y, double margin, bool fallback) =>
        string.Create(CultureInfo.InvariantCulture, $"corner={corner} x={x} y={y} margin={margin} fallback={fallback}");

    private static readonly IReadOnlyList<DetailWindowPlacementSkinUnderTest> Skins = new[]
    {
        new DetailWindowPlacementSkinUnderTest(
            "O-view.Tray",
            (wl, wt, ww, wh, winW, winH, m) =>
            {
                var r = TrayPlacement.Compute(wl, wt, ww, wh, winW, winH, m);
                return Describe(r.Corner.ToString(), r.X, r.Y, r.MarginPx, r.IsFallback);
            }),
        new DetailWindowPlacementSkinUnderTest(
            "O-view.Linux",
            (wl, wt, ww, wh, winW, winH, m) =>
            {
                var r = LinuxPlacement.Compute(wl, wt, ww, wh, winW, winH, m);
                return Describe(r.Corner.ToString(), r.X, r.Y, r.MarginPx, r.IsFallback);
            }),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in DetailWindowPlacementFixtures.All)
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
