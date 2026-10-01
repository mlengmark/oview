namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0006 D2's per-platform default directory against injected environment-variable
/// lookups only — no real environment variable is read, so both branches run regardless of the
/// OS the test executes on. The Linux branch is INFERRED/unverified on real hardware, same as
/// prior slices; these tests check the documented rule, not a real XDG desktop.
/// </summary>
public class StoreDirectoryResolverTests
{
    [Fact]
    public void ResolveWindows_combines_LOCALAPPDATA_with_the_app_directory_name()
    {
        var path = StoreDirectoryResolver.ResolveWindows(name =>
            name == "LOCALAPPDATA" ? @"C:\Users\someone\AppData\Local" : null);

        Assert.Equal(Path.Combine(@"C:\Users\someone\AppData\Local", "O-view"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveWindows_throws_when_LOCALAPPDATA_is_missing(string? localAppData)
    {
        Assert.Throws<InvalidOperationException>(() =>
            StoreDirectoryResolver.ResolveWindows(name => name == "LOCALAPPDATA" ? localAppData : null));
    }

    [Fact]
    public void ResolveLinux_prefers_XDG_DATA_HOME_when_set()
    {
        var path = StoreDirectoryResolver.ResolveLinux(name => name switch
        {
            "XDG_DATA_HOME" => "/home/someone/.local/share",
            "HOME" => "/home/someone",
            _ => null,
        });

        Assert.Equal(Path.Combine("/home/someone/.local/share", "O-view"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveLinux_falls_back_to_HOME_when_XDG_DATA_HOME_is_missing(string? xdgDataHome)
    {
        var path = StoreDirectoryResolver.ResolveLinux(name => name switch
        {
            "XDG_DATA_HOME" => xdgDataHome,
            "HOME" => "/home/someone",
            _ => null,
        });

        Assert.Equal(Path.Combine("/home/someone", ".local", "share", "O-view"), path);
    }

    [Fact]
    public void ResolveLinux_throws_when_neither_XDG_DATA_HOME_nor_HOME_is_set()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StoreDirectoryResolver.ResolveLinux(_ => null));
    }

    [Fact]
    public void ResolveDefault_matches_the_current_process_OS_branch()
    {
        var path = StoreDirectoryResolver.ResolveDefault();

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(StoreDirectoryResolver.ResolveWindows(Environment.GetEnvironmentVariable), path);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal(StoreDirectoryResolver.ResolveLinux(Environment.GetEnvironmentVariable), path);
        }
        else
        {
            Assert.Fail("Unexpected OS for this test run.");
        }
    }
}
