using OView.Core.Models;

namespace OView.Core.Providers.Jsonl;

/// <summary>
/// Scans local Claude transcripts for a readable record and reports provenance and capture
/// time only, never a meter (ADR-0005 D6a, slice 3b). Locates transcripts under the roots
/// <see cref="ClaudeDataRoots.CandidateRoots"/> resolves, most-canonical first, and reads
/// each with <see cref="TranscriptParser.TryParseAssistantRecord"/>. Token totals are not
/// aggregated here — <see cref="UsageSnapshot"/> has no field for them (D6a), and the
/// accumulated-history seam that does (D6c's <c>UsageStatistics</c>) is a separate slice
/// built from ADR-0006's ledger, not from this provider.
/// </summary>
public sealed class JsonlUsageProvider : IUsageProvider
{
    private readonly IReadOnlyList<string> _candidateRoots;

    /// <param name="candidateRoots">
    /// The transcript search roots, most-canonical first — typically
    /// <see cref="ClaudeDataRoots.CandidateRoots"/>'s result for the current host. Injected
    /// rather than resolved here, so this type never reads the real operating system or
    /// environment (ADR-0005 D5).
    /// </param>
    public JsonlUsageProvider(IReadOnlyList<string> candidateRoots)
    {
        _candidateRoots = candidateRoots;
    }

    /// <summary>
    /// Returns a <see cref="DataSourceKind.JsonlFallback"/> snapshot shaped exactly per
    /// ADR-0005 D6a once any candidate root yields a readable transcript record, or
    /// <see cref="UsageSnapshot.Unavailable"/> if none does. Every failure — a missing
    /// directory, an unreadable file, a permission error, malformed JSON — is silently
    /// equivalent to "no record here"; none of them ever surfaces as an exception.
    /// </summary>
    public UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
    {
        foreach (var root in _candidateRoots)
        {
            if (HasReadableTranscriptRecord(root))
            {
                return new UsageSnapshot(
                    DataSourceKind.JsonlFallback,
                    utcNow,
                    new UsagePercent(null, UsageValueStatus.Unavailable),
                    new UsageInstant(null, UsageValueStatus.Unavailable),
                    new UsagePercent(null, UsageValueStatus.Unavailable),
                    new UsageInstant(null, UsageValueStatus.Unavailable),
                    UsageLevel.Green)
                {
                    ExtraUsage = null,
                };
            }
        }

        return UsageSnapshot.Unavailable;
    }

    /// <summary>
    /// Walks every <c>*.jsonl</c> file under <paramref name="root"/>, returning as soon as
    /// one line parses as an assistant record. Wraps the whole walk — enumeration and every
    /// read — in one <c>try</c>, because <see cref="Directory.EnumerateFiles"/> is lazy and
    /// can throw mid-iteration (a subdirectory that disappears or denies access between the
    /// existence check and the read), not only on the initial call.
    /// </summary>
    private static bool HasReadableTranscriptRecord(string root)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (TranscriptParser.TryParseAssistantRecord(line, out _))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }

        return false;
    }
}
