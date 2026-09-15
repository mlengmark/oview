using System.Globalization;
using OView.Core.Models;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

public class UsageFormatterTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]
    [InlineData(1_000, "1.0K")]
    [InlineData(1_500, "1.5K")]
    [InlineData(999_999, "1000.0K")]
    [InlineData(1_000_000, "1.0M")]
    [InlineData(12_700_000, "12.7M")]
    [InlineData(684_600_000, "684.6M")]
    public void Tokens_AbbreviatesAtThousandsAndMillions(long tokens, string expected)
    {
        Assert.Equal(expected, UsageFormatter.Tokens(new TokenCount(tokens, UsageValueStatus.Real)));
    }

    [Theory]
    [InlineData(0, "$0.00")]
    [InlineData(9.36, "$9.36")]
    [InlineData(492.52, "$492.52")]
    [InlineData(0.004, "$0.00")]
    public void Usd_AlwaysTwoDecimals(decimal usd, string expected)
    {
        Assert.Equal(expected, UsageFormatter.Usd(new EstimatedUsd(usd, UsageValueStatus.Estimated)));
    }

    [Fact]
    public void Usd_Unavailable_IsNotAvailable_NotZero()
    {
        // Deliberately worded "n/a" rather than the Windows skin's "unknown" (ADR-0003) —
        // never a fabricated "$0.00".
        Assert.Equal("n/a", UsageFormatter.Usd(new EstimatedUsd(null, UsageValueStatus.Unavailable)));
    }

    [Fact]
    public void Tokens_Unavailable_RendersAGapRatherThanAFabricatedZero()
    {
        Assert.Equal("n/a", UsageFormatter.Tokens(new TokenCount(null, UsageValueStatus.Unavailable)));
    }

    [Fact]
    public void Formatting_IsInvariant_NotTheMachineCulture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("$1234.50", UsageFormatter.Usd(new EstimatedUsd(1234.50m, UsageValueStatus.Estimated)));
            Assert.Equal("1.5K", UsageFormatter.Tokens(new TokenCount(1_500, UsageValueStatus.Real)));
            Assert.Equal("12.7M", UsageFormatter.Tokens(new TokenCount(12_700_000, UsageValueStatus.Real)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
