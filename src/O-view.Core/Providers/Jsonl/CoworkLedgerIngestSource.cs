using OView.Core.Storage;

namespace OView.Core.Providers.Jsonl;

/// <summary>
/// Reads Cowork's audit-log transcripts and writes them into the usage ledger (ADR-0006) —
/// the 2026-10-09 gate G7 parity amendment to ADR-0005 D2. <b>This is not a fifth
/// <see cref="IUsageProvider"/>.</b> Cowork audit records are transcript-shaped token counts,
/// not a plan meter, so they reach the tiles and the graph through the ledger, on the same
/// ingest path <see cref="JsonlUsageProvider"/>'s own transcripts would use — never through
/// <see cref="OView.Core.Models.UsageSnapshot"/>, and never on the tooltip's path (ADR-0005
/// D6b, ADR-0008 D9d). The only provenance this type reports is the same
/// <c>DataSourceKind.JsonlFallback</c> tag a JSONL read already carries; it is not registered
/// in <see cref="OView.Core.Providers.Composite.CompositeUsageProvider"/>'s composition and
/// has no snapshot of its own.
///
/// <para><b>Idempotent by construction.</b> <see cref="UsageLedgerStore.UpsertRange"/> keys on
/// <see cref="TranscriptRecord.RequestId"/> via <c>INSERT ... ON CONFLICT DO UPDATE</c>
/// (ADR-0006 D1) — re-ingesting the same audit log rewrites identical rows rather than
/// duplicating them, with no watermark or offset tracking needed here.</para>
///
/// <para><b>Absent Cowork leaves the ledger untouched.</b> A machine with no
/// <c>local-agent-mode-sessions</c> directory under any candidate root yields no transcripts,
/// so <see cref="Ingest"/> upserts nothing and every figure the ledger already reports stays
/// byte-identical.</para>
/// </summary>
public static class CoworkLedgerIngestSource
{
    /// <summary>
    /// Scans every candidate Claude data root for Cowork audit-log transcripts, parses and
    /// de-duplicates their assistant records by request id
    /// (<see cref="TranscriptParser.TryParseCoworkAssistantRecord"/>,
    /// <see cref="TranscriptParser.Deduplicate"/>), and upserts the result into
    /// <paramref name="store"/> in one transaction. Never throws: a root that cannot be
    /// scanned, a file that cannot be read, and a line that fails to parse are all silently
    /// equivalent to "no record here" (ADR-0005 D1's never-throw obligation, carried forward
    /// to this ingest path).
    /// </summary>
    public static void Ingest(IReadOnlyList<string> candidateRoots, UsageLedgerStore store)
    {
        var records = new List<TranscriptRecord>();

        foreach (var transcript in CoworkAuditLocator.FindTranscripts(candidateRoots))
        {
            records.AddRange(ReadTranscript(transcript));
        }

        if (records.Count == 0)
        {
            return;
        }

        store.UpsertRange(TranscriptParser.Deduplicate(records));
    }

    /// <summary>
    /// Every parseable assistant record in one transcript file, in file order. Wraps the
    /// whole read in one <c>try</c>, mirroring <see cref="JsonlUsageProvider"/>'s own
    /// transcript read: <see cref="File.ReadLines(string)"/> is lazy and can throw mid-
    /// enumeration (a file truncated or removed between the scan and the read), not only on
    /// the initial call.
    /// </summary>
    private static IReadOnlyList<TranscriptRecord> ReadTranscript(string path)
    {
        var records = new List<TranscriptRecord>();

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (TranscriptParser.TryParseCoworkAssistantRecord(line, out var record))
                {
                    records.Add(record);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return records;
    }
}
