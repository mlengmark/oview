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
    public void Save_then_Load_round_trips_a_non_null_last_announced_update_tag()
    {
        var store = new ShellSettingsStore(_directory);
        var settings = new ShellSettings(80, TimeSpan.FromSeconds(60), true, "v1.2.3");

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal("v1.2.3", loaded.LastAnnouncedUpdateTag);
    }

    [Fact]
    public void Load_defaults_the_last_announced_update_tag_to_null_for_a_file_written_before_this_field_existed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "settings.json"),
            """{ "version": 1, "alertThresholdPercent": 80, "pollCadenceSeconds": 60, "autoUpdateEnabled": true }""");
        var store = new ShellSettingsStore(_directory);

        var settings = store.Load();

        Assert.Null(settings.LastAnnouncedUpdateTag);
        Assert.True(settings.AutoUpdateEnabled);
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

    [Fact]
    public void Load_moves_an_unparseable_file_aside_rather_than_leaving_it_in_place()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ not valid json");
        var store = new ShellSettingsStore(_directory);

        store.Load();

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".corrupt"));
        Assert.Equal("{ not valid json", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void Load_moves_a_file_with_an_invalid_poll_cadence_aside()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(
            path,
            """{ "version": 1, "alertThresholdPercent": 90, "pollCadenceSeconds": -5, "autoUpdateEnabled": true }""");
        var store = new ShellSettingsStore(_directory);

        store.Load();

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void A_second_corruption_overwrites_the_previous_corrupt_backup_rather_than_accumulating()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        var store = new ShellSettingsStore(_directory);

        File.WriteAllText(path, "{ first corrupt");
        store.Load();
        File.WriteAllText(path, "{ second corrupt");
        store.Load();

        Assert.Equal("{ second corrupt", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void A_missing_file_is_not_treated_as_corrupt()
    {
        var store = new ShellSettingsStore(_directory);

        store.Load();

        Assert.False(File.Exists(Path.Combine(_directory, "settings.json") + ".corrupt"));
    }

    [Fact]
    public void Save_after_a_corrupt_load_writes_a_fresh_file_without_resurrecting_the_corrupt_one()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ not valid json");
        var store = new ShellSettingsStore(_directory);
        store.Load();

        var saved = store.Save(new ShellSettings(70, TimeSpan.FromSeconds(90), true));
        var loaded = store.Load();

        Assert.True(saved);
        Assert.Equal(new ShellSettings(70, TimeSpan.FromSeconds(90), true), loaded);
        Assert.True(File.Exists(path + ".corrupt"));
    }
}
