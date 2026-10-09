using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>
/// Proves ADR-0008 D9e's widening holds: every new <see cref="UsageDetail"/> member defaults
/// to its own <c>Unavailable</c> sentinel when no provider has assembled it, so an unassembled
/// shell pushes an honest gap rather than a zero.
/// </summary>
public class UsageDetailTests
{
    [Fact]
    public void AccountDefaultsToUnavailableWhenNoProviderAssemblesIt()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        Assert.Equal(AccountIdentity.Unavailable, detail.Account);
        Assert.Equal(UsageValueStatus.Unavailable, detail.Account.Status);
        Assert.Null(detail.Account.DisplayName);
        Assert.Null(detail.Account.EmailAddress);
        Assert.Null(detail.Account.OrganizationType);
    }

    [Fact]
    public void HistoryDefaultsToUnavailableWhenNoProviderAssemblesIt()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        Assert.Equal(DailyUsageSeries.Unavailable, detail.History);
        Assert.Equal(UsageValueStatus.Unavailable, detail.History.Status);
        Assert.Empty(detail.History.Days);
    }

    [Fact]
    public void ResetBoundariesDefaultsToUnavailableWhenNoProviderAssemblesIt()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        Assert.Equal(WeeklyResetBoundaries.Unavailable, detail.ResetBoundaries);
        Assert.Equal(UsageValueStatus.Unavailable, detail.ResetBoundaries.Status);
        Assert.Empty(detail.ResetBoundaries.Boundaries);
    }

    [Fact]
    public void TokensTodayDefaultsToUnavailableWhenNoProviderAssemblesIt()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        Assert.Equal(TokenKindTotals.Unavailable, detail.TokensToday);
        Assert.Equal(UsageValueStatus.Unavailable, detail.TokensToday.Status);
        Assert.Null(detail.TokensToday.Total.Value);
    }

    [Fact]
    public void Tokens31dDefaultsToUnavailableWhenNoProviderAssemblesIt()
    {
        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable);

        Assert.Equal(TokenKindTotals.Unavailable, detail.Tokens31d);
        Assert.Equal(UsageValueStatus.Unavailable, detail.Tokens31d.Status);
        Assert.Null(detail.Tokens31d.Total.Value);
    }

    [Fact]
    public void UnavailableDetailAlsoDefaultsAllFiveNewMembersToUnavailable()
    {
        var detail = UsageDetail.Unavailable;

        Assert.Equal(UsageValueStatus.Unavailable, detail.Account.Status);
        Assert.Equal(UsageValueStatus.Unavailable, detail.History.Status);
        Assert.Equal(UsageValueStatus.Unavailable, detail.ResetBoundaries.Status);
        Assert.Equal(UsageValueStatus.Unavailable, detail.TokensToday.Status);
        Assert.Equal(UsageValueStatus.Unavailable, detail.Tokens31d.Status);
    }

    [Fact]
    public void NewMembersCanBeSetExplicitlyWithoutDisturbingTheOriginalThreeMembers()
    {
        var account = new AccountIdentity("Ada", "ada@example.com", "team", UsageValueStatus.Real);

        var detail = new UsageDetail(UsageSnapshot.Unavailable, UsageStatistics.Unavailable, ModelUsageBreakdown.Unavailable)
        {
            Account = account,
        };

        Assert.Equal(account, detail.Account);
        Assert.Equal(UsageSnapshot.Unavailable, detail.Snapshot);
        Assert.Equal(UsageStatistics.Unavailable, detail.Statistics);
        Assert.Equal(ModelUsageBreakdown.Unavailable, detail.Models);
    }
}
