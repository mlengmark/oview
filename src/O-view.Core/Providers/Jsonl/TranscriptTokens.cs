namespace OView.Core.Providers.Jsonl;

/// <summary>
/// The token fields Claude's assistant records publish under <c>message.usage</c>, carried
/// forward without derivation — no pricing, no display formatting (ADR-0005 D2, slice 3a).
/// </summary>
/// <param name="InputTokens">Mapped from <c>usage.input_tokens</c>.</param>
/// <param name="OutputTokens">Mapped from <c>usage.output_tokens</c>.</param>
/// <param name="CacheCreationInputTokens">Mapped from <c>usage.cache_creation_input_tokens</c> —
/// the flat, unattributed total, present even when <see cref="CacheCreationEphemeral5mTokens"/>
/// and <see cref="CacheCreationEphemeral1hTokens"/> are not.</param>
/// <param name="CacheReadInputTokens">Mapped from <c>usage.cache_read_input_tokens</c>.</param>
/// <param name="CacheCreationEphemeral5mTokens">Mapped from
/// <c>usage.cache_creation.ephemeral_5m_input_tokens</c>. <c>null</c>, not <c>0</c>, when
/// <c>usage.cache_creation</c> is absent from the record — absent means not attributed to a
/// TTL, and writing <c>0</c> there would fabricate a number this project has promised never to
/// fabricate.</param>
/// <param name="CacheCreationEphemeral1hTokens">Mapped from
/// <c>usage.cache_creation.ephemeral_1h_input_tokens</c>, with the same nullability reasoning
/// as <see cref="CacheCreationEphemeral5mTokens"/>.</param>
public readonly record struct TranscriptTokens(
    long InputTokens,
    long OutputTokens,
    long CacheCreationInputTokens,
    long CacheReadInputTokens,
    long? CacheCreationEphemeral5mTokens,
    long? CacheCreationEphemeral1hTokens);
