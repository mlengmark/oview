using OView.Core.Providers.Jsonl;

namespace OView.Core.Tests.Providers.Jsonl;

/// <summary>
/// Proves ADR-0005 D2's 2026-10-09 amendment: Cowork's session tree is found under
/// <c>local-agent-mode-sessions</c>, in either on-disk shape, and a missing sessions
/// directory is "no Cowork here", not an error.
/// </summary>
public sealed class CoworkAuditLocatorTests : IDisposable
{
    private readonly string _root;

    public CoworkAuditLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ovi745-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void NoSessionsDirectoryYieldsNoTranscripts()
    {
        var transcripts = CoworkAuditLocator.FindTranscripts(_root);

        Assert.Empty(transcripts);
    }

    [Fact]
    public void FindsOlderBuildAuditJsonlAtTheSessionRoot()
    {
        var sessionDir = Path.Combine(_root, "local-agent-mode-sessions", "org", "user", "session-1");
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(Path.Combine(sessionDir, "audit.jsonl"), "");

        var transcripts = CoworkAuditLocator.FindTranscripts(_root);

        Assert.Equal([Path.Combine(sessionDir, "audit.jsonl")], transcripts);
    }

    [Fact]
    public void FindsCurrentBuildTranscriptNestedUnderTheSandboxsOwnClaudeProjectsDirectory()
    {
        var nested = Path.Combine(
            _root, "local-agent-mode-sessions", "org", "user", "session-2",
            ".claude", "projects", "encoded-cwd");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "abc123.jsonl"), "");

        var transcripts = CoworkAuditLocator.FindTranscripts(_root);

        Assert.Equal([Path.Combine(nested, "abc123.jsonl")], transcripts);
    }

    [Fact]
    public void FindsTranscriptsAcrossSeveralCandidateRoots()
    {
        var firstSession = Path.Combine(_root, "first", "local-agent-mode-sessions", "org", "user", "s1");
        var secondSession = Path.Combine(_root, "second", "local-agent-mode-sessions", "org", "user", "s2");
        Directory.CreateDirectory(firstSession);
        Directory.CreateDirectory(secondSession);
        File.WriteAllText(Path.Combine(firstSession, "audit.jsonl"), "");
        File.WriteAllText(Path.Combine(secondSession, "audit.jsonl"), "");

        var transcripts = CoworkAuditLocator.FindTranscripts(
            [Path.Combine(_root, "first"), Path.Combine(_root, "second")]);

        Assert.Equal(2, transcripts.Count);
    }
}
