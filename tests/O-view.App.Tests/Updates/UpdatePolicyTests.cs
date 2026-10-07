using System.Runtime.InteropServices;
using OView.App.Updates;
using OView.Core.Updates;

namespace OView.App.Tests.Updates;

/// <summary>
/// What each kind of install is allowed to do about an update (ADR-0010 D1, D2). The two
/// mandatory tests here are <see cref="DetectionNeverYieldsNoneOnAPublishedArchitecture"/>
/// and <see cref="MayDownloadAndRunIsTrueForExactlyWindowsInstaller"/> — ADR-0010 requires
/// both before any later slice may download anything.
/// </summary>
public class UpdatePolicyTests
{
    public static IEnumerable<object[]> AllKinds() =>
        Enum.GetValues<InstallKind>().Select(kind => new object[] { kind });

    /// <summary>
    /// ADR-0010 D2's first mandatory invariant: for every install kind, on a published
    /// architecture, detection must find an asset — the source's shipped bug was an apt
    /// build handed <see cref="ReleaseAssets.None"/> and so it could never report an update
    /// at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DetectionNeverYieldsNoneOnAPublishedArchitecture(InstallKind kind)
    {
        var selector = UpdatePolicy.DetectionAsset(kind, Architecture.X64);

        Assert.NotSame(ReleaseAssets.None, selector);
    }

    /// <summary>
    /// ADR-0010 D2's second mandatory invariant, pinned as one assertion over the whole
    /// truth table rather than per-kind checks — a weaker test would re-prove only the
    /// source's one historical bug (LinuxPackage) while leaving WindowsPortable and
    /// LinuxTarball free to be granted permission by a later edit.
    /// </summary>
    [Fact]
    public void MayDownloadAndRunIsTrueForExactlyWindowsInstaller()
    {
        foreach (var kind in Enum.GetValues<InstallKind>())
        {
            Assert.Equal(kind == InstallKind.WindowsInstaller, UpdatePolicy.MayDownloadAndRun(kind));
        }
    }

    [Fact]
    public void OnlyAWindowsInstallerBuildReplacesItselfInPlace()
    {
        Assert.Equal(UpdateAction.InstallInPlace, UpdatePolicy.ActionFor(InstallKind.WindowsInstaller));
    }

    /// <summary>The acceptance criterion, stated directly: an apt build never downloads or executes anything.</summary>
    [Fact]
    public void AnAptInstalledBuildNeverDownloadsOrRuns()
    {
        Assert.Equal(UpdateAction.DeferToPackageManager, UpdatePolicy.ActionFor(InstallKind.LinuxPackage));
        Assert.False(UpdatePolicy.MayDownloadAndRun(InstallKind.LinuxPackage));
    }

    [Theory]
    [InlineData(InstallKind.WindowsPortable)]
    [InlineData(InstallKind.LinuxTarball)]
    public void UserOwnedBuildsAreSentToTheReleasePage(InstallKind kind)
    {
        Assert.Equal(UpdateAction.OpenReleasePage, UpdatePolicy.ActionFor(kind));
        Assert.False(UpdatePolicy.MayDownloadAndRun(kind));
    }

    [Theory]
    [InlineData(InstallKind.WindowsInstaller)]
    [InlineData(InstallKind.WindowsPortable)]
    public void BothWindowsKindsDetectOnTheInstaller(InstallKind kind)
    {
        var selector = UpdatePolicy.DetectionAsset(kind, Architecture.X64);

        Assert.True(selector.Matches(ReleaseAssets.WindowsInstallerName));
    }

    [Theory]
    [InlineData(Architecture.X64, "amd64")]
    [InlineData(Architecture.Arm64, "arm64")]
    public void LinuxPackageDetectsItsDebianArchitecture(Architecture architecture, string debianArchitecture)
    {
        var selector = UpdatePolicy.DetectionAsset(InstallKind.LinuxPackage, architecture);

        Assert.True(selector.Matches($"o-view_0.6.0_{debianArchitecture}.deb"));
    }

    [Theory]
    [InlineData(Architecture.X64, "linux-x64")]
    [InlineData(Architecture.Arm64, "linux-arm64")]
    public void LinuxTarballDetectsItsRuntimeIdentifier(Architecture architecture, string runtimeIdentifier)
    {
        var selector = UpdatePolicy.DetectionAsset(InstallKind.LinuxTarball, architecture);

        Assert.True(selector.Matches($"o-view-0.6.0-{runtimeIdentifier}.tar.gz"));
    }

    /// <summary>
    /// An architecture this project does not publish for yields <see cref="ReleaseAssets.None"/>
    /// rather than pointing a user at a package that would not run on their machine.
    /// </summary>
    [Theory]
    [InlineData(InstallKind.LinuxPackage)]
    [InlineData(InstallKind.LinuxTarball)]
    public void AnUnpublishedArchitectureYieldsNone(InstallKind kind)
    {
        var selector = UpdatePolicy.DetectionAsset(kind, Architecture.Arm);

        Assert.Same(ReleaseAssets.None, selector);
    }
}
