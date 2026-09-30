namespace OView.Core.Storage;

/// <summary>
/// One (local date × model) bucket, aggregated at query time from <see cref="UsageLedgerStore"/>
/// (ADR-0006 D1 — daily rollups are computed, never stored). No display formatting: <see
/// cref="LocalDate"/> is a value, not a string, and stays the skin's to render.
/// </summary>
public sealed record DailyModelUsage(
    DateOnly LocalDate,
    string Model,
    int RequestCount,
    long InputTokens,
    long OutputTokens,
    long CacheCreationInputTokens,
    long CacheReadInputTokens,
    long CacheCreationEphemeral5mTokens,
    long CacheCreationEphemeral1hTokens);
