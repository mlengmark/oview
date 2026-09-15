using System.Globalization;
using OView.Core.Models;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Byte-for-byte regression target: the source app's own <c>UsageFormatterTests</c>
/// (`mlengmark/O-view`, `tests/O-view.Core.Tests/UsageFormatterTests.cs`), ported onto the
/// <see cref="TokenCount"/>/<see cref="EstimatedUsd"/> contract types. Every case below
/// reproduces a value/expected-string pair read directly from that source file.
/// </summary>
public class UsageFormatterTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]              // last value below the K boundary
    [InlineData(1_000, "1.0K")]           // first value at it
    [InlineData(1_500, "1.5K")]
    [InlineData(999_999, "1000.0K")]      // last value below the M boundary
    [InlineData(1_000_000, "1.0M")]       // first value at it
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
    [InlineData(0.004, "$0.00")]          // rounds, never renders as "unknown"
    public void Usd_AlwaysTwoDecimals(decimal usd, string expected)
    {
        Assert.Equal(expected, UsageFormatter.Usd(new EstimatedUsd(usd, UsageValueStatus.Estimated)));
    }

    [Fact]
    public void Usd_Unavailable_IsUnknown_NotZero()
    {
        // An unpriced window has an unknown value, not a zero one. "$0.00" would read as
        // "this cost nothing".
        Assert.Equal("unknown", UsageFormatter.Usd(new EstimatedUsd(null, UsageValueStatus.Unavailable)));
    }

    /// <summary>
    /// Not present in the source app (its <c>Tokens(long)</c> overload can never receive an
    /// absent value — every call site already has a real count). Added here because the
    /// Core contract's <see cref="TokenCount"/> can legitimately be unavailable, and this
    /// skin must still say so rather than rendering a fabricated "0".
    /// </summary>
    [Fact]
    public void Tokens_Unavailable_RendersAGapRatherThanAFabricatedZero()
    {
        Assert.Equal("?", UsageFormatter.Tokens(new TokenCount(null, UsageValueStatus.Unavailable)));
    }

    [Fact]
    public void Formatting_IsInvariant_NotTheMachineCulture()
    {
        // A comma decimal separator would break both figures on a European machine. The
        // app pins its own presentation rather than inheriting the OS setting.
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
