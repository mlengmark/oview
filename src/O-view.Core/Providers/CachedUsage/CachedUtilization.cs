using System.Globalization;
using System.Text.Json;
using OView.Core.Models;

namespace OView.Core.Providers.CachedUsage;

/// <summary>One meter: a percentage, and when the window behind it rolls over.</summary>
/// <param name="Percent">Utilization 0-100, as Claude Code received it.</param>
/// <param name="ResetsAtUtc">
/// Exact reset instant, or null when this bar carries none. Reported, not derived — the only
/// exact reset instant this repository has ever had (ADR-0005 D2).
/// </param>
public sealed record UtilizationBar(int Percent, DateTimeOffset? ResetsAtUtc);

/// <summary>
/// The usage figures Claude Code caches in <c>~/.claude.json</c> -&gt; <c>cachedUsageUtilization</c>
/// (ADR-0005 D2, slice 5), carried forward from the source repository's confirmed
/// <c>CachedUtilization</c> (<c>897777b</c>), simplified to this repository's already-landed
/// <see cref="ExtraUsageState"/> shape (two members plus parent nullability, not a three-value
/// enum) rather than the source's richer <c>ExtraUsageStatus</c> record — see this file's
/// landing note in docs/adr/0005-data-provider-contract.md for what was carried forward and
/// what was left out.
///
/// <para>Every field is treated as optional: this is another application's private cache, and
/// it has already gained and lost keys.</para>
/// </summary>
/// <param name="FetchedAtUtc">
/// When Claude Code last refreshed the figures. Drives the staleness label — this is a cache
/// refreshed when Claude Code runs, not a sampler.
/// </param>
/// <param name="FiveHour">The rolling five-hour session meter.</param>
/// <param name="SevenDay">The seven-day weekly meter.</param>
public sealed record CachedUtilization(
    DateTimeOffset FetchedAtUtc,
    UtilizationBar? FiveHour,
    UtilizationBar? SevenDay)
{
    /// <summary>File name Claude Code writes its own state to, beside or under the home
    /// directory candidates <see cref="ClaudeDataRoots.ClaudeCliConfigRoots"/> resolves.</summary>
    public const string FileName = ".claude.json";

    private const string PropertyName = "cachedUsageUtilization";

    /// <summary>
    /// Whether work past the plan allowance bills as extra usage on this account, read from
    /// <c>utilization.extra_usage.is_enabled</c>. Null when the block does not say — this
    /// repository's existing <see cref="ExtraUsageState"/> has no "unknown" member of its own
    /// (OVI-168); the parent nullability already carries that meaning.
    /// </summary>
    public ExtraUsageState? ExtraUsage { get; init; }

    /// <summary>
    /// The most recently fetched block across <paramref name="candidateRoots"/>; null when no
    /// candidate has one. Never throws.
    ///
    /// <para><b>Not merely the first root that exists.</b> A relocated or migrated config can
    /// leave a stub behind at the old path — Claude Code's 2026-08-24 move to
    /// <c>~/.claude/.claude.json</c> left the previous <c>~/.claude.json</c> in place, still
    /// readable, just no longer updated. List order decides nothing here for the same reason it
    /// decides nothing between providers (ADR-0005 D3): the candidates are two locations for one
    /// logical file, so the freshest reading is the reading.</para>
    /// </summary>
    public static CachedUtilization? TryReadNewest(IReadOnlyList<string> candidateRoots)
    {
        CachedUtilization? newest = null;

        foreach (var root in candidateRoots)
        {
            var candidate = SafeReadFrom(Path.Combine(root, FileName));
            if (candidate is not null && (newest is null || candidate.FetchedAtUtc > newest.FetchedAtUtc))
            {
                newest = candidate;
            }
        }

        return newest;
    }

    /// <summary>
    /// Parses the block out of a <c>.claude.json</c> document, or null when it is absent or
    /// carries no usable fetch timestamp. A percentage stops describing anything once it cannot
    /// be dated, which is why an undated block is refused wholesale rather than partially
    /// trusted.
    /// </summary>
    public static CachedUtilization? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty(PropertyName, out var block) ||
            block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!block.TryGetProperty("fetchedAtMs", out var fetched) ||
            fetched.ValueKind != JsonValueKind.Number || !fetched.TryGetInt64(out var fetchedMs))
        {
            return null;
        }

        DateTimeOffset fetchedAtUtc;
        try
        {
            fetchedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(fetchedMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        block.TryGetProperty("utilization", out var bars);

        return new CachedUtilization(
            fetchedAtUtc,
            ReadBar(bars, "five_hour"),
            ReadBar(bars, "seven_day"))
        {
            ExtraUsage = ReadExtraUsage(bars),
        };
    }

    /// <summary>
    /// One bar, or null when absent, explicitly null, or its percentage is outside 0-100 —
    /// the same range discipline <c>PlanHistoryProvider</c> applies to its own samples, because
    /// this is another application's private cache and a figure outside the valid range is not
    /// something this repository will pass on rather than distrust.
    /// </summary>
    private static UtilizationBar? ReadBar(JsonElement bars, string name)
    {
        if (bars.ValueKind != JsonValueKind.Object ||
            !bars.TryGetProperty(name, out var bar) ||
            bar.ValueKind != JsonValueKind.Object ||
            !bar.TryGetProperty("utilization", out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var percent))
        {
            return null;
        }

        var rounded = (int)Math.Round(percent);
        if (rounded is < 0 or > 100)
        {
            return null;
        }

        return new UtilizationBar(rounded, ReadResetAt(bar));
    }

    /// <summary>
    /// The reset instant, normalized to UTC. The source writes an offset-qualified timestamp
    /// (<c>2026-08-24T00:00:00.046735+00:00</c>), so it is parsed as one and converted — never
    /// as a bare <see cref="DateTime"/>, whose <c>Kind</c> would be lost on the way through.
    /// </summary>
    private static DateTimeOffset? ReadResetAt(JsonElement bar) =>
        bar.TryGetProperty("resets_at", out var resets) &&
        resets.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(resets.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Reads <c>utilization.extra_usage.is_enabled</c>; null when it is absent, the parent
    /// object is missing, or the value is not a JSON boolean — every one of those is "the file
    /// did not say", which is a state that happens routinely for another application's cache.
    /// </summary>
    private static ExtraUsageState? ReadExtraUsage(JsonElement bars)
    {
        if (bars.ValueKind != JsonValueKind.Object ||
            !bars.TryGetProperty("extra_usage", out var extra) ||
            extra.ValueKind != JsonValueKind.Object ||
            !extra.TryGetProperty("is_enabled", out var enabled) ||
            enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return enabled.ValueKind == JsonValueKind.True ? ExtraUsageState.Enabled : ExtraUsageState.Disabled;
    }

    /// <summary>
    /// One file: null when it is missing, unreadable, or malformed — all three are "this
    /// candidate cannot answer", and the caller's next candidate can. Opened with
    /// <see cref="FileShare.ReadWrite"/> because Claude Code may rewrite this file while it is
    /// being read, the same reason <c>PlanHistoryProvider</c> opens its file that way.
    /// </summary>
    private static CachedUtilization? SafeReadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
