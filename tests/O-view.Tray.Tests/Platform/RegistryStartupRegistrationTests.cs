using Microsoft.Win32;
using OView.App;
using OView.Tray.Platform;

namespace OView.Tray.Tests.Platform;

/// <summary>
/// The Windows run-at-startup mechanism. Exercises a real HKCU round-trip against a
/// GUID-suffixed scratch subkey under <c>HKCU\Software</c>, never the real
/// <c>...\CurrentVersion\Run</c> key, so a test run never touches this machine's actual
/// startup programs.
/// </summary>
public class RegistryStartupRegistrationTests : IDisposable
{
    private readonly string _subKeyPath;

    public RegistryStartupRegistrationTests()
    {
        _subKeyPath = @"Software\OViewTests\" + Guid.NewGuid();
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_subKeyPath, throwOnMissingSubKey: false);
    }

    // Returned as the interface deliberately: Apply is a default interface method, so it is
    // reachable only through the contract — which is where that rule belongs, and how every
    // real caller holds it.
    private IStartupRegistration Subject(string? exe = @"C:\Program Files\O-view\o-view.exe") =>
        new RegistryStartupRegistration(_subKeyPath, "O-view", () => exe);

    [Fact]
    public void StartsDisabled()
    {
        Assert.False(Subject().IsEnabled());
    }

    [Fact]
    public void EnableWritesTheValueAndDisableRemovesIt()
    {
        var subject = Subject();

        Assert.True(subject.Enable());
        Assert.True(subject.IsEnabled());

        Assert.True(subject.Disable());
        Assert.False(subject.IsEnabled());
    }

    [Fact]
    public void DisableOnSomethingAlreadyAbsentIsSuccess()
    {
        // Deleting what is not there leaves the machine in the requested state, so it is
        // success — reporting failure would make the settings tick flip back for no reason.
        Assert.True(Subject().Disable());
    }

    [Fact]
    public void EnableFailsWhenTheExecutablePathIsUnknown()
    {
        var subject = Subject(exe: null);

        Assert.False(subject.Enable());
        Assert.False(subject.IsEnabled());
    }

    [Fact]
    public void ValueIsQuotedSoAPathWithSpacesStillLaunches()
    {
        new RegistryStartupRegistration(_subKeyPath, "O-view", () => @"C:\Program Files\O-view\o-view.exe").Enable();

        using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath);
        Assert.Equal("\"C:\\Program Files\\O-view\\o-view.exe\"", key?.GetValue("O-view"));
    }

    /// <summary>
    /// The shared rule from <see cref="IStartupRegistration.Apply"/>: report the state as it
    /// actually stands, never the state that was asked for.
    /// </summary>
    [Fact]
    public void ApplyReportsTheStateThatActuallyResulted()
    {
        Assert.True(Subject().Apply(true));
        Assert.False(Subject().Apply(false));

        // Enabling cannot succeed with no executable path, so Apply must answer false rather
        // than echoing the request back.
        Assert.False(Subject(exe: null).Apply(true));
    }

    [Fact]
    public void DefaultsMatchTheRealRunKey()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", RegistryStartupRegistration.DefaultSubKeyPath);
        Assert.Equal("O-view", RegistryStartupRegistration.DefaultValueName);
    }
}
