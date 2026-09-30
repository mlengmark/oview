using OView.Core.Models;
using OView.Core.Providers.CachedUsage;

namespace OView.Core.Tests.Providers.CachedUsage;

/// <summary>
/// Proves <see cref="CachedUtilization.Parse"/> against the documented shape of
/// <c>~/.claude.json</c> -&gt; <c>cachedUsageUtilization</c> (ADR-0005 D2, slice 5), and
/// <see cref="CachedUtilization.TryReadNewest"/>'s newest-across-candidates selection.
/// </summary>
public sealed class CachedUtilizationTests : IDisposable
{
    private readonly string _root;

    public CachedUtilizationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi227-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ParseReadsFetchedAtBothBarsAndExtraUsage()
    {
        var json = """
        {
          "cachedUsageUtilization": {
            "fetchedAtMs": 1758700800000,
            "utilization": {
              "five_hour": { "utilization": 42, "resets_at": "2026-09-24T12:00:00.046735+00:00" },
              "seven_day": { "utilization": 17, "resets_at": "2026-09-28T00:00:00.046735+00:00" },
              "extra_usage": { "is_enabled": false }
            }
          }
        }
        """;

        var result = CachedUtilization.Parse(json);

        Assert.NotNull(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1758700800000), result!.FetchedAtUtc);
        Assert.Equal(42, result.FiveHour!.Percent);
        Assert.Equal(DateTimeOffset.Parse("2026-09-24T12:00:00.046735+00:00"), result.FiveHour.ResetsAtUtc);
        Assert.Equal(17, result.SevenDay!.Percent);
        Assert.Equal(ExtraUsageState.Disabled, result.ExtraUsage);
    }

    [Fact]
    public void ParseReturnsNullWhenTheTopLevelBlockIsAbsent()
    {
        Assert.Null(CachedUtilization.Parse("{\"oauthAccount\":{}}"));
    }

    [Fact]
    public void ParseReturnsNullWhenFetchedAtMsIsMissing()
    {
        Assert.Null(CachedUtilization.Parse("""
        { "cachedUsageUtilization": { "utilization": { "five_hour": { "utilization": 10 } } } }
        """));
    }

    [Fact]
    public void ParseThrowsOnNotJsonAsDocumentedByJsonDocument()
    {
        // CachedUtilization.Parse deliberately does not itself catch malformed JSON — that is
        // TryReadNewest's caller-facing contract via SafeReadFrom (never-throw is enforced at
        // the provider seam, per ADR-0005 D1, not duplicated inside every parsing helper).
        Assert.ThrowsAny<Exception>(() => CachedUtilization.Parse("not json at all"));
    }

    [Fact]
    public void ParseTreatsABarWithNoUtilizationNumberAsAbsent()
    {
        var result = CachedUtilization.Parse("""
        { "cachedUsageUtilization": { "fetchedAtMs": 1, "utilization": { "five_hour": null } } }
        """);

        Assert.NotNull(result);
        Assert.Null(result!.FiveHour);
    }

    [Fact]
    public void ParseDiscardsABarWhosePercentIsOutOfRange()
    {
        var result = CachedUtilization.Parse("""
        { "cachedUsageUtilization": { "fetchedAtMs": 1, "utilization": { "five_hour": { "utilization": 142 } } } }
        """);

        Assert.NotNull(result);
        Assert.Null(result!.FiveHour);
    }

    [Fact]
    public void ParseLeavesExtraUsageNullWhenIsEnabledIsNotABoolean()
    {
        var result = CachedUtilization.Parse("""
        { "cachedUsageUtilization": { "fetchedAtMs": 1, "utilization": { "extra_usage": { "is_enabled": "yes" } } } }
        """);

        Assert.NotNull(result);
        Assert.Null(result!.ExtraUsage);
    }

    [Fact]
    public void TryReadNewestReturnsNullWhenNoCandidateExists()
    {
        Assert.Null(CachedUtilization.TryReadNewest([Path.Combine(_root, "does-not-exist")]));
    }

    [Fact]
    public void TryReadNewestPicksTheFresherBlockRegardlessOfCandidateOrder()
    {
        var staleRoot = Path.Combine(_root, "stale");
        var freshRoot = Path.Combine(_root, "fresh");
        Directory.CreateDirectory(staleRoot);
        Directory.CreateDirectory(freshRoot);
        WriteBlock(staleRoot, fetchedAtMs: 1000);
        WriteBlock(freshRoot, fetchedAtMs: 2000);

        // Stale candidate listed first, mirroring the real migration trap: a relocated config
        // can leave an existing-but-stale stub at the old, more "canonical" path.
        var result = CachedUtilization.TryReadNewest([staleRoot, freshRoot]);

        Assert.NotNull(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(2000), result!.FetchedAtUtc);
    }

    [Fact]
    public void TryReadNewestSkipsAnUnreadableCandidateAndUsesTheOther()
    {
        var badRoot = Path.Combine(_root, "bad");
        var goodRoot = Path.Combine(_root, "good");
        Directory.CreateDirectory(badRoot);
        Directory.CreateDirectory(goodRoot);
        File.WriteAllText(Path.Combine(badRoot, CachedUtilization.FileName), "not json at all");
        WriteBlock(goodRoot, fetchedAtMs: 5000);

        var result = CachedUtilization.TryReadNewest([badRoot, goodRoot]);

        Assert.NotNull(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(5000), result!.FetchedAtUtc);
    }

    private static void WriteBlock(string root, long fetchedAtMs) =>
        File.WriteAllText(
            Path.Combine(root, CachedUtilization.FileName),
            $$"""{ "cachedUsageUtilization": { "fetchedAtMs": {{fetchedAtMs}} } }""");
}
