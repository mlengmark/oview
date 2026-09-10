namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A single pinned content fact a golden-master fixture requires every skin's rendered
/// text to satisfy — a number, a rounding rule, a disclosure — never an exact string
/// (ADR-0003). Two skins may phrase the same fact differently; both must satisfy it.
/// </summary>
public sealed record ContentFact(string Description, Func<string, bool> IsSatisfiedBy)
{
    /// <summary>The common case: the rendered text must contain a specific substring
    /// (e.g. "57%", "Mon 23:00") regardless of what surrounds it.</summary>
    public static ContentFact Contains(string substring) =>
        new($"contains \"{substring}\"", rendered => rendered.Contains(substring, StringComparison.Ordinal));
}
