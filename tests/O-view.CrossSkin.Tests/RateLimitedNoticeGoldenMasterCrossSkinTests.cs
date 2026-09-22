using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to <c>PanelTextFormatter.RateLimitedNotice</c>
/// (OVI-98, Phase 1 slice 3.4): runs every <see cref="RateLimitedNoticeFixture"/> against
/// every skin's own <c>RateLimitedNotice</c> implementation, and fails if a skin omits or
/// contradicts a pinned content fact — not if it phrases the fact differently from another
/// skin. Parallel to <see cref="GoldenMasterCrossSkinTests"/>, <see cref="FreshnessGoldenMasterCrossSkinTests"/>,
/// and <see cref="BoostNoticeGoldenMasterCrossSkinTests"/> rather than a change to any of
/// them — see <see cref="RateLimitedNoticeFixture"/>'s doc comment for why.
/// </summary>
public class RateLimitedNoticeGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<RateLimitedNoticeSkinUnderTest> Skins = new[]
    {
        new RateLimitedNoticeSkinUnderTest("O-view.Tray", TrayPanelTextFormatter.RateLimitedNotice),
        new RateLimitedNoticeSkinUnderTest("O-view.Linux", LinuxPanelTextFormatter.RateLimitedNotice),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in RateLimitedNoticeFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = skin.Format(fixture.RetryAfterUtc, fixture.Local);

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
