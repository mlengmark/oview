namespace OView.App;

/// <summary>
/// Redaction for the diagnostics bundle (ADR-0007 D2 point 8). Scrubs known PII-bearing
/// values — the OS username and the user-specific home-directory environment variables that
/// feed <see cref="StoreDirectoryResolver"/> — wherever they appear in the bundle's rendered
/// text, not only in the one field (the store directory) expected to carry them. A path can
/// resurface in more than one field of a diagnostic dump; a blanket pass over the rendered
/// text is the only redaction shape that does not depend on remembering every call site.
///
/// <para>D4 draws the line as "no PII beyond what's needed for diagnosis": the directory
/// itself is useful for diagnosis (it tells a reader where to look), the user name embedded in
/// it is not, so the name is replaced and the rest of the path is kept.</para>
/// </summary>
public static class DiagnosticsRedactor
{
    public const string RedactedMarker = "<redacted>";

    /// <summary>
    /// Replaces every occurrence of each sensitive value with <see cref="RedactedMarker"/>.
    /// Longer values are replaced first so a short value that happens to be a substring of a
    /// longer one (for example a user name that is also a prefix of a directory path) does not
    /// leave a partially-redacted remainder.
    /// </summary>
    public static string Redact(string text, IEnumerable<string> sensitiveValues)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sensitiveValues);

        foreach (var value in sensitiveValues
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .OrderByDescending(v => v.Length))
        {
            text = text.Replace(value, RedactedMarker, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// The values this process treats as sensitive before any diagnostic output leaves the
    /// shell: the OS user name and the home-directory environment variables
    /// <see cref="StoreDirectoryResolver"/> reads. Takes an injected environment-variable
    /// lookup and user name, the same pure-function shape as
    /// <see cref="StoreDirectoryResolver.ResolveWindows"/>, so it is testable without the real
    /// environment or the real logged-in user.
    /// </summary>
    public static IReadOnlyCollection<string> DefaultSensitiveValues(
        Func<string, string?> getEnvironmentVariable, string? userName)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var values = new List<string>();

        void AddIfPresent(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }
        }

        AddIfPresent(userName);
        AddIfPresent(getEnvironmentVariable("USERNAME"));
        AddIfPresent(getEnvironmentVariable("USERPROFILE"));
        AddIfPresent(getEnvironmentVariable("LOCALAPPDATA"));
        AddIfPresent(getEnvironmentVariable("HOME"));
        AddIfPresent(getEnvironmentVariable("XDG_DATA_HOME"));

        return values;
    }
}
