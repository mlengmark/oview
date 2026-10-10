using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own account-block rendering, wired into the harness so every
/// <see cref="AccountIdentityFixture"/> is exercised against every skin from one test run
/// (ADR-0003). Each skin exposes the display name, email and tier badge as three separate
/// formatter calls, not one string (<c>PanelTextFormatter.AccountDisplayName</c>/
/// <c>AccountEmail</c>/<c>AccountTierBadge</c>); <see cref="Render"/> joins them with a
/// separator no real value in this fixture family ever contains, so a pinned
/// <see cref="ContentFact"/> can check any of the three without caring which one.
/// </summary>
public sealed record AccountIdentitySkinUnderTest(string Name, Func<AccountIdentity, string> Render);
