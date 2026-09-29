using OView.Core.Providers.Jsonl;

namespace OView.Core.Tests.Providers.Jsonl;

/// <summary>
/// Proves ADR-0005 D2 slice 3a's parsing and de-duplication rules with string fixtures
/// only — no test in this file touches the real filesystem (ADR-0005 D2, slice 3a).
/// </summary>
public class TranscriptParserTests
{
    [Fact]
    public void WellFormedAssistantRecordParses()
    {
        var line = AssistantLine(
            requestId: "req_well_formed",
            timestamp: "2026-07-20T12:58:16.640Z",
            model: "claude-opus-4-8",
            inputTokens: 2,
            outputTokens: 120,
            cacheCreationInputTokens: 14226,
            cacheReadInputTokens: 25061,
            ephemeral5m: 0,
            ephemeral1h: 16719);

        var parsed = TranscriptParser.TryParseAssistantRecord(line, out var record);

        Assert.True(parsed);
        Assert.Equal("req_well_formed", record!.RequestId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-20T12:58:16.640Z"), record.TimestampUtc);
        Assert.Equal("claude-opus-4-8", record.Model);
        Assert.Equal(2, record.Tokens.InputTokens);
        Assert.Equal(120, record.Tokens.OutputTokens);
        Assert.Equal(14226, record.Tokens.CacheCreationInputTokens);
        Assert.Equal(25061, record.Tokens.CacheReadInputTokens);
        Assert.Equal(0, record.Tokens.CacheCreationEphemeral5mTokens);
        Assert.Equal(16719, record.Tokens.CacheCreationEphemeral1hTokens);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"type\":\"assistant\",\"requestId\":\"req_1\",\"timestamp\":\"2026-07-20T12:58:16.640Z\",\"message\":{\"model\":\"claude-opus-4-8\",\"usage\":{\"input_tokens\":1")]
    public void MalformedOrTruncatedLineReturnsFalseWithoutThrowing(string line)
    {
        var parsed = TranscriptParser.TryParseAssistantRecord(line, out var record);

        Assert.False(parsed);
        Assert.Null(record);
    }

    [Fact]
    public void UnknownRecordTypeIsSkipped()
    {
        var line = """{"type":"user","requestId":"req_1","timestamp":"2026-07-20T12:58:16.640Z","message":{"model":"claude-opus-4-8","usage":{"input_tokens":1,"output_tokens":1}}}""";

        var parsed = TranscriptParser.TryParseAssistantRecord(line, out var record);

        Assert.False(parsed);
        Assert.Null(record);
    }

    [Theory]
    [InlineData("<synthetic>")]
    [InlineData("<SYNTHETIC>")]
    [InlineData("<Synthetic>")]
    public void SyntheticRecordIsDroppedRegardlessOfCase(string syntheticModel)
    {
        var line = AssistantLine(
            requestId: "req_synthetic",
            timestamp: "2026-07-20T12:58:16.640Z",
            model: syntheticModel,
            inputTokens: 0,
            outputTokens: 0,
            cacheCreationInputTokens: 0,
            cacheReadInputTokens: 0,
            ephemeral5m: null,
            ephemeral1h: null);

        var parsed = TranscriptParser.TryParseAssistantRecord(line, out var record);

        Assert.False(parsed);
        Assert.Null(record);
    }

    [Fact]
    public void RecordWithoutCacheCreationYieldsNullTtlFieldsRatherThanZero()
    {
        var line = """{"type":"assistant","requestId":"req_no_split","timestamp":"2026-07-20T12:58:16.640Z","message":{"model":"claude-opus-4-8","usage":{"input_tokens":5,"output_tokens":7,"cache_creation_input_tokens":100,"cache_read_input_tokens":50}}}""";

        var parsed = TranscriptParser.TryParseAssistantRecord(line, out var record);

        Assert.True(parsed);
        Assert.Equal(100, record!.Tokens.CacheCreationInputTokens);
        Assert.Null(record.Tokens.CacheCreationEphemeral5mTokens);
        Assert.Null(record.Tokens.CacheCreationEphemeral1hTokens);
    }

    [Fact]
    public void DeduplicateKeepsLastOccurrencePerRequestIdFromTwentyEightRecordsTwelveIds()
    {
        // 12 distinct request ids, 28 total records: ids 1-4 appear 3 times, ids 5-12
        // appear twice, matching the source finding's measured 28/12 duplication ratio.
        var lines = new List<string>();
        var idOccurrenceCounts = new (string id, int count)[]
        {
            ("req_1", 3), ("req_2", 3), ("req_3", 3), ("req_4", 3),
            ("req_5", 2), ("req_6", 2), ("req_7", 2), ("req_8", 2),
            ("req_9", 2), ("req_10", 2), ("req_11", 2), ("req_12", 2),
        };

        foreach (var (id, count) in idOccurrenceCounts)
        {
            for (var occurrence = 0; occurrence < count; occurrence++)
            {
                lines.Add(AssistantLine(
                    requestId: id,
                    timestamp: $"2026-07-20T12:{occurrence:D2}:00.000Z",
                    model: "claude-opus-4-8",
                    inputTokens: 1,
                    outputTokens: (occurrence + 1) * 10,
                    cacheCreationInputTokens: 0,
                    cacheReadInputTokens: 0,
                    ephemeral5m: null,
                    ephemeral1h: null));
            }
        }

        Assert.Equal(28, lines.Count);

        var records = new List<TranscriptRecord>();
        foreach (var line in lines)
        {
            Assert.True(TranscriptParser.TryParseAssistantRecord(line, out var record));
            records.Add(record!);
        }

        var deduplicated = TranscriptParser.Deduplicate(records);

        Assert.Equal(12, deduplicated.Count);

        var req1 = deduplicated.Single(r => r.RequestId == "req_1");
        Assert.Equal(30, req1.Tokens.OutputTokens); // last of 3 occurrences: (2 + 1) * 10

        var req12 = deduplicated.Single(r => r.RequestId == "req_12");
        Assert.Equal(20, req12.Tokens.OutputTokens); // last of 2 occurrences: (1 + 1) * 10
    }

    private static string AssistantLine(
        string requestId,
        string timestamp,
        string model,
        long inputTokens,
        long outputTokens,
        long cacheCreationInputTokens,
        long cacheReadInputTokens,
        long? ephemeral5m,
        long? ephemeral1h)
    {
        var cacheCreationSplit = ephemeral5m is null && ephemeral1h is null
            ? string.Empty
            : $",\"cache_creation\":{{\"ephemeral_5m_input_tokens\":{ephemeral5m ?? 0},\"ephemeral_1h_input_tokens\":{ephemeral1h ?? 0}}}";

        return "{\"type\":\"assistant\",\"requestId\":\"" + requestId + "\",\"timestamp\":\"" + timestamp + "\"," +
            "\"message\":{\"model\":\"" + model + "\",\"usage\":{" +
            "\"input_tokens\":" + inputTokens + "," +
            "\"output_tokens\":" + outputTokens + "," +
            "\"cache_creation_input_tokens\":" + cacheCreationInputTokens + "," +
            "\"cache_read_input_tokens\":" + cacheReadInputTokens +
            cacheCreationSplit +
            "}}}";
    }
}
