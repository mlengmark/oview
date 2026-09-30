using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OView.Core.Models;

namespace OView.Core.Storage;

/// <summary>
/// The evidence trail behind <see cref="OView.Core.Providers.Composite.CompositeUsageProvider"/>'s
/// <see cref="ProviderHealth"/> (ADR-0006 D1 "Ingest audit"): one row per provider recording its
/// last successful poll and its current consecutive-failure streak, so that history survives a
/// process restart instead of resetting to "never polled" every time the app starts.
///
/// <para><b>ADR-0006 D2 — this type never resolves its own path.</b> It takes a directory, never
/// calls <c>Environment.SpecialFolder</c>, reads no environment variable, and branches on no OS.
/// The shell (ADR-0007) decides where that directory is and hands it down.</para>
///
/// <para><b>ADR-0006 D3 — corruption degrades to "not known yet", never to a crash or a
/// guess.</b> Writes are atomic (temp file, then replace); a read that finds a missing or
/// unparseable file returns an empty audit trail rather than throwing or inventing history. A
/// file found corrupt on <see cref="ReadAll"/> is moved aside rather than left in place or
/// deleted (D3.3), and <see cref="State"/> reports whether that happened this session
/// (ADR-0006 D4).</para>
/// </summary>
public sealed class IngestAuditStore
{
    private const string FileName = "ingest-audit.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _directory;

    /// <param name="directory">The directory the store's file lives in. Never resolved
    /// internally (ADR-0006 D2) — the caller (ultimately the shell) decides where this is.</param>
    public IngestAuditStore(string directory)
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
    /// Every provider's audit record, keyed by <see cref="ProviderHealth.ProviderName"/>. Returns
    /// an empty dictionary when no file has ever been written, the file is missing, or the file
    /// could not be parsed. Never throws. A file found corrupt is moved aside (ADR-0006 D3.3)
    /// rather than left in place, and <see cref="State"/> is updated to reflect it.
    /// </summary>
    public IReadOnlyDictionary<string, IngestAuditRecord> ReadAll()
    {
        var path = Path.Combine(_directory, FileName);

        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, IngestAuditRecord>();
            }

            AuditFile? file;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file = JsonSerializer.Deserialize<AuditFile>(stream, SerializerOptions);
            }

            if (file?.Providers is null)
            {
                MarkCorrupt(path);
                return new Dictionary<string, IngestAuditRecord>();
            }

            var result = new Dictionary<string, IngestAuditRecord>();
            foreach (var entry in file.Providers)
            {
                if (entry.ProviderName is not { Length: > 0 } name)
                {
                    continue;
                }

                DateTimeOffset? lastSuccessAt = null;
                if (entry.LastSuccessUtc is { Length: > 0 } text &&
                    DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    lastSuccessAt = parsed;
                }

                result[name] = new IngestAuditRecord(lastSuccessAt, entry.ConsecutiveFailures);
            }

            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MarkCorrupt(path);
            return new Dictionary<string, IngestAuditRecord>();
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
    /// Writes the full audit trail atomically: a temp file is written and completed, then moved
    /// over the real path, so a process killed mid-write leaves the previous good file (or none),
    /// never a half-written one. Replaces the entire file — there is no partial-update path,
    /// mirroring <see cref="WeeklyResetAnchorStore.Save"/>.
    /// </summary>
    /// <returns>Whether the write succeeded.</returns>
    public bool Save(IReadOnlyDictionary<string, IngestAuditRecord> records)
    {
        var path = Path.Combine(_directory, FileName);
        var temp = path + ".tmp";

        var file = new AuditFile
        {
            Version = 1,
            Providers = records
                .Select(pair => new AuditEntry
                {
                    ProviderName = pair.Key,
                    LastSuccessUtc = pair.Value.LastSuccessAt?.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                    ConsecutiveFailures = pair.Value.ConsecutiveFailures,
                })
                .ToList(),
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

    private sealed class AuditFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("providers")] public List<AuditEntry>? Providers { get; set; }
    }

    private sealed class AuditEntry
    {
        [JsonPropertyName("providerName")] public string? ProviderName { get; set; }
        [JsonPropertyName("lastSuccessUtc")] public string? LastSuccessUtc { get; set; }
        [JsonPropertyName("consecutiveFailures")] public int ConsecutiveFailures { get; set; }
    }
}

/// <summary>
/// One provider's persisted audit trail (ADR-0006 D1) — the on-disk half of
/// <see cref="ProviderHealth"/>'s <c>LastSuccessAt</c>/<c>ConsecutiveFailures</c> fields.
/// </summary>
/// <param name="LastSuccessAt">The last time this provider polled successfully, or
/// <see langword="null"/> if it never has (or the audit trail does not go back far enough to
/// say).</param>
/// <param name="ConsecutiveFailures">How many polls in a row this provider has thrown, most
/// recent streak only, as of the last time this record was saved.</param>
public sealed record IngestAuditRecord(DateTimeOffset? LastSuccessAt, int ConsecutiveFailures);
