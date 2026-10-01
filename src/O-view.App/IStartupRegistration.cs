namespace OView.App;

/// <summary>
/// Whether O-view starts with the user's session.
///
/// <para>This is OS-owned truth (ADR-0007 D4): the per-user, no-elevation mechanism itself —
/// the Windows Run key, an XDG autostart <c>.desktop</c> file on Linux — <b>is</b> the state.
/// It is never duplicated into the shell's settings file, so the two can never disagree, and
/// an external editor (Task Manager's startup page, deleting the desktop file) stays
/// authoritative. Per D5 the mechanism is implemented by the skin for that OS; this interface
/// is only the shell's seam onto it.</para>
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    /// <summary>Registers the running executable. Returns whether it succeeded.</summary>
    bool Enable();

    /// <summary>Returns whether it succeeded. Removing something already absent is success.</summary>
    bool Disable();

    /// <summary>
    /// Applies the requested state and returns the state as it <b>actually stands
    /// afterwards</b>, not the state that was asked for.
    ///
    /// <para>A registry write or a file write can fail, and reporting the requested state
    /// regardless would be a fabricated fact about the user's machine. Shared here so no
    /// skin has to get this subtly wrong on its own.</para>
    /// </summary>
    bool Apply(bool enable)
    {
        var ok = enable ? Enable() : Disable();
        return ok ? enable : IsEnabled();
    }
}
