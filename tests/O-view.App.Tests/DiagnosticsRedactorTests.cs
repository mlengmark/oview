namespace OView.App.Tests;

/// <summary>
/// Covers <see cref="DiagnosticsRedactor"/> in isolation from the writer: every sensitive value
/// supplied is removed from the text wherever it occurs, including a value that is a substring
/// of another supplied value, and the default-value lookup pulls from an injected environment
/// only — never the real one.
/// </summary>
public class DiagnosticsRedactorTests
{
    [Fact]
    public void Redact_replaces_every_occurrence_of_a_sensitive_value()
    {
        var text = """{"storeDirectory":"C:\\Users\\sensitive-user\\AppData\\Local\\O-view","note":"owned by sensitive-user"}""";

        var redacted = DiagnosticsRedactor.Redact(text, new[] { "sensitive-user" });

        Assert.DoesNotContain("sensitive-user", redacted);
        Assert.Contains(DiagnosticsRedactor.RedactedMarker, redacted);
    }

    [Fact]
    public void Redact_handles_a_value_that_is_a_substring_of_another_sensitive_value()
    {
        var text = "path=C:\\Users\\sensitive-user\\AppData\\Local";

        var redacted = DiagnosticsRedactor.Redact(
            text,
            new[] { "sensitive-user", @"C:\Users\sensitive-user\AppData\Local" });

        Assert.DoesNotContain("sensitive-user", redacted);
        Assert.DoesNotContain(@"C:\Users", redacted);
    }

    [Fact]
    public void Redact_ignores_null_or_empty_sensitive_values()
    {
        var text = "nothing sensitive here";

        var redacted = DiagnosticsRedactor.Redact(text, new[] { "", null! });

        Assert.Equal(text, redacted);
    }

    [Fact]
    public void Redact_leaves_text_unchanged_when_no_sensitive_value_is_present()
    {
        var text = "no match in here";

        var redacted = DiagnosticsRedactor.Redact(text, new[] { "sensitive-user" });

        Assert.Equal(text, redacted);
    }

    [Fact]
    public void DefaultSensitiveValues_collects_user_name_and_home_directory_variables_from_the_injected_lookup()
    {
        var values = DiagnosticsRedactor.DefaultSensitiveValues(
            name => name switch
            {
                "USERNAME" => "sensitive-user",
                "USERPROFILE" => @"C:\Users\sensitive-user",
                "LOCALAPPDATA" => @"C:\Users\sensitive-user\AppData\Local",
                "HOME" => null,
                "XDG_DATA_HOME" => null,
                _ => null,
            },
            userName: "sensitive-user");

        Assert.Contains("sensitive-user", values);
        Assert.Contains(@"C:\Users\sensitive-user", values);
        Assert.Contains(@"C:\Users\sensitive-user\AppData\Local", values);
    }

    [Fact]
    public void DefaultSensitiveValues_omits_variables_the_injected_lookup_does_not_provide()
    {
        var values = DiagnosticsRedactor.DefaultSensitiveValues(_ => null, userName: null);

        Assert.Empty(values);
    }
}
