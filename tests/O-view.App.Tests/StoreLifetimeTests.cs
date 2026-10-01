namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0007 D4's one-owner-of-persisted-state rule: one <c>StoreLifetime</c>
/// construction creates the directory if missing and hands back exactly one instance of each
/// store, the same instance on every access, for the process. Each test uses its own temp
/// directory under the OS temp folder, never a real user profile path.
/// </summary>
public class StoreLifetimeTests : IDisposable
{
    private readonly string _directory;

    public StoreLifetimeTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "oview-store-lifetime-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Construction_creates_the_directory_when_it_does_not_exist()
    {
        Assert.False(Directory.Exists(_directory));

        _ = new StoreLifetime(_directory);

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public void Construction_does_not_throw_when_the_directory_already_exists()
    {
        Directory.CreateDirectory(_directory);

        var lifetime = new StoreLifetime(_directory);

        Assert.NotNull(lifetime.WeeklyResetAnchorStore);
        Assert.NotNull(lifetime.UsageLedgerStore);
    }

    [Fact]
    public void Each_store_property_returns_the_same_instance_on_every_access()
    {
        var lifetime = new StoreLifetime(_directory);

        Assert.Same(lifetime.WeeklyResetAnchorStore, lifetime.WeeklyResetAnchorStore);
        Assert.Same(lifetime.UsageLedgerStore, lifetime.UsageLedgerStore);
    }

    [Fact]
    public void Both_stores_are_usable_immediately_against_the_same_directory()
    {
        var lifetime = new StoreLifetime(_directory);

        Assert.Null(lifetime.WeeklyResetAnchorStore.Read());
        Assert.Empty(lifetime.UsageLedgerStore.QueryDailyUsage(TimeZoneInfo.Utc));
    }
}
