namespace OView.Core.Providers.Jsonl;

/// <summary>
/// Locates Cowork's own audit-log transcripts, as distinct from
/// <see cref="JsonlUsageProvider"/>'s Claude Code transcript scan (ADR-0005 D2's 2026-10-09
/// amendment). Cowork runs each session in a sandboxed Claude home under
/// <c>&lt;claude-data-root&gt;\local-agent-mode-sessions\...\&lt;session&gt;\</c>, and — per the
/// source repository's CLAUDE.md rule 9 (read-only reference, on-disk format only) — writes
/// usage records in two shapes inside it: <c>audit.jsonl</c> at the session root on older
/// builds, and <c>.claude\projects\...\*.jsonl</c> inside the sandbox's own Claude Code home
/// when the session runs Claude Code inside the sandbox. A flat recursive
/// <c>*.jsonl</c> search under the session tree finds both without needing to know which
/// shape a given build wrote, and <see cref="TranscriptFileScan"/> is what keeps that walk
/// from aborting on the broken junction the same source documents inside that tree.
///
/// <para>Widening the pattern cannot double-count: ingestion de-duplicates on request id
/// (<see cref="TranscriptParser.Deduplicate"/>) and the ledger upserts on request id again
/// (<see cref="OView.Core.Storage.UsageLedgerStore"/>), so a request recorded in more than one
/// file is stored once.</para>
///
/// <para>Read-only, like every other provider input (ADR-0005 D1).</para>
/// </summary>
public static class CoworkAuditLocator
{
    /// <summary>Directory holding Cowork sessions, relative to a Claude data root.</summary>
    public const string SessionsDirectoryName = "local-agent-mode-sessions";

    /// <summary>
    /// Every transcript Cowork has written under one Claude data root, in either on-disk
    /// shape. Empty if the root has no Cowork sessions directory — a machine that has never
    /// opened Cowork is the normal case, not an error.
    /// </summary>
    public static IReadOnlyList<string> FindTranscripts(string claudeDataRoot) =>
        TranscriptFileScan.Find(Path.Combine(claudeDataRoot, SessionsDirectoryName), "*.jsonl");

    /// <summary>
    /// Every Cowork transcript across several Claude data roots (ADR-0005 D5's candidate-root
    /// list — a packaged MSIX install and an unpackaged one are both covered).
    /// </summary>
    public static IReadOnlyList<string> FindTranscripts(IEnumerable<string> claudeDataRoots) =>
        [.. claudeDataRoots.SelectMany(FindTranscripts)];
}
