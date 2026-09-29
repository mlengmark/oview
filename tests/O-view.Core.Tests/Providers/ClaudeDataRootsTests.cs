using OView.Core.Providers;

namespace OView.Core.Tests.Providers;

/// <summary>
/// Proves ADR-0005 D5's path-resolution rules with fake/injected roots only — no test in
/// this file touches the real filesystem or the real operating system, which is the point
/// of the rules being pure functions of injected inputs.
/// </summary>
public class ClaudeDataRootsTests
{
    [Fact]
    public void WindowsCanonicalCombinesAppDataWithClaude()
    {
        var result = ClaudeDataRoots.WindowsCanonical(@"C:\Users\fakeuser\AppData\Roaming");

        Assert.Equal(Path.Combine(@"C:\Users\fakeuser\AppData\Roaming", "Claude"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WindowsCanonicalReturnsNullWithoutAppDataRatherThanGuessingAPath(string? appData)
    {
        Assert.Null(ClaudeDataRoots.WindowsCanonical(appData));
    }

    [Fact]
    public void WindowsMsixCombinesLocalAppDataPackagesFamilyAndLocalCacheRoamingClaude()
    {
        var result = ClaudeDataRoots.WindowsMsix(@"C:\Users\fakeuser\AppData\Local", "FakeVendor.Claude_abc123");

        Assert.Equal(
            Path.Combine(@"C:\Users\fakeuser\AppData\Local", "Packages", "FakeVendor.Claude_abc123", "LocalCache", "Roaming", "Claude"),
            result);
    }

    [Theory]
    [InlineData(null, "FakeVendor.Claude_abc123")]
    [InlineData(@"C:\Users\fakeuser\AppData\Local", null)]
    [InlineData("", "FakeVendor.Claude_abc123")]
    [InlineData(@"C:\Users\fakeuser\AppData\Local", "")]
    public void WindowsMsixReturnsNullWhenEitherInputIsMissing(string? localAppData, string? packageFamilyName)
    {
        Assert.Null(ClaudeDataRoots.WindowsMsix(localAppData, packageFamilyName));
    }

    [Fact]
    public void LinuxCanonicalCombinesHomeWithDotConfigClaude()
    {
        var result = ClaudeDataRoots.LinuxCanonical("/home/fakeuser");

        Assert.Equal(Path.Combine("/home/fakeuser", ".config", "Claude"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LinuxCanonicalReturnsNullWithoutHomeRatherThanGuessingAPath(string? home)
    {
        Assert.Null(ClaudeDataRoots.LinuxCanonical(home));
    }

    [Fact]
    public void LinuxSnapCombinesHomeSnapNameCurrentDotConfigClaude()
    {
        var result = ClaudeDataRoots.LinuxSnap("/home/fakeuser", "fake-claude-snap");

        Assert.Equal(
            Path.Combine("/home/fakeuser", "snap", "fake-claude-snap", "current", ".config", "Claude"),
            result);
    }

    [Theory]
    [InlineData(null, "fake-claude-snap")]
    [InlineData("/home/fakeuser", null)]
    [InlineData("", "fake-claude-snap")]
    [InlineData("/home/fakeuser", "")]
    public void LinuxSnapReturnsNullWhenEitherInputIsMissing(string? home, string? snapName)
    {
        Assert.Null(ClaudeDataRoots.LinuxSnap(home, snapName));
    }

    [Fact]
    public void LinuxFlatpakCombinesHomeDotVarAppAppIdConfigClaude()
    {
        var result = ClaudeDataRoots.LinuxFlatpak("/home/fakeuser", "com.fake.Claude");

        Assert.Equal(
            Path.Combine("/home/fakeuser", ".var", "app", "com.fake.Claude", "config", "Claude"),
            result);
    }

    [Theory]
    [InlineData(null, "com.fake.Claude")]
    [InlineData("/home/fakeuser", null)]
    [InlineData("", "com.fake.Claude")]
    [InlineData("/home/fakeuser", "")]
    public void LinuxFlatpakReturnsNullWhenEitherInputIsMissing(string? home, string? flatpakAppId)
    {
        Assert.Null(ClaudeDataRoots.LinuxFlatpak(home, flatpakAppId));
    }

    [Fact]
    public void CandidateRootsForWindowsReturnsCanonicalThenMsixInThatOrder()
    {
        var inputs = new ClaudeDataRootInputs(
            ClaudeHostPlatform.Windows,
            AppDataDirectory: @"C:\Users\fakeuser\AppData\Roaming",
            LocalAppDataDirectory: @"C:\Users\fakeuser\AppData\Local",
            WindowsPackageFamilyName: "FakeVendor.Claude_abc123");

        var candidates = ClaudeDataRoots.CandidateRoots(inputs);

        Assert.Equal(
            [
                ClaudeDataRoots.WindowsCanonical(inputs.AppDataDirectory)!,
                ClaudeDataRoots.WindowsMsix(inputs.LocalAppDataDirectory, inputs.WindowsPackageFamilyName)!
            ],
            candidates);
    }

    [Fact]
    public void CandidateRootsForWindowsSkipsMsixWhenPackageFamilyNameIsNotSupplied()
    {
        var inputs = new ClaudeDataRootInputs(
            ClaudeHostPlatform.Windows,
            AppDataDirectory: @"C:\Users\fakeuser\AppData\Roaming",
            LocalAppDataDirectory: @"C:\Users\fakeuser\AppData\Local");

        var candidates = ClaudeDataRoots.CandidateRoots(inputs);

        Assert.Equal([ClaudeDataRoots.WindowsCanonical(inputs.AppDataDirectory)!], candidates);
    }

    [Fact]
    public void CandidateRootsForLinuxReturnsCanonicalThenSnapThenFlatpakInThatOrder()
    {
        var inputs = new ClaudeDataRootInputs(
            ClaudeHostPlatform.Linux,
            HomeDirectory: "/home/fakeuser",
            LinuxSnapName: "fake-claude-snap",
            LinuxFlatpakAppId: "com.fake.Claude");

        var candidates = ClaudeDataRoots.CandidateRoots(inputs);

        Assert.Equal(
            [
                ClaudeDataRoots.LinuxCanonical(inputs.HomeDirectory)!,
                ClaudeDataRoots.LinuxSnap(inputs.HomeDirectory, inputs.LinuxSnapName)!,
                ClaudeDataRoots.LinuxFlatpak(inputs.HomeDirectory, inputs.LinuxFlatpakAppId)!
            ],
            candidates);
    }

    [Fact]
    public void CandidateRootsForLinuxSkipsSnapAndFlatpakWhenTheirIdentifiersAreNotSupplied()
    {
        var inputs = new ClaudeDataRootInputs(ClaudeHostPlatform.Linux, HomeDirectory: "/home/fakeuser");

        var candidates = ClaudeDataRoots.CandidateRoots(inputs);

        Assert.Equal([ClaudeDataRoots.LinuxCanonical(inputs.HomeDirectory)!], candidates);
    }

    [Theory]
    [InlineData(ClaudeHostPlatform.Windows)]
    [InlineData(ClaudeHostPlatform.Linux)]
    public void CandidateRootsReturnsEmptyWhenNoRootsAreSupplied(ClaudeHostPlatform platform)
    {
        var candidates = ClaudeDataRoots.CandidateRoots(new ClaudeDataRootInputs(platform));

        Assert.Empty(candidates);
    }
}
