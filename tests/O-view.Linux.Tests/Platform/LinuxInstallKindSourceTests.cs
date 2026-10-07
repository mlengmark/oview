using OView.App.Updates;
using OView.Linux.Platform;

namespace OView.Linux.Tests.Platform;

/// <summary>
/// ADR-0010 D1's risk note is that install-kind detection cannot be proven on CI — these tests
/// prove the path comparison itself, with both the dpkg-owned path and the running executable's
/// path injected, never the real environment.
/// </summary>
public class LinuxInstallKindSourceTests
{
    [Fact]
    public void ExecutableUnderTheDpkgPathIsLinuxPackage()
    {
        var subject = new LinuxInstallKindSource("/usr/lib/o-view", () => "/usr/lib/o-view/o-view");

        Assert.Equal(InstallKind.LinuxPackage, subject.Current);
    }

    [Fact]
    public void ExecutableOutsideTheDpkgPathIsLinuxTarball()
    {
        var subject = new LinuxInstallKindSource("/usr/lib/o-view", () => "/home/user/o-view/o-view");

        Assert.Equal(InstallKind.LinuxTarball, subject.Current);
    }

    [Fact]
    public void ASimilarlyNamedSiblingDirectoryIsNotMistakenForTheDpkgPath()
    {
        // "/usr/lib/o-view-extra" starts with "/usr/lib/o-view" as a raw string but is not the
        // dpkg-owned directory itself — StartsWith alone would misclassify it.
        var subject = new LinuxInstallKindSource("/usr/lib/o-view", () => "/usr/lib/o-view-extra/o-view");

        Assert.Equal(InstallKind.LinuxTarball, subject.Current);
    }

    [Fact]
    public void NoProcessPathIsLinuxTarball()
    {
        var subject = new LinuxInstallKindSource("/usr/lib/o-view", () => null);

        Assert.Equal(InstallKind.LinuxTarball, subject.Current);
    }

    [Fact]
    public void DefaultDpkgInstallPathMatchesTheSourceRepositoryConvention()
    {
        Assert.Equal("/usr/lib/o-view", LinuxInstallKindSource.DefaultDpkgInstallPath);
    }
}
