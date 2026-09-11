using System.Reflection;
using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>
/// Proves ADR-0001's contract holds for the tooltip's slice of Core: every value is
/// structured (typed, with its own trust status), not a pre-built string, and the
/// Windows-only 127-character NotifyIcon cap has no footprint in this assembly at all.
/// </summary>
public class UsageSnapshotTests
{
    [Fact]
    public void EveryContractValueIsStructuredNeverAPreBuiltString()
    {
        var properties = typeof(UsageSnapshot).GetProperties();

        Assert.NotEmpty(properties);
        Assert.All(properties, property => Assert.NotEqual(typeof(string), property.PropertyType));
    }

    [Fact]
    public void EveryPercentAndInstantCarriesItsOwnTrustStatus()
    {
        var snapshot = new UsageSnapshot(
            DataSourceKind.Live,
            new DateTimeOffset(2026, 9, 8, 20, 45, 0, TimeSpan.Zero),
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(DateTimeOffset.UtcNow, UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(DateTimeOffset.UtcNow, UsageValueStatus.Real),
            UsageLevel.Amber);

        Assert.Equal(UsageValueStatus.Real, snapshot.SessionUtilizationPercent.Status);
        Assert.Equal(UsageValueStatus.Real, snapshot.SessionResetAt.Status);
        Assert.Equal(UsageValueStatus.Real, snapshot.WeeklyUtilizationPercent.Status);
        Assert.Equal(UsageValueStatus.Real, snapshot.WeeklyResetAt.Status);
    }

    [Fact]
    public void UnavailableSnapshotMarksEveryValueUnavailableRatherThanZeroOrBlank()
    {
        var snapshot = UsageSnapshot.Unavailable;

        Assert.Equal(DataSourceKind.Unavailable, snapshot.DataSourceKind);
        Assert.Null(snapshot.SessionUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionUtilizationPercent.Status);
        Assert.Null(snapshot.SessionResetAt.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.SessionResetAt.Status);
        Assert.Null(snapshot.WeeklyUtilizationPercent.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.WeeklyUtilizationPercent.Status);
        Assert.Null(snapshot.WeeklyResetAt.Value);
        Assert.Equal(UsageValueStatus.Unavailable, snapshot.WeeklyResetAt.Status);
    }

    [Fact]
    public void LastIngestAtRoundTripsTheExplicitValueGiven()
    {
        var ingestAt = new DateTimeOffset(2026, 9, 11, 8, 30, 0, TimeSpan.Zero);
        var snapshot = new UsageSnapshot(
            DataSourceKind.Stale,
            ingestAt,
            new UsagePercent(57, UsageValueStatus.Real),
            new UsageInstant(DateTimeOffset.UtcNow, UsageValueStatus.Real),
            new UsagePercent(14, UsageValueStatus.Real),
            new UsageInstant(DateTimeOffset.UtcNow, UsageValueStatus.Real),
            UsageLevel.Amber);

        Assert.Equal(ingestAt, snapshot.LastIngestAt);
    }

    /// <summary>
    /// <see cref="UsageSnapshot.LastIngestAt"/> is a required, non-nullable field (ADR-0001's
    /// 2026-09-11 amendment) — a skin must always be able to read it, never guess whether it
    /// is present. <see cref="UsageSnapshot.Unavailable"/> still needs a value to satisfy the
    /// constructor; it uses <see cref="DateTimeOffset.MinValue"/> as an explicit "never"
    /// sentinel rather than a plausible-looking fabricated recent timestamp.
    /// </summary>
    [Fact]
    public void UnavailableSnapshotUsesMinValueAsAnExplicitNeverSentinelForLastIngestAt()
    {
        Assert.Equal(DateTimeOffset.MinValue, UsageSnapshot.Unavailable.LastIngestAt);
    }

    [Fact]
    public void DataSourceKindDistinguishesStaleFromLiveAndFromEveryOtherTier()
    {
        var values = Enum.GetValues<DataSourceKind>();

        Assert.Contains(DataSourceKind.Stale, values);
        Assert.Equal(values.Distinct().Count(), values.Length);
    }

    [Fact]
    public void ThisAssemblyDefinesNoNotifyIconLengthCap()
    {
        var membersNamedMaxLength = typeof(UsageSnapshot).Assembly.GetTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(member => member.Name == "MaxLength");

        Assert.Empty(membersNamedMaxLength);
    }
}
