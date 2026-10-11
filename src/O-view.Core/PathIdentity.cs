namespace OView.Core;

/// <summary>
/// Whether two paths name the same filesystem entry — a question with a different answer on
/// each platform, so it is asked in exactly one place (mirroring
/// <see cref="OView.Core.Providers.ClaudeDataRoots.CandidateRoots"/>'s own one-member rule for
/// platform awareness, ADR-0005 D5).
///
/// <para>Windows filesystems are case-insensitive: <c>Alpha</c> and <c>alpha</c> are one
/// directory. Linux filesystems are case-sensitive: they are two, and folding them together
/// would make a directory walk's cycle-detection skip the second silently. Ported from the
/// source repository's <c>OView.Core.PathIdentity</c> (read-only reference, on-disk format
/// only) because <see cref="OView.Core.Providers.Jsonl.TranscriptFileScan"/> needs the same
/// per-platform comparer for its own visited-directory set.</para>
///
/// <para>macOS is deliberately not handled — out of scope (gate G3).</para>
/// </summary>
public static class PathIdentity
{
    /// <summary>
    /// The comparer for de-duplicating or looking up paths. Resolved once — the platform does
    /// not change while the process runs.
    /// </summary>
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
