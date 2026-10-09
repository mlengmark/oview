using OView.Core.Updates;

namespace OView.Core.Tests.Updates;

/// <summary>
/// The bug these prevent: an updater that believes a malformed or ambiguous
/// <c>SHA256SUMS</c> and installs on the strength of it.
///
/// <para>Every case here asserts the same property — anything short of an unambiguous,
/// well-formed entry for the exact asset yields null, and null means "do not install".
/// A lenient parser would turn each of these into permission.</para>
/// </summary>
public class ChecksumFileTests
{
    private const string InstallerDigest =
        "9c0d294c05fc1d88d698034609bb81c0c69196327594e4c69d2915c80fd9850c";
    private const string OtherDigest =
        "d9298a10d1b0735837dc4bd85dac641b0f3cef27a47e5d53a54f2f3f5b2fcffa";

    private static string Manifest(params string[] lines) => string.Join("\n", lines) + "\n";

    // ── the shapes sha256sum actually writes ────────────────────────────────────────

    [Fact]
    public void ReadsTheDigestForTheNamedAsset()
    {
        var text = Manifest(
            $"{OtherDigest}  O-view.Tray.exe",
            $"{InstallerDigest}  O-view-Setup.exe",
            $"{OtherDigest}  o-view_0.7.0_amd64.deb");

        Assert.Equal(InstallerDigest, ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Theory]
    [InlineData("  ")]           // text mode, what sha256sum writes by default
    [InlineData(" *")]           // binary mode
    public void AcceptsBothSeparatorForms(string separator)
    {
        var text = Manifest($"{InstallerDigest}{separator}O-view-Setup.exe");

        Assert.Equal(InstallerDigest, ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Fact]
    public void ToleratesCarriageReturnsAndBlankLines()
    {
        var text = $"\r\n{InstallerDigest}  O-view-Setup.exe\r\n\r\n";

        Assert.Equal(InstallerDigest, ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    // ── everything that must NOT produce a digest (ADR-0010 D3 refusal cases) ───────

    [Fact]
    public void MissingManifestEntryHasNoDigest()
    {
        var text = Manifest($"{OtherDigest}  O-view.Tray.exe");

        Assert.Null(ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Fact]
    public void UnnamedAssetHasNoDigest()
    {
        // The manifest says nothing at all about O-view-Setup.exe.
        var text = Manifest($"{OtherDigest}  o-view_0.7.0_amd64.deb");

        Assert.Null(ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Fact]
    public void NamedTwiceIsRefusedRatherThanResolved()
    {
        // Which line wins is not a guess worth making: one of them is wrong, and picking
        // either could be picking the attacker's.
        var text = Manifest(
            $"{InstallerDigest}  O-view-Setup.exe",
            $"{OtherDigest}  O-view-Setup.exe");

        Assert.Null(ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Theory]
    [InlineData("abc123  O-view-Setup.exe")]                       // too short
    [InlineData("O-view-Setup.exe")]                               // no digest at all
    [InlineData("zzzd294c05fc1d88d698034609bb81c0c69196327594e4c69d2915c80fd9850c  O-view-Setup.exe")]
    public void UnparseableEntriesAreIgnored(string line)
    {
        Assert.Null(ChecksumFile.DigestFor(Manifest(line), "O-view-Setup.exe"));
    }

    [Fact]
    public void ADigestRecordedAgainstAPathIsNotAMatchForTheBareName()
    {
        // "dist/O-view-Setup.exe" describes a file somewhere else. Accepting it would let
        // the manifest speak about something other than the asset that was downloaded.
        var text = Manifest($"{InstallerDigest}  dist/O-view-Setup.exe");

        Assert.Null(ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingManifestHasNoDigest(string? text)
    {
        Assert.Null(ChecksumFile.DigestFor(text, "O-view-Setup.exe"));
    }

    // ── comparison (the digest-mismatch refusal case) ───────────────────────────────

    [Fact]
    public void ComparisonIgnoresHexCasing()
    {
        // sha256sum writes lowercase; Convert.ToHexString writes uppercase. A case-sensitive
        // comparison would reject every genuine update.
        Assert.True(ChecksumFile.Matches(InstallerDigest, InstallerDigest.ToUpperInvariant()));
    }

    [Fact]
    public void DigestMismatchDoesNotMatch()
    {
        Assert.False(ChecksumFile.Matches(InstallerDigest, OtherDigest));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "9c0d294c05fc1d88d698034609bb81c0c69196327594e4c69d2915c80fd9850c")]
    [InlineData("9c0d294c05fc1d88d698034609bb81c0c69196327594e4c69d2915c80fd9850c", null)]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    public void MissingOrShortDigestsNeverMatch(string? recorded, string? computed)
    {
        // Two nulls matching would mean "we know nothing about either side, so install it".
        Assert.False(ChecksumFile.Matches(recorded, computed));
    }
}
