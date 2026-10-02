using OView.CrossSkin.Tests.Fixtures;

namespace OView.CrossSkin.Tests;

/// <summary>
/// Sanity tests for <see cref="DetailWindowFixture"/> and <see cref="AlertFixture"/> (ADR-0003,
/// ADR-0008 D2, OVI-324). Unlike the six existing fixture families, these two carry no
/// <c>Render</c>/<c>SkinUnderTest</c> yet — no detail-window or alert presenter exists (ADR-0008
/// slices 6/7/10/11) — so there is nothing to run a fixture's content facts against. These
/// tests instead confirm the fixture data itself is well-formed: unique names, and at least
/// one pinned fact per fixture that genuinely exercises its snapshot/event (an empty fact list
/// is only acceptable for <see cref="AlertFixtures.UpdateAvailable"/>, which has no extra
/// payload to pin). A real cross-skin test replaces this one in the slice that wires each
/// presenter's <c>Render</c>.
/// </summary>
public class DetailWindowAndAlertFixtureDataTests
{
    [Fact]
    public void DetailWindowFixtureNamesAreUnique()
    {
        var names = DetailWindowFixtures.All.Select(f => f.Name).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void EveryDetailWindowFixturePinsAtLeastOneContentFact()
    {
        foreach (var fixture in DetailWindowFixtures.All)
        {
            Assert.True(fixture.ContentFacts.Count > 0, $"[{fixture.Name}] pins no content facts.");
        }
    }

    [Fact]
    public void AlertFixtureNamesAreUnique()
    {
        var names = AlertFixtures.All.Select(f => f.Name).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void EveryAlertFixtureExceptUpdateAvailablePinsAtLeastOneContentFact()
    {
        foreach (var fixture in AlertFixtures.All)
        {
            if (fixture == AlertFixtures.UpdateAvailable)
            {
                continue;
            }

            Assert.True(fixture.ContentFacts.Count > 0, $"[{fixture.Name}] pins no content facts.");
        }
    }
}
