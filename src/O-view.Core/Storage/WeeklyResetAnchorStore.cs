using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OView.Core.Models;

namespace OView.Core.Storage;

/// <summary>
/// The one weekly-reset instant Core has ever been told, kept so it does not have to be
/// told again (ADR-0006 D1). Preserves the source repository's confirmed
/// <c>WeeklyResetAnchor</c> behaviour (<c>897777b</c>): the vendor's cached reset report is
/// stale for long stretches between refreshes — 43 hours, measured on the development
/// machine — so a value read only while fresh is usually absent. Read once and stored, it is
/// correct until the schedule itself changes.
///
/// <para><b>ADR-0006 D2 — this type never resolves its own path.</b> It takes a directory,
/// never calls <c>Environment.SpecialFolder</c>, reads no environment variable, and branches
/// on no OS. The shell (ADR-0007) decides where that directory is and hands it down, which is
/// also what keeps this type testable without a real user profile.</para>
///
/// <para><b>ADR-0006 D3 — corruption degrades to "not known yet", never to a crash or a
/// guess.</b> Writes are atomic (temp file, then replace); a read that finds a missing or
/// unparseable file returns <see langword="null"/> rather than throwing or inventing a value.
/// A file found corrupt on <see cref="Read"/> is moved aside rather than left in place or
/// deleted (D3.3), and <see cref="State"/> reports whether that happened this session
/// (ADR-0006 D4, OVI-236).</para>
/// </summary>
public sealed class WeeklyResetAnchorStore
{
    private const string FileName = "weekly-reset.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _directory;

    /// <param name="directory">The directory the store's file lives in. Never resolved
    /// internally (ADR-0006 D2) — the caller (ultimately the shell) decides where this is.</param>
    public WeeklyResetAnchorStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Whether this store was readable and writable this session (<see cref="HistoryStoreState.Ok"/>),
    /// found corrupt and moved aside in favour of a fresh empty store
    /// (<see cref="HistoryStoreState.Rebuilt"/>), or still cannot be trusted after that recovery
    /// attempt (<see cref="HistoryStoreState.Unavailable"/>) — ADR-0006 D4.
    /// </summary>
    public HistoryStoreState State { get; private set; } = HistoryStoreState.Ok;

    /// <summary>
    /// The stored weekly-reset instant, or <see langword="null"/> when none has been stored,
    /// the file is missing, or the file could not be parsed. Never throws. A file found corrupt
    /// is moved aside (ADR-0006 D3.3) rather than left in place, and <see cref="State"/> is
    /// updated to reflect it.
    /// </summary>
    public DateTimeOffset? Read()
    {
        var path = Path.Combine(_directory, FileName);

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            AnchorFile? file;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file = JsonSerializer.Deserialize<AnchorFile>(stream, SerializerOptions);
            }

            if (file?.AnchorUtc is not { Length: > 0 } text ||
                !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var anchor))
            {
                MarkCorrupt(path);
                return null;
            }

            return anchor;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MarkCorrupt(path);
            return null;
        }
    }

    private void MarkCorrupt(string path)
    {
        if (State == HistoryStoreState.Unavailable)
        {
            return;
        }

        State = CorruptStoreRecovery.MoveAsideIfPresent(path);
    }

    /// <summary>
    /// Writes <paramref name="anchorUtc"/> atomically: a temp file is written and completed,
    /// then moved over the real path, so a process killed mid-write leaves the previous good
    /// file (or none), never a half-written one.
    /// </summary>
    /// <returns>Whether the write succeeded.</returns>
    public bool Save(DateTimeOffset anchorUtc)
    {
        var path = Path.Combine(_directory, FileName);
        var temp = path + ".tmp";

        var file = new AnchorFile
        {
            Version = 1,
            AnchorUtc = anchorUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
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

    private sealed class AnchorFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("anchorUtc")] public string? AnchorUtc { get; set; }
    }
}
