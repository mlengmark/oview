using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace OView.Core.Providers.Jsonl;

/// <summary>
/// Pure string-in, record-out parsing for one line of a Claude transcript, and
/// de-duplication of a set of parsed records (ADR-0005 D2, slice 3a). No file I/O, no clock
/// read, no pricing — turning a resolved root into files to read is slice 3b.
/// </summary>
public static class TranscriptParser
{
    /// <summary>
    /// Claude Code's marker for a locally generated assistant message — an interruption, an
    /// error, a "prompt too long" notice, not a model and not an API call. Compared
    /// case-insensitively, and only here: the source repository carried three separate
    /// <c>&lt;synthetic&gt;</c> branches that diverged on case sensitivity, so an upstream
    /// casing change would have silently swapped which one was live (GitHub issue #57).
    /// Dropped rather than stored at zero — every synthetic record measured carries
    /// all-zero usage, so storing it adds nothing to a total while inflating a request
    /// count with messages no model produced.
    /// </summary>
    private const string SyntheticModel = "<synthetic>";

    /// <summary>
    /// Parses one transcript line into a <see cref="TranscriptRecord"/>. Returns
    /// <see langword="false"/> — never throws — for malformed JSON, a record that is not an
    /// assistant record, a missing or empty <c>requestId</c>, a missing or unparseable
    /// <c>timestamp</c>, a missing <c>message.usage</c> block, and a record whose model is
    /// the synthetic marker.
    /// </summary>
    public static bool TryParseAssistantRecord(string line, [NotNullWhen(true)] out TranscriptRecord? record)
    {
        record = null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                type.GetString() != "assistant")
            {
                return false;
            }

            if (!root.TryGetProperty("requestId", out var requestIdElement) ||
                requestIdElement.ValueKind != JsonValueKind.String ||
                requestIdElement.GetString() is not { Length: > 0 } requestId)
            {
                return false;
            }

            if (!root.TryGetProperty("timestamp", out var timestampElement) ||
                timestampElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
            {
                return false;
            }

            if (!root.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("usage", out var usage) ||
                usage.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var model = message.TryGetProperty("model", out var modelElement) &&
                modelElement.ValueKind == JsonValueKind.String &&
                modelElement.GetString() is { } modelValue
                    ? modelValue
                    : "unknown";

            if (model.Equals(SyntheticModel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            record = new TranscriptRecord(requestId, timestamp, model, ReadTokens(usage));
            return true;
        }
        catch (JsonException)
        {
            // Truncated final line (Claude appends while we read) or malformed content —
            // skip, never fatal.
            return false;
        }
    }

    /// <summary>
    /// Groups <paramref name="recordsInFileOrder"/> by <see cref="TranscriptRecord.RequestId"/>
    /// and keeps only the last occurrence in file order. Not an optimisation: the same
    /// <c>requestId</c> appears many times as a response streams (28 records / 12 ids
    /// measured), and summing every occurrence overcounts by roughly 2.3× (CONFIRMED —
    /// <c>docs/findings/jsonl-schema.md</c> in the source repository). Transcripts are
    /// append-ordered, so on a timestamp tie the last line wins.
    /// </summary>
    public static IReadOnlyList<TranscriptRecord> Deduplicate(IReadOnlyList<TranscriptRecord> recordsInFileOrder)
    {
        var lastByRequestId = new Dictionary<string, TranscriptRecord>();

        foreach (var record in recordsInFileOrder)
        {
            lastByRequestId[record.RequestId] = record;
        }

        var deduplicated = new List<TranscriptRecord>(lastByRequestId.Count);
        var seen = new HashSet<string>();

        foreach (var record in recordsInFileOrder)
        {
            if (seen.Add(record.RequestId))
            {
                deduplicated.Add(lastByRequestId[record.RequestId]);
            }
        }

        return deduplicated;
    }

    private static TranscriptTokens ReadTokens(JsonElement usage)
    {
        long? write5m = null;
        long? write1h = null;

        if (usage.TryGetProperty("cache_creation", out var split) &&
            split.ValueKind == JsonValueKind.Object)
        {
            write5m = ReadTokenField(split, "ephemeral_5m_input_tokens");
            write1h = ReadTokenField(split, "ephemeral_1h_input_tokens");
        }

        return new TranscriptTokens(
            ReadTokenField(usage, "input_tokens") ?? 0,
            ReadTokenField(usage, "output_tokens") ?? 0,
            ReadTokenField(usage, "cache_creation_input_tokens") ?? 0,
            ReadTokenField(usage, "cache_read_input_tokens") ?? 0,
            write5m,
            write1h);
    }

    /// <summary>Absent or non-numeric token fields mean none reported; null, not an error.</summary>
    private static long? ReadTokenField(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var value) && value >= 0
            ? value
            : null;
}
