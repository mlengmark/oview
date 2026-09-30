using System.Globalization;
using Microsoft.Data.Sqlite;
using OView.Core.Providers.Jsonl;

namespace OView.Core.Storage;

/// <summary>
/// ADR-0006 D1's usage ledger: one SQLite row per vendor request (<c>usage.db</c>), the store
/// that survives the vendor's ~30-day transcript retention. Every field a request already
/// carries as a <see cref="TranscriptRecord"/> — no conversation content is added (D5).
///
/// <para><b>Upsert is the only write path (D1's confirmed design).</b> <see cref="Upsert"/> and
/// <see cref="UpsertRange"/> key on <see cref="TranscriptRecord.RequestId"/> and always write
/// via <c>INSERT ... ON CONFLICT DO UPDATE</c>, never a blind <c>INSERT</c>. That single
/// mechanism is simultaneously de-duplication and idempotency: re-ingesting a transcript
/// rewrites identical rows rather than duplicating them, and when a request appears more than
/// once in one ingest, the last occurrence in file order wins because each later call
/// overwrites the row the earlier call wrote.</para>
///
/// <para><b>Local-day bucketing is query-time only (D1).</b> <see cref="QueryDailyUsage"/>
/// derives each row's local date from its stored UTC timestamp at query time, against the
/// caller-supplied <see cref="TimeZoneInfo"/> — no <c>utc_date</c> column exists to disagree
/// with it, and no rollup is persisted (source issue #211: a stored UTC date cannot answer for
/// a local day that straddles two UTC days).</para>
///
/// <para><b>ADR-0006 D2 — this type never resolves its own path.</b> It takes a directory, like
/// <see cref="WeeklyResetAnchorStore"/>; the shell (ADR-0007) decides where that directory is.
/// Corrupt-database handling (D3) and <c>HistoryStoreState</c> (D4) are slice 3's job, not
/// this one's.</para>
/// </summary>
public sealed class UsageLedgerStore
{
    private const string FileName = "usage.db";

    private readonly string _connectionString;

    /// <param name="directory">The directory the store's database file lives in. Never resolved
    /// internally (ADR-0006 D2) — the caller (ultimately the shell) decides where this is.</param>
    public UsageLedgerStore(string directory)
    {
        Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, FileName),
            Pooling = false,
        }.ToString();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_requests (
                request_id TEXT PRIMARY KEY,
                timestamp_utc TEXT NOT NULL,
                model TEXT NOT NULL,
                input_tokens INTEGER NOT NULL,
                output_tokens INTEGER NOT NULL,
                cache_creation_input_tokens INTEGER NOT NULL,
                cache_read_input_tokens INTEGER NOT NULL,
                cache_creation_ephemeral_5m_tokens INTEGER NULL,
                cache_creation_ephemeral_1h_tokens INTEGER NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Upserts one request. Re-running this with the same <see cref="TranscriptRecord.RequestId"/>
    /// rewrites the row rather than duplicating it.
    /// </summary>
    public void Upsert(TranscriptRecord record)
    {
        using var connection = OpenConnection();
        UpsertOn(connection, transaction: null, record);
    }

    /// <summary>
    /// Upserts every record in one transaction, in the order given. When the same
    /// <see cref="TranscriptRecord.RequestId"/> appears more than once, the last one in
    /// <paramref name="records"/> wins — the file-order overwrite semantics a transcript's own
    /// streaming duplicates rely on.
    /// </summary>
    public void UpsertRange(IEnumerable<TranscriptRecord> records)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var record in records)
        {
            UpsertOn(connection, transaction, record);
        }

        transaction.Commit();
    }

    /// <summary>
    /// Aggregates every stored row into (local date × model) buckets, computed fresh on every
    /// call against <paramref name="zone"/> (D1 — no rollup is ever persisted). An empty store
    /// yields an empty list.
    /// </summary>
    public IReadOnlyList<DailyModelUsage> QueryDailyUsage(TimeZoneInfo zone)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp_utc, model, input_tokens, output_tokens,
                   cache_creation_input_tokens, cache_read_input_tokens,
                   cache_creation_ephemeral_5m_tokens, cache_creation_ephemeral_1h_tokens
            FROM usage_requests;
            """;

        var buckets = new Dictionary<(DateOnly LocalDate, string Model), Bucket>();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var timestampUtc = DateTimeOffset.Parse(
                reader.GetString(0),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var model = reader.GetString(1);

            var localInstant = TimeZoneInfo.ConvertTime(timestampUtc, zone);
            var key = (LocalDate: DateOnly.FromDateTime(localInstant.DateTime), Model: model);

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket();
                buckets[key] = bucket;
            }

            bucket.RequestCount++;
            bucket.InputTokens += reader.GetInt64(2);
            bucket.OutputTokens += reader.GetInt64(3);
            bucket.CacheCreationInputTokens += reader.GetInt64(4);
            bucket.CacheReadInputTokens += reader.GetInt64(5);
            bucket.CacheCreationEphemeral5mTokens += reader.IsDBNull(6) ? 0 : reader.GetInt64(6);
            bucket.CacheCreationEphemeral1hTokens += reader.IsDBNull(7) ? 0 : reader.GetInt64(7);
        }

        return buckets
            .Select(pair => new DailyModelUsage(
                pair.Key.LocalDate,
                pair.Key.Model,
                pair.Value.RequestCount,
                pair.Value.InputTokens,
                pair.Value.OutputTokens,
                pair.Value.CacheCreationInputTokens,
                pair.Value.CacheReadInputTokens,
                pair.Value.CacheCreationEphemeral5mTokens,
                pair.Value.CacheCreationEphemeral1hTokens))
            .OrderBy(usage => usage.LocalDate)
            .ThenBy(usage => usage.Model, StringComparer.Ordinal)
            .ToList();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void UpsertOn(SqliteConnection connection, SqliteTransaction? transaction, TranscriptRecord record)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO usage_requests (
                request_id, timestamp_utc, model,
                input_tokens, output_tokens,
                cache_creation_input_tokens, cache_read_input_tokens,
                cache_creation_ephemeral_5m_tokens, cache_creation_ephemeral_1h_tokens)
            VALUES (
                $requestId, $timestampUtc, $model,
                $inputTokens, $outputTokens,
                $cacheCreationInputTokens, $cacheReadInputTokens,
                $cacheCreationEphemeral5mTokens, $cacheCreationEphemeral1hTokens)
            ON CONFLICT(request_id) DO UPDATE SET
                timestamp_utc = excluded.timestamp_utc,
                model = excluded.model,
                input_tokens = excluded.input_tokens,
                output_tokens = excluded.output_tokens,
                cache_creation_input_tokens = excluded.cache_creation_input_tokens,
                cache_read_input_tokens = excluded.cache_read_input_tokens,
                cache_creation_ephemeral_5m_tokens = excluded.cache_creation_ephemeral_5m_tokens,
                cache_creation_ephemeral_1h_tokens = excluded.cache_creation_ephemeral_1h_tokens;
            """;

        command.Parameters.AddWithValue("$requestId", record.RequestId);
        command.Parameters.AddWithValue(
            "$timestampUtc",
            record.TimestampUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$model", record.Model);
        command.Parameters.AddWithValue("$inputTokens", record.Tokens.InputTokens);
        command.Parameters.AddWithValue("$outputTokens", record.Tokens.OutputTokens);
        command.Parameters.AddWithValue("$cacheCreationInputTokens", record.Tokens.CacheCreationInputTokens);
        command.Parameters.AddWithValue("$cacheReadInputTokens", record.Tokens.CacheReadInputTokens);
        command.Parameters.AddWithValue(
            "$cacheCreationEphemeral5mTokens",
            (object?)record.Tokens.CacheCreationEphemeral5mTokens ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$cacheCreationEphemeral1hTokens",
            (object?)record.Tokens.CacheCreationEphemeral1hTokens ?? DBNull.Value);

        command.ExecuteNonQuery();
    }

    private sealed class Bucket
    {
        public int RequestCount;
        public long InputTokens;
        public long OutputTokens;
        public long CacheCreationInputTokens;
        public long CacheReadInputTokens;
        public long CacheCreationEphemeral5mTokens;
        public long CacheCreationEphemeral1hTokens;
    }
}
