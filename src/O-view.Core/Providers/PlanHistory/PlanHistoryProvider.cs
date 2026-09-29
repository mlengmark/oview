using System.Text.Json;
using OView.Core.Models;

namespace OView.Core.Providers.PlanHistory;

/// <summary>
/// Reads Claude Desktop's sampled plan-history file (ADR-0005 D2, slice 4), carried forward
/// from the source repository's <c>Providers/PlanHistory/</c> (CONFIRMED at <c>897777b</c>)
/// with its window-derivation subsystem — <c>ResetDetector</c>, local-activity narrowing,
/// multi-org preference, the manual weekly-reset override — deliberately left out. This
/// slice reports the two rolling-window percentages the file states directly; it derives no
/// reset instant. See this file's landing note in
/// docs/adr/0005-data-provider-contract.md for what was carried forward and what was
/// deferred, and why.
///
/// <para>Locates the file under <see cref="ClaudeDataRoots.CandidateRoots"/>'s roots, most-
/// canonical first — the same DI pattern <c>JsonlUsageProvider</c> uses (ADR-0005 D5) — and
/// stops at the first root that yields a readable, non-empty sample set.</para>
/// </summary>
public sealed class PlanHistoryProvider : IUsageProvider
{
    private const string FileName = "plan-usage-history.json";

    /// <summary>
    /// Maximum sample age still labelled <see cref="DataSourceKind.Live"/>.
    ///
    /// <para><b>CONFIRMED at <c>897777b</c>, and not the "roughly every 5 minutes" figure in
    /// this ADR's D2 table.</b> The source's own <c>PlanHistoryProvider.DefaultFreshness</c>
    /// doc comment records that Claude Desktop's sampling cadence changed from 5 to 15
    /// minutes on 2026-08-10, measured against 1,443 real gaps in a 30-day file, and that its
    /// freshness bound was re-measured and raised to 16 minutes — one interval at the new
    /// cadence plus a minute of slack — for exactly that reason. This ADR's D2 table row
    /// predates that change; this slice uses the more specific, more recent evidence rather
    /// than the table's, and flags the discrepancy in this file's landing note instead of
    /// silently overriding the table.</para>
    /// </summary>
    public static readonly TimeSpan DefaultFreshness = TimeSpan.FromMinutes(16);

    private readonly IReadOnlyList<string> _candidateRoots;
    private readonly TimeSpan _freshness;

    /// <param name="candidateRoots">
    /// The plan-history search roots, most-canonical first — typically
    /// <see cref="ClaudeDataRoots.CandidateRoots"/>'s result for the current host. Injected
    /// rather than resolved here, so this type never reads the real operating system or
    /// environment (ADR-0005 D5).
    /// </param>
    /// <param name="freshness">Maximum sample age still labelled
    /// <see cref="DataSourceKind.Live"/>; defaults to <see cref="DefaultFreshness"/>.</param>
    public PlanHistoryProvider(IReadOnlyList<string> candidateRoots, TimeSpan? freshness = null)
    {
        _candidateRoots = candidateRoots;
        _freshness = freshness ?? DefaultFreshness;
    }

    /// <summary>
    /// Returns a <see cref="DataSourceKind.Live"/> or <see cref="DataSourceKind.Stale"/>
    /// snapshot built from the newest valid sample under the first candidate root that has
    /// one, or <see cref="UsageSnapshot.Unavailable"/> if none does. Every failure — a
    /// missing directory, an unreadable file, a permission error, malformed JSON — is
    /// silently equivalent to "no sample here"; none of them ever surfaces as an exception
    /// (ADR-0005 D1).
    /// </summary>
    public UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
    {
        foreach (var root in _candidateRoots)
        {
            var latest = ReadLatestSample(Path.Combine(root, FileName));
            if (latest is null)
            {
                continue;
            }

            var age = utcNow - latest.AtUtc;
            var source = age <= _freshness ? DataSourceKind.Live : DataSourceKind.Stale;

            return new UsageSnapshot(
                source,
                utcNow,
                new UsagePercent(latest.FiveHourPercent, UsageValueStatus.Real),
                new UsageInstant(null, UsageValueStatus.Unavailable),
                new UsagePercent(latest.SevenDayPercent, UsageValueStatus.Real),
                new UsageInstant(null, UsageValueStatus.Unavailable),
                UsageLevel.Green)
            {
                ExtraUsage = null,
            };
        }

        return UsageSnapshot.Unavailable;
    }

    /// <summary>
    /// Parses <paramref name="path"/> and returns its most recent valid sample, or
    /// <c>null</c> on any failure or if no sample in it validates — mirrors the source
    /// repository's <c>PlanHistoryFile.Read</c> (CONFIRMED at <c>897777b</c>): opened with
    /// <see cref="FileShare.ReadWrite"/> because Claude Desktop appends to this file while
    /// this reads it, and every malformed sample is skipped rather than treated as fatal to
    /// the file.
    /// </summary>
    private static PlanHistorySample? ReadLatestSample(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(stream);

            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("samples", out var samples) ||
                samples.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            PlanHistorySample? latest = null;
            foreach (var element in samples.EnumerateArray())
            {
                if (TryParseSample(element) is not { } sample)
                {
                    continue;
                }

                if (latest is null || sample.AtUtc > latest.AtUtc)
                {
                    latest = sample;
                }
            }

            return latest;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// One sample's worth of validation, carried forward field-for-field from the source
    /// repository's <c>PlanHistoryFile.TryParseSample</c> (CONFIRMED at <c>897777b</c>): a
    /// sample missing or misshaping any of <c>t</c>, <c>org</c>, <c>u.fh</c>, <c>u.sd</c> is
    /// skipped, not fatal to the file. <c>org</c> is validated for shape but not carried onto
    /// <see cref="PlanHistorySample"/> — this slice does not de-interleave multi-org files,
    /// because doing so needs a preferred-organization value this repository has no source
    /// for yet (the source reads it from <c>~/.claude.json</c>, which no slice here parses).
    /// Skipping org-mismatched samples without a preference to skip *by* would only discard
    /// data, so this slice takes the file's latest sample regardless of which organization it
    /// names — the same behaviour the source falls back to when no preferred org is supplied.
    /// </summary>
    private static PlanHistorySample? TryParseSample(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // TryGetInt32/64 throw on non-Number kinds rather than returning false — the
        // ValueKind check first is load-bearing (carried forward from the source's comment).
        if (!element.TryGetProperty("t", out var t) ||
            t.ValueKind != JsonValueKind.Number || !t.TryGetInt64(out var epochMs))
        {
            return null;
        }

        if (!element.TryGetProperty("org", out var org) ||
            org.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(org.GetString()))
        {
            return null;
        }

        if (!element.TryGetProperty("u", out var u) || u.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!u.TryGetProperty("fh", out var fh) ||
            fh.ValueKind != JsonValueKind.Number || !fh.TryGetInt32(out var fiveHour))
        {
            return null;
        }

        if (!u.TryGetProperty("sd", out var sd) ||
            sd.ValueKind != JsonValueKind.Number || !sd.TryGetInt32(out var sevenDay))
        {
            return null;
        }

        if (fiveHour is < 0 or > 100 || sevenDay is < 0 or > 100)
        {
            return null;
        }

        try
        {
            return new PlanHistorySample(DateTimeOffset.FromUnixTimeMilliseconds(epochMs), fiveHour, sevenDay);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
