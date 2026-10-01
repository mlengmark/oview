using OView.App;
using OView.Linux.Platform;

namespace OView.Linux.Tests.Platform;

/// <summary>
/// The Linux run-at-startup mechanism. Nothing here needs a Linux API — it is a text file in
/// a directory — so it runs wherever <c>dotnet test</c> runs. It does not confirm that a real
/// desktop session honours the file, which this repository has never had a Linux runner to
/// observe (ADR-0002/0004's "never observed" caveat).
/// </summary>
public class XdgAutostartRegistrationTests : IDisposable
{
    private readonly string _directory;

    public XdgAutostartRegistrationTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "oview-xdg-autostart-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // Returned as the interface deliberately: Apply is a default interface method, so it is
    // reachable only through the contract — which is where that rule belongs, and how every
    // real caller holds it.
    private IStartupRegistration Subject(string? exe = "/usr/bin/o-view") =>
        new XdgAutostartRegistration(_directory, () => exe);

    private string FilePath => Path.Combine(_directory, "o-view.desktop");

    [Fact]
    public void StartsDisabled()
    {
        Assert.False(Subject().IsEnabled());
    }

    [Fact]
    public void EnableWritesTheDesktopFileAndDisableRemovesIt()
    {
        var subject = Subject();

        Assert.True(subject.Enable());
        Assert.True(subject.IsEnabled());
        Assert.True(File.Exists(FilePath));

        Assert.True(subject.Disable());
        Assert.False(subject.IsEnabled());
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void EnableCreatesTheAutostartDirectoryWhenAbsent()
    {
        var nested = Path.Combine(_directory, "config", "autostart");
        var subject = new XdgAutostartRegistration(nested, () => "/usr/bin/o-view");

        Assert.True(subject.Enable());
        Assert.True(subject.IsEnabled());
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
    public void EntryCarriesTheFieldsAutostartActuallyNeeds()
    {
        Subject().Enable();

        var text = File.ReadAllText(FilePath);

        Assert.StartsWith("[Desktop Entry]", text, StringComparison.Ordinal);
        Assert.Contains("Type=Application", text, StringComparison.Ordinal);
        Assert.Contains("Name=O-view", text, StringComparison.Ordinal);
        Assert.Contains("Terminal=false", text, StringComparison.Ordinal);
        // GNOME honours this to disable an entry without deleting it; an entry without it can
        // be ignored by the session while this class still reports "enabled".
        Assert.Contains("X-GNOME-Autostart-enabled=true", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecIsQuotedSoAPathWithSpacesStillLaunches()
    {
        new XdgAutostartRegistration(_directory, () => "/opt/My Apps/o-view").Enable();

        var text = File.ReadAllText(FilePath);

        Assert.Contains("Exec=\"/opt/My Apps/o-view\"", text, StringComparison.Ordinal);
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
}
