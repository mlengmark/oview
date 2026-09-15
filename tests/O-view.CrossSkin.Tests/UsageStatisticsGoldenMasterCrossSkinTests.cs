using OView.CrossSkin.Tests.Fixtures;
using TrayUsageFormatter = OView.Tray.Presentation.UsageFormatter;
using TrayPanelStatisticsFormatter = OView.Tray.Presentation.PanelStatisticsFormatter;
using LinuxUsageFormatter = OView.Linux.Presentation.UsageFormatter;
using LinuxPanelStatisticsFormatter = OView.Linux.Presentation.PanelStatisticsFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the <see cref="OView.Core.Models.UsageStatistics"/>
/// slice of the contract (OVI-27): runs every <see cref="UsageStatisticsFixture"/> against
/// every skin's own <c>UsageFormatter</c>/<c>PanelStatisticsFormatter</c> code, and fails if
/// a skin omits or contradicts a pinned content fact — not if it phrases the fact
/// differently from another skin. Parallel to <see cref="GoldenMasterCrossSkinTests"/> (the
/// tooltip fixture family) rather than a change to it — see
/// <see cref="UsageStatisticsFixture"/>'s doc comment for why.
///
/// <para>The per-skin rendering below (concatenating <c>Tokens</c>, <c>Usd</c>, and
/// <c>CoverageNote</c> into one string) exists only for this test's own purposes — no
/// production panel/tile UI is built by this slice on either skin; that is later, separate
/// work.</para>
/// </summary>
public class UsageStatisticsGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<UsageStatisticsSkinUnderTest> Skins = new[]
    {
        new UsageStatisticsSkinUnderTest("O-view.Tray", RenderTray),
        new UsageStatisticsSkinUnderTest("O-view.Linux", RenderLinux),
    };

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in UsageStatisticsFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = skin.Render(fixture.Statistics);

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

    private static string RenderTray(OView.Core.Models.UsageStatistics statistics)
    {
        var note = TrayPanelStatisticsFormatter.CoverageNote(statistics.HistoryCoverage);
        var suffix = note.Length > 0 ? $" · {note}" : "";

        return $"Today {TrayUsageFormatter.Tokens(statistics.OutputTokensToday)} tokens / " +
               $"{TrayUsageFormatter.Usd(statistics.EstimatedSpendToday)} · " +
               $"31d {TrayUsageFormatter.Tokens(statistics.OutputTokensWindow31d)} tokens / " +
               $"{TrayUsageFormatter.Usd(statistics.EstimatedValueWindow31d)}{suffix}";
    }

    private static string RenderLinux(OView.Core.Models.UsageStatistics statistics)
    {
        var note = LinuxPanelStatisticsFormatter.CoverageNote(statistics.HistoryCoverage);
        var suffix = note.Length > 0 ? $" ({note})" : "";

        return $"Today: {LinuxUsageFormatter.Tokens(statistics.OutputTokensToday)} tokens, " +
               $"{LinuxUsageFormatter.Usd(statistics.EstimatedSpendToday)} / " +
               $"31d: {LinuxUsageFormatter.Tokens(statistics.OutputTokensWindow31d)} tokens, " +
               $"{LinuxUsageFormatter.Usd(statistics.EstimatedValueWindow31d)}{suffix}";
    }
}
