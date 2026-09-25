using System.Reflection;
using OView.Core.Models;

namespace OView.Core.Tests.Models;

/// <summary>
/// Pins ADR-0001's OVI-146 amendment: <c>Status == Unavailable ⇒ Value == null</c> for every
/// status-paired value type. The ill-formed pair throws at construction, by every route, so
/// no skin can ever be handed a value flagged unavailable.
/// </summary>
public class UsageValueRuleTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 8, 20, 59, 0, TimeSpan.Zero);

    public static TheoryData<Type> StatusPairedTypes => new()
    {
        typeof(UsagePercent),
        typeof(UsageInstant),
        typeof(TokenCount),
        typeof(EstimatedUsd),
    };

    [Fact]
    public void UnavailableWithAValueThrowsForEveryStatusPairedType()
    {
        Assert.Throws<ArgumentException>(() => new UsagePercent(47, UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() => new UsageInstant(Instant, UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() => new TokenCount(1_200, UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() => new EstimatedUsd(1.25m, UsageValueStatus.Unavailable));
    }

    [Fact]
    public void AZeroValueIsStillAValueAndStillThrows()
    {
        Assert.Throws<ArgumentException>(() => new UsagePercent(0, UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() => new TokenCount(0, UsageValueStatus.Unavailable));
        Assert.Throws<ArgumentException>(() => new EstimatedUsd(0m, UsageValueStatus.Unavailable));
    }

    [Fact]
    public void UnavailableWithNullConstructs()
    {
        Assert.Null(new UsagePercent(null, UsageValueStatus.Unavailable).Value);
        Assert.Null(new UsageInstant(null, UsageValueStatus.Unavailable).Value);
        Assert.Null(new TokenCount(null, UsageValueStatus.Unavailable).Value);
        Assert.Null(new EstimatedUsd(null, UsageValueStatus.Unavailable).Value);
    }

    [Theory]
    [InlineData(UsageValueStatus.Real)]
    [InlineData(UsageValueStatus.Estimated)]
    public void RealOrEstimatedWithAValueConstructs(UsageValueStatus status)
    {
        Assert.Equal(47, new UsagePercent(47, status).Value);
        Assert.Equal(Instant, new UsageInstant(Instant, status).Value);
        Assert.Equal(1_200, new TokenCount(1_200, status).Value);
        Assert.Equal(1.25m, new EstimatedUsd(1.25m, status).Value);
    }

    [Theory]
    [InlineData(UsageValueStatus.Real)]
    [InlineData(UsageValueStatus.Estimated)]
    public void TheConverseIsNotForbidden(UsageValueStatus status)
    {
        Assert.Null(new UsagePercent(null, status).Value);
        Assert.Null(new UsageInstant(null, status).Value);
        Assert.Null(new TokenCount(null, status).Value);
        Assert.Null(new EstimatedUsd(null, status).Value);
    }

    [Fact]
    public void DefaultSatisfiesTheRule()
    {
        Assert.Equal(UsageValueStatus.Real, default(UsagePercent).Status);
        Assert.Null(default(UsagePercent).Value);
        Assert.Null(default(UsageInstant).Value);
        Assert.Null(default(TokenCount).Value);
        Assert.Null(default(EstimatedUsd).Value);
    }

    [Theory]
    [MemberData(nameof(StatusPairedTypes))]
    public void NoWithExpressionOrInitializerCanSetValueOrStatus(Type type)
    {
        // A `with` expression or object initializer compiles only against a settable
        // (init or set) property. With neither accessor, the positional constructor, which
        // checks the rule, is the only way to put a value and a status into an instance, and
        // no assignment order inside a `with` can bypass it.
        foreach (var name in new[] { "Value", "Status" })
        {
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(property);
            Assert.Null(property.SetMethod);
        }
    }

    [Fact]
    public void AnEmptyWithCopiesAWellFormedPair()
    {
        var unavailable = new UsagePercent(null, UsageValueStatus.Unavailable);

        var copy = unavailable with { };

        Assert.Equal(unavailable, copy);
    }

    [Fact]
    public void TheRecordShapeSurvives()
    {
        var (value, status) = new UsagePercent(47, UsageValueStatus.Estimated);

        Assert.Equal(47, value);
        Assert.Equal(UsageValueStatus.Estimated, status);
        Assert.Equal(new TokenCount(5, UsageValueStatus.Real), new TokenCount(5, UsageValueStatus.Real));
        Assert.NotEqual(new TokenCount(5, UsageValueStatus.Real), new TokenCount(5, UsageValueStatus.Estimated));
    }
}
