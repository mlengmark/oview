using System.Text.Json;
using System.Text.Json.Serialization;

namespace OView.App;

/// <summary>
/// Persists <see cref="ShellSettings"/> to the one settings file ADR-0007 D4 gives the shell
/// — behaviour settings only, never the usage ledger or weekly-reset anchor (Core's stores,
/// ADR-0006), never run-at-startup (the OS owns that truth, never mirrored), and never a
/// per-skin perceptual preference (each skin's own file). Lives in the same directory as
/// Core's stores (<see cref="StoreDirectoryResolver"/>) but as its own file, so a corrupt or
/// missing settings file never affects the ledger or anchor and vice versa.
///
/// <para>Matches <see cref="OView.Core.Storage.WeeklyResetAnchorStore"/>'s degrade-rather-
/// than-crash shape: a missing or unparseable file is not an error, it just means no settings
/// have been saved yet, so <see cref="Load"/> returns <see cref="ShellSettings.Default"/>
/// rather than throwing. Writes are atomic (temp file, then replace) for the same reason
/// Core's stores are.</para>
/// </summary>
public sealed class ShellSettingsStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    /// <param name="directory">The directory the settings file lives in. Never resolved
    /// internally — the caller (ultimately the shell, via
    /// <see cref="StoreDirectoryResolver"/>) decides where this is, same contract as
    /// <see cref="OView.Core.Storage.WeeklyResetAnchorStore"/>.</param>
    public ShellSettingsStore(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        _directory = directory;
    }

    /// <summary>
    /// The persisted settings, or <see cref="ShellSettings.Default"/> when no file exists yet
    /// or the file could not be parsed. Never throws.
    /// </summary>
    public ShellSettings Load()
    {
        var path = Path.Combine(_directory, FileName);

        try
        {
            if (!File.Exists(path))
            {
                return ShellSettings.Default;
            }

            SettingsFile? file;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file = JsonSerializer.Deserialize<SettingsFile>(stream, SerializerOptions);
            }

            if (file is null || file.PollCadenceSeconds <= 0)
            {
                return ShellSettings.Default;
            }

            return new ShellSettings(
                file.AlertThresholdPercent,
                TimeSpan.FromSeconds(file.PollCadenceSeconds),
                file.AutoUpdateEnabled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return ShellSettings.Default;
        }
    }

    /// <summary>
    /// Writes <paramref name="settings"/> atomically: a temp file is written and completed,
    /// then moved over the real path, so a process killed mid-write leaves the previous good
    /// file (or none), never a half-written one.
    /// </summary>
    /// <returns>Whether the write succeeded.</returns>
    public bool Save(ShellSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var path = Path.Combine(_directory, FileName);
        var temp = path + ".tmp";

        var file = new SettingsFile
        {
            Version = 1,
            AlertThresholdPercent = settings.AlertThresholdPercent,
            PollCadenceSeconds = (int)settings.PollCadence.TotalSeconds,
            AutoUpdateEnabled = settings.AutoUpdateEnabled,
        };

        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(temp, JsonSerializer.Serialize(file, SerializerOptions));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }

    private sealed class SettingsFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("alertThresholdPercent")] public int AlertThresholdPercent { get; set; }
        [JsonPropertyName("pollCadenceSeconds")] public int PollCadenceSeconds { get; set; }
        [JsonPropertyName("autoUpdateEnabled")] public bool AutoUpdateEnabled { get; set; }
    }
}
