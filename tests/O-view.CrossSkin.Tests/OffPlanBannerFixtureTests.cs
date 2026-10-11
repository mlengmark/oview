using OView.CrossSkin.Tests.Fixtures;

namespace OView.CrossSkin.Tests;

/// <summary>
/// The cross-skin golden-master test for the off-plan live banner (ADR-0003, gate G7 parity
/// slice P15, OVI-691): both skins' own <c>DetailWindowContentBuilder.Build</c> read the same
/// <see cref="OffPlanBannerFixtures"/> and must satisfy every pinned <see cref="ContentFact"/>,
/// and must agree on which states show a banner at all.
/// </summary>
public sealed class OffPlanBannerFixtureTests
{
    [Fact]
    public void FixtureNamesAreUnique()
    {
        var names = OffPlanBannerFixtures.All.Select(f => f.Name).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    public static IEnumerable<object[]> Fixtures()
    {
        foreach (var fixture in OffPlanBannerFixtures.All)
        {
            yield return new object[] { fixture };
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TraySatisfiesEveryPinnedContentFact(OffPlanBannerFixture fixture)
    {
        var content = OView.Tray.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);
        var rendered = content.OffPlanBannerTitle + "\n" + content.OffPlanBannerDetail;

        foreach (var fact in fixture.ContentFacts)
        {
            Assert.True(fact.IsSatisfiedBy(rendered), $"[tray/{fixture.Name}] failed: {fact.Description}");
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void LinuxSatisfiesEveryPinnedContentFact(OffPlanBannerFixture fixture)
    {
        var content = OView.Linux.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);
        var rendered = content.OffPlanBannerTitle + "\n" + content.OffPlanBannerDetail;

        foreach (var fact in fixture.ContentFacts)
        {
            Assert.True(fact.IsSatisfiedBy(rendered), $"[linux/{fixture.Name}] failed: {fact.Description}");
        }
    }

    /// <summary>
    /// Both skins must agree on which states hide the banner entirely (gate G7 parity slice
    /// P15): a skin cannot show a banner the other hides, or vice versa, for the same detail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void BothSkinsAgreeOnWhetherTheBannerShows(OffPlanBannerFixture fixture)
    {
        var tray = OView.Tray.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);
        var linux = OView.Linux.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);

        Assert.Equal(
            string.IsNullOrEmpty(tray.OffPlanBannerTitle),
            string.IsNullOrEmpty(linux.OffPlanBannerTitle));
    }

    [Fact]
    public void NoBannerFixtureProducesNoBannerOnEitherSkin()
    {
        var fixture = OffPlanBannerFixtures.NoBannerOnPlan;

        var tray = OView.Tray.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);
        var linux = OView.Linux.Presentation.DetailWindowContentBuilder.Build(
            fixture.Detail, OffPlanBannerFixtures.UtcNow, fixture.DisplayZone);

        Assert.True(string.IsNullOrEmpty(tray.OffPlanBannerTitle));
        Assert.True(string.IsNullOrEmpty(tray.OffPlanBannerDetail));
        Assert.True(string.IsNullOrEmpty(linux.OffPlanBannerTitle));
        Assert.True(string.IsNullOrEmpty(linux.OffPlanBannerDetail));
    }
}
