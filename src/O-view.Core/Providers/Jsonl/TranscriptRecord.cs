namespace OView.Core.Providers.Jsonl;

/// <summary>
/// One validated assistant record from a Claude transcript. No pricing type and no display
/// string — turning a set of these into a priced, formatted total is a later slice
/// (ADR-0005 D2, slice 3a).
/// </summary>
public sealed record TranscriptRecord(
    string RequestId,
    DateTimeOffset TimestampUtc,
    string Model,
    TranscriptTokens Tokens);
