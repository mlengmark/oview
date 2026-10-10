using OView.Core.Models;
using OView.CrossSkin.Tests.Fixtures;
using TrayPanelTextFormatter = OView.Tray.Presentation.PanelTextFormatter;
using LinuxPanelTextFormatter = OView.Linux.Presentation.PanelTextFormatter;

namespace OView.CrossSkin.Tests;

/// <summary>
/// ADR-0003's anti-drift harness, applied to the detail window header's account block
/// (ADR-0008 D9e, gate G7 parity slice P8, OVI-645): runs every
/// <see cref="AccountIdentityFixture"/> against every skin's own
/// <c>PanelTextFormatter.AccountDisplayName</c>/<c>AccountEmail</c>/<c>AccountTierBadge</c>, and
/// fails if a skin omits or contradicts a pinned content fact — not if it phrases the fallback
/// differently from another skin. Parallel to <see cref="FreshnessGoldenMasterCrossSkinTests"/>.
/// </summary>
public class AccountIdentityGoldenMasterCrossSkinTests
{
    private static readonly IReadOnlyList<AccountIdentitySkinUnderTest> Skins = new[]
    {
        new AccountIdentitySkinUnderTest("O-view.Tray", Render(
            TrayPanelTextFormatter.AccountDisplayName, TrayPanelTextFormatter.AccountEmail, TrayPanelTextFormatter.AccountTierBadge)),
        new AccountIdentitySkinUnderTest("O-view.Linux", Render(
            LinuxPanelTextFormatter.AccountDisplayName, LinuxPanelTextFormatter.AccountEmail, LinuxPanelTextFormatter.AccountTierBadge)),
    };

    private static Func<AccountIdentity, string> Render(
        Func<AccountIdentity, string> displayName, Func<AccountIdentity, string> email, Func<AccountIdentity, string> tierBadge) =>
        account => $"{displayName(account)}|{email(account)}|{tierBadge(account)}";

    [Fact]
    public void EverySkinSatisfiesEveryPinnedContentFactInEveryFixture()
    {
        var failures = new List<string>();

        foreach (var fixture in AccountIdentityFixtures.All)
        {
            foreach (var skin in Skins)
            {
                var rendered = skin.Render(fixture.Account);

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
