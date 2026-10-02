using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.4.2", 0, 4, 2)]
    [InlineData("0.4.2", 0, 4, 2)]
    [InlineData("0.4.2.0", 0, 4, 2)]
    [InlineData("V1.2.3-beta.1", 1, 2, 3)]
    [InlineData("2.0+build", 2, 0, 0)]
    [InlineData("3", 3, 0, 0)]
    public void TryParse_normalises_tags_and_assembly_versions(string text, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("1.2.3.4.5")]
    public void TryParse_rejects_unparseable_input(string? text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out _));
    }

    [Fact]
    public void Comparison_is_numeric_not_lexicographic()
    {
        Assert.True(new ReleaseVersion(0, 10, 0) > new ReleaseVersion(0, 9, 0));
    }

    [Fact]
    public void ToString_renders_major_minor_patch()
    {
        Assert.Equal("1.2.3", new ReleaseVersion(1, 2, 3).ToString());
    }
}
