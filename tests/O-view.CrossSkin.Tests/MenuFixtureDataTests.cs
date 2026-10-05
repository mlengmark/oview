using OView.CrossSkin.Tests.Fixtures;

namespace OView.CrossSkin.Tests;

/// <summary>
/// Sanity tests for <see cref="MenuFixture"/> (ADR-0003, ADR-0009 D1-D4, OVI-438). Like
/// <see cref="AlertFixture"/>, no menu presenter exists yet (ADR-0009 slices 6/9), so there is
/// no rendered text to run content facts against. These tests instead confirm the fixture data
/// itself states the four content facts the slice brief names: which items exist, that
/// run-at-startup is pinned against the OS-returned state rather than the requested one, that a
/// failed toggle is marked as owing a stated failure, and that the threshold picker's persisted
/// value is pinned per scenario rather than one fixed label. A real cross-skin test replaces
/// this one in the slice that wires each menu's <c>Render</c>.
/// </summary>
public class MenuFixtureDataTests
{
    [Fact]
    public void MenuFixtureNamesAreUnique()
    {
        var names = MenuFixtures.All.Select(f => f.Name).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void MenuFixturesCoverEveryMenuItemKind()
    {
        var covered = MenuFixtures.All.Select(f => f.Item).Distinct().ToHashSet();
        foreach (var kind in Enum.GetValues<MenuItemKind>())
        {
            Assert.Contains(kind, covered);
        }
    }

    [Fact]
    public void AtLeastOneRunAtStartupFixtureHasOsReturnedStateDifferFromRequested()
    {
        // D3: proves the fixture set actually exercises "reports what the OS returned, not
        // what was requested" rather than only ever covering the case where they happen to agree.
        var exercisesTheDistinction = MenuFixtures.All
            .Where(f => f.Item == MenuItemKind.RunAtStartup)
            .Any(f => f.StartupRequestedEnabled != f.StartupOsReturnedEnabled);

        Assert.True(exercisesTheDistinction);
    }

    [Fact]
    public void EveryRunAtStartupFixtureIsFailedToggleIffRequestedDiffersFromOsReturned()
    {
        foreach (var fixture in MenuFixtures.All.Where(f => f.Item == MenuItemKind.RunAtStartup))
        {
            var requestedDiffersFromReturned =
                fixture.StartupRequestedEnabled != fixture.StartupOsReturnedEnabled;

            Assert.Equal(requestedDiffersFromReturned, fixture.FailedToggle);
        }
    }

    [Fact]
    public void AtLeastOneRunAtStartupFixtureIsAFailedToggle()
    {
        // D3's third rule — a failed toggle must be visible — has nothing to pin against if no
        // fixture ever models one.
        Assert.Contains(MenuFixtures.All, f => f.Item == MenuItemKind.RunAtStartup && f.FailedToggle);
    }

    [Fact]
    public void EveryNotificationThresholdFixturePinsItsOwnPersistedDigit()
    {
        foreach (var fixture in MenuFixtures.All.Where(f => f.Item == MenuItemKind.NotificationThreshold))
        {
            Assert.NotNull(fixture.PersistedThresholdPercent);
            Assert.True(fixture.ContentFacts.Count > 0, $"[{fixture.Name}] pins no content facts.");
            Assert.True(
                fixture.ContentFacts.All(fact => fact.IsSatisfiedBy(fixture.PersistedThresholdPercent!.Value.ToString())),
                $"[{fixture.Name}] content facts do not match its own persisted percent.");
        }
    }

    [Fact]
    public void NotificationThresholdFixturesPinDistinctPersistedValues()
    {
        // D4/ADR-0003: a menu that hardcoded one label could satisfy at most one of these.
        var persistedValues = MenuFixtures.All
            .Where(f => f.Item == MenuItemKind.NotificationThreshold)
            .Select(f => f.PersistedThresholdPercent)
            .ToList();

        Assert.Equal(persistedValues.Distinct().Count(), persistedValues.Count);
        Assert.True(persistedValues.Count >= 2, "Needs at least two scenarios to prove the value isn't fixed.");
    }
}
