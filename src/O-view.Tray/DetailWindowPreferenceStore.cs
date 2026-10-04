using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OView.Tray;

/// <summary>
/// Persists the detail window's last dragged position (ADR-0008 slice 6, OVI-386) — a skin
/// perceptual preference, never Core data and never the shell's <c>ShellSettingsStore</c>
/// (ADR-0007 D4 reserves that file for shell-owned behaviour settings). This store, its file,
/// and its wording belong to this skin alone; <c>O-view.Linux</c>'s own last-position store
/// (slice 10) is a separate file with its own format, the same "each skin owns its own copy"
/// rule <see cref="Presentation.DetailWindowPlacement"/> already follows for first placement.
///
/// <para>Matches <c>WeeklyResetAnchorStore</c>'s degrade-rather-than-crash shape (ADR-0006
/// D3, referenced here only as a pattern — this type does not depend on O-view.Core): a
/// missing or unparseable file is not an error, it just means no position has been saved yet,
/// so <see cref="Load"/> returns <see langword="null"/> rather than throwing. A file found
/// corrupt is moved aside to <c>{file}.corrupt</c> rather than deleted or silently overwritten
/// (OVI-236/238's convention), so the next <see cref="Save"/> starts clean without destroying
/// the evidence. Writes are atomic (temp file, then replace).</para>
/// </summary>
public sealed class DetailWindowPreferenceStore
{
    private const string FileName = "detail-window.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    /// <param name="directory">The directory this preference file lives in. Never resolved
    /// internally — the caller decides where this is, same contract Core's own stores use.</param>
    public DetailWindowPreferenceStore(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        _directory = directory;
    }

    /// <summary>
    /// The last saved position, or <see langword="null"/> when no position has ever been
    /// saved or the file could not be parsed. Never throws. A file found corrupt is moved
    /// aside before returning <see langword="null"/>.
    /// </summary>
    public (double X, double Y)? Load()
    {
        var path = Path.Combine(_directory, FileName);

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            PositionFile? file;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file = JsonSerializer.Deserialize<PositionFile>(stream, SerializerOptions);
            }

            if (file is null)
            {
                MoveAside(path);
                return null;
            }

            return (file.X, file.Y);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MoveAside(path);
            return null;
        }
    }

    /// <summary>
    /// Writes the position atomically: a temp file is written and completed, then moved over
    /// the real path, so a process killed mid-write leaves the previous good file (or none),
    /// never a half-written one.
    /// </summary>
    /// <returns>Whether the write succeeded.</returns>
    public bool Save(double x, double y)
    {
        var path = Path.Combine(_directory, FileName);
        var temp = path + ".tmp";

        var file = new PositionFile { Version = 1, X = x, Y = y };

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

    /// <summary>
    /// Relocates a corrupt file to <c>{path}.corrupt</c> rather than deleting or overwriting
    /// it (OVI-236/238's convention). A fixed name, not a timestamped one — a second
    /// corruption overwrites the previous backup rather than accumulating a history this
    /// small preference does not need. Swallows its own failure: if even the move does not
    /// work, <see cref="Load"/> still returns <see langword="null"/> and the next
    /// <see cref="Save"/> simply overwrites the untrusted file.
    /// </summary>
    private static void MoveAside(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Move(path, path + ".corrupt", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class PositionFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
    }
}
