using OView.App.Updates;
using OView.Tray.Platform;

namespace OView.Tray.Tests.Platform;

/// <summary>
/// ADR-0010 D1's risk note is that install-kind detection cannot be proven on CI — these tests
/// prove the path comparison itself, with both the install root and the running executable's
/// path injected, never the real environment.
/// </summary>
public class WindowsInstallKindSourceTests
{
    private const string InstallRoot = @"C:\Users\Test\AppData\Local\Programs\O-view";

    [Fact]
    public void ExecutableInTheInstallRootIsWindowsInstaller()
    {
        var subject = new WindowsInstallKindSource(InstallRoot, () => InstallRoot + @"\O-view.Tray.exe");

        Assert.Equal(InstallKind.WindowsInstaller, subject.Current);
    }

    [Fact]
    public void ExecutableElsewhereIsWindowsPortable()
    {
        var subject = new WindowsInstallKindSource(InstallRoot, () => @"C:\Users\Test\Downloads\O-view.Tray.exe");

        Assert.Equal(InstallKind.WindowsPortable, subject.Current);
    }

    [Fact]
    public void ComparisonIsCaseInsensitive()
    {
        var subject = new WindowsInstallKindSource(InstallRoot, () => InstallRoot.ToUpperInvariant() + @"\O-view.Tray.exe");

        Assert.Equal(InstallKind.WindowsInstaller, subject.Current);
    }

    [Fact]
    public void TrailingSeparatorOnTheInstallRootDoesNotAffectTheComparison()
    {
        var subject = new WindowsInstallKindSource(InstallRoot + @"\", () => InstallRoot + @"\O-view.Tray.exe");

        Assert.Equal(InstallKind.WindowsInstaller, subject.Current);
    }

    [Fact]
    public void NoProcessPathIsWindowsPortable()
    {
        var subject = new WindowsInstallKindSource(InstallRoot, () => null);

        Assert.Equal(InstallKind.WindowsPortable, subject.Current);
    }

    [Fact]
    public void DefaultInstallRootIsUnderLocalAppDataProgramsOView()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "O-view");

        Assert.Equal(expected, WindowsInstallKindSource.DefaultInstallRoot);
    }
}
