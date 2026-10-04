using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>
/// Covers <see cref="NotificationHostAdvisoryFormatter"/> (ADR-0008 D6 point 3, OVI-397):
/// the absent case states the observation and what to do, never a guess about the user's
/// machine; the present case states only the observation.
/// </summary>
public class NotificationHostAdvisoryFormatterTests
{
    [Fact]
    public void Describe_true_states_only_that_a_host_was_found()
    {
        var text = NotificationHostAdvisoryFormatter.Describe(observed: true);

        Assert.Contains("found", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("extension", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_false_states_the_observation_and_the_GNOME_extension_advice()
    {
        var text = NotificationHostAdvisoryFormatter.Describe(observed: false);

        Assert.Contains("org.kde.StatusNotifierWatcher", text);
        Assert.Contains("no owner", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppIndicator", text);
        Assert.Contains("KStatusNotifierItem", text);
    }

    [Fact]
    public void Describe_false_never_asserts_what_the_absent_extension_is()
    {
        // D6 point 3: "A message asserting the extension is missing, when what was observed
        // was an absent bus name, is as wrong as saying nothing." The wording must describe
        // the bus observation, not claim to know the desktop environment's state.
        var text = NotificationHostAdvisoryFormatter.Describe(observed: false);

        Assert.DoesNotContain("the extension is missing", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("you are running GNOME", text, StringComparison.OrdinalIgnoreCase);
    }
}
