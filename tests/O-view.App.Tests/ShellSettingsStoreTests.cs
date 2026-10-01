namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0007 D4's shell settings file: happy-path load/save round trip, missing-file-
/// uses-defaults, and a corrupt file degrading to defaults rather than throwing (same shape as
/// <c>WeeklyResetAnchorStore</c>'s corruption handling). Each test uses its own temp directory
/// under the OS temp folder, never a real user profile path.
/// </summary>
public class ShellSettingsStoreTests : IDisposable
{
    private readonly string _directory;

    public ShellSettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "oview-shell-settings-tests-" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_returns_defaults_when_no_file_exists_yet()
    {
        var store = new ShellSettingsStore(_directory);

        var settings = store.Load();

        Assert.Equal(ShellSettings.Default, settings);
    }

    [Fact]
    public void Save_then_Load_round_trips_the_settings()
    {
        var store = new ShellSettingsStore(_directory);
        var settings = new ShellSettings(
            AlertThresholdPercent: 90,
            PollCadence: TimeSpan.FromSeconds(30),
            AutoUpdateEnabled: true);

        var saved = store.Save(settings);
        var loaded = store.Load();

        Assert.True(saved);
        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void Save_creates_the_directory_when_it_does_not_exist()
    {
        Assert.False(Directory.Exists(_directory));
        var store = new ShellSettingsStore(_directory);

        store.Save(ShellSettings.Default);

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public void Save_overwrites_a_previously_saved_file()
    {
        var store = new ShellSettingsStore(_directory);
        store.Save(new ShellSettings(80, TimeSpan.FromSeconds(60), false));

        store.Save(new ShellSettings(50, TimeSpan.FromSeconds(120), true));
        var loaded = store.Load();

        Assert.Equal(new ShellSettings(50, TimeSpan.FromSeconds(120), true), loaded);
    }

    [Fact]
    public void Load_returns_defaults_when_the_file_is_not_valid_json()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not valid json");
        var store = new ShellSettingsStore(_directory);

        var settings = store.Load();

        Assert.Equal(ShellSettings.Default, settings);
    }

    [Fact]
    public void Load_returns_defaults_when_the_poll_cadence_is_zero_or_negative()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "settings.json"),
            """{ "version": 1, "alertThresholdPercent": 90, "pollCadenceSeconds": 0, "autoUpdateEnabled": true }""");
        var store = new ShellSettingsStore(_directory);

        var settings = store.Load();

        Assert.Equal(ShellSettings.Default, settings);
    }
}
