using OView.Tray;

namespace OView.Tray.Tests;

/// <summary>
/// Proves <see cref="DetailWindowPreferenceStore"/>'s contract (ADR-0008 slice 6, OVI-386):
/// a directory-injected, skin-owned preference file that writes atomically and never throws
/// on a missing or corrupt file — the same degrade-rather-than-crash shape Core's own stores
/// use, and the move-aside-not-overwrite convention OVI-236/238 established.
/// </summary>
public sealed class DetailWindowPreferenceStoreTests : IDisposable
{
    private readonly string _directory;

    public DetailWindowPreferenceStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ovi386-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_returns_null_when_no_file_has_ever_been_written()
    {
        var store = new DetailWindowPreferenceStore(_directory);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_returns_null_when_the_directory_itself_does_not_exist()
    {
        var store = new DetailWindowPreferenceStore(Path.Combine(_directory, "does-not-exist"));

        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveThenLoadRoundTripsTheSamePosition()
    {
        var store = new DetailWindowPreferenceStore(_directory);

        var saved = store.Save(123.5, -40);

        Assert.True(saved);
        Assert.Equal((123.5, -40), store.Load());
    }

    [Fact]
    public void Save_creates_the_directory_when_it_does_not_already_exist()
    {
        var nested = Path.Combine(_directory, "nested", "path");
        var store = new DetailWindowPreferenceStore(nested);

        var saved = store.Save(1, 2);

        Assert.True(saved);
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void Save_overwrites_a_previously_stored_position()
    {
        var store = new DetailWindowPreferenceStore(_directory);

        store.Save(1, 1);
        store.Save(2, 2);

        Assert.Equal((2, 2), store.Load());
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind_after_a_successful_write()
    {
        var store = new DetailWindowPreferenceStore(_directory);

        store.Save(1, 1);

        Assert.False(File.Exists(Path.Combine(_directory, "detail-window.json.tmp")));
    }

    [Fact]
    public void Load_returns_null_and_never_throws_when_the_file_is_unparseable_json()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "detail-window.json"), "{ this is not valid json");
        var store = new DetailWindowPreferenceStore(_directory);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_moves_an_unparseable_file_aside_rather_than_leaving_or_deleting_it()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "detail-window.json");
        File.WriteAllText(path, "{ this is not valid json");
        var store = new DetailWindowPreferenceStore(_directory);

        store.Load();

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".corrupt"));
        Assert.Equal("{ this is not valid json", File.ReadAllText(path + ".corrupt"));
    }

    [Fact]
    public void Save_recovers_after_a_previous_corrupt_file_was_moved_aside()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "detail-window.json"), "{ not valid json at all");
        var store = new DetailWindowPreferenceStore(_directory);

        store.Load();
        store.Save(5, 6);

        Assert.Equal((5, 6), store.Load());
    }
}
