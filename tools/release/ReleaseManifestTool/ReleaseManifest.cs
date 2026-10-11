using System.Security.Cryptography;
using OView.Core.Updates;

namespace OView.Tools.ReleaseManifest;

/// <summary>
/// The actual logic behind every <c>ReleaseManifestTool</c> command, kept separate from
/// <c>Program</c> so it can be unit-tested without spawning a process.
///
/// <para>Every asset name this type ever compares against comes from
/// <see cref="ReleaseAssets"/> — the release workflow (ADR-0010 D5, slicing table row 9)
/// calls this tool exactly so it never has to restate one of those names by hand.</para>
/// </summary>
public static class ReleaseManifest
{
    /// <summary>One label per required release artefact, independent of version or arch.</summary>
    private static (string Label, Func<string, bool> Matches)[] RequiredAssets() =>
    [
        ("windows installer", name => ReleaseAssets.WindowsInstaller.Matches(name)),
        ("windows portable", name =>
            string.Equals(name, ReleaseAssets.WindowsPortableName, StringComparison.OrdinalIgnoreCase)),
        ("debian amd64", name => ReleaseAssets.DebianPackage("amd64").Matches(name)),
        ("debian arm64", name => ReleaseAssets.DebianPackage("arm64").Matches(name)),
        ("tarball linux-x64", name => ReleaseAssets.Tarball("linux-x64").Matches(name)),
        ("tarball linux-arm64", name => ReleaseAssets.Tarball("linux-arm64").Matches(name)),
    ];

    /// <summary>One of the fixed (non-versioned) names <see cref="ReleaseAssets"/> owns, or
    /// null when <paramref name="key"/> does not name one of them.</summary>
    public static string? Name(string key) => key switch
    {
        "windows-installer" => ReleaseAssets.WindowsInstallerName,
        "windows-portable" => ReleaseAssets.WindowsPortableName,
        "checksums" => ReleaseAssets.ChecksumsName,
        _ => null,
    };

    /// <summary>
    /// Writes <see cref="ReleaseAssets.ChecksumsName"/> into <paramref name="stageDir"/>,
    /// covering every other file already staged there. Never reads an existing manifest —
    /// this is generation, not a merge, so a stale entry from a previous run cannot survive.
    /// </summary>
    public static int GenerateManifest(string stageDir, TextWriter output)
    {
        var files = StagedFiles(stageDir);
        if (files.Count == 0)
        {
            output.WriteLine($"no staged files found in {stageDir}");
            return 1;
        }

        var lines = files.Select(name =>
        {
            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(stageDir, name))));
            return $"{digest.ToLowerInvariant()}  {name}";
        });

        File.WriteAllText(Path.Combine(stageDir, ReleaseAssets.ChecksumsName), string.Join('\n', lines) + "\n");
        output.WriteLine($"wrote {ReleaseAssets.ChecksumsName} with {files.Count} entries");
        return 0;
    }

    /// <summary>
    /// Confirms every staged file is a recognised <see cref="ReleaseAssets"/> artefact, that
    /// every required artefact is present exactly once, and that
    /// <see cref="ChecksumFile.DigestFor"/>/<see cref="ChecksumFile.Matches"/> — the same
    /// parsing Core uses against a downloaded release — accept the manifest already staged
    /// there against the real bytes on disk.
    /// </summary>
    public static int Verify(string stageDir, TextWriter output)
    {
        var manifestPath = Path.Combine(stageDir, ReleaseAssets.ChecksumsName);
        if (!File.Exists(manifestPath))
        {
            output.WriteLine($"manifest not found: {manifestPath}");
            return 1;
        }
        var manifestText = File.ReadAllText(manifestPath);

        var required = RequiredAssets();
        var matched = new HashSet<string>();
        var failures = new List<string>();

        foreach (var file in StagedFiles(stageDir))
        {
            var label = required.FirstOrDefault(r => r.Matches(file)).Label;
            if (label is null)
            {
                failures.Add($"staged file matches no required ReleaseAssets artefact: {file}");
                continue;
            }

            if (!matched.Add(label))
            {
                failures.Add($"more than one staged file matched '{label}': {file}");
                continue;
            }

            var digest = ChecksumFile.DigestFor(manifestText, file);
            if (digest is null)
            {
                failures.Add($"{ReleaseAssets.ChecksumsName} has no usable entry for {file}");
                continue;
            }

            var computed = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(stageDir, file))));
            if (!ChecksumFile.Matches(digest, computed))
            {
                failures.Add($"checksum mismatch for {file}");
            }
        }

        foreach (var (label, _) in required)
        {
            if (!matched.Contains(label))
            {
                failures.Add($"no staged asset found for required artefact: {label}");
            }
        }

        foreach (var failure in failures)
        {
            output.WriteLine(failure);
        }

        if (failures.Count > 0)
        {
            return 1;
        }

        output.WriteLine($"verified {matched.Count} assets against {ReleaseAssets.ChecksumsName}");
        return 0;
    }

    private static List<string> StagedFiles(string stageDir) =>
        Directory.GetFiles(stageDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && name != ReleaseAssets.ChecksumsName)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
}
