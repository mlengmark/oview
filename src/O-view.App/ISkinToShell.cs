namespace OView.App;

/// <summary>
/// The skin-to-shell half of the seam (ADR-0007 D6, lines 198-222): the shell implements
/// this, the skin calls it. Every member is a command, not a query — the skin asks the
/// shell to do something and the next <see cref="IShellToSkin.ShowSnapshot"/>/
/// <see cref="IShellToSkin.RaiseEvent"/> call (the other direction) is how it learns the
/// result, if any.
/// </summary>
public interface ISkinToShell
{
    /// <summary>The user asked for an immediate poll instead of waiting for the next tick.</summary>
    void RefreshNow();

    /// <summary>The user activated the icon (<paramref name="visible"/> = <c>true</c>) or
    /// dismissed the detail window (<c>false</c>) (ADR-0008 D9b). The skin reports the gesture;
    /// the shell decides and answers with <see cref="IShellToSkin.SetVisible"/> followed by
    /// <see cref="IShellToSkin.ShowDetail"/> — the skin never calls <c>SetVisible</c> on
    /// itself.</summary>
    void RequestWidget(bool visible);

    /// <summary>The user clicked the tray icon (ADR-0008 D9b, amended OVI-601 — ui-spec.md
    /// section 4, "Clicking the icon toggles"): open the widget if it is closed, close it if
    /// it is open. Distinct from <see cref="RequestWidget"/>, which a menu's "show usage
    /// details" item still uses to mean "show" unconditionally — this is the icon's own
    /// toggle gesture, left-click only.</summary>
    void ToggleWidget();

    /// <summary>The user changed the alert threshold percent (D4: a shell-owned behaviour
    /// setting).</summary>
    void SetThresholdPercent(int percent);

    /// <summary>The user toggled auto-update opt-in (D4: a shell-owned behaviour setting).</summary>
    void SetAutoUpdate(bool enabled);

    /// <summary>The user asked for a diagnostics bundle, with redaction (D2 point 8).</summary>
    void WriteDiagnosticsBundle();

    /// <summary>The user asked to quit. The shell owns process lifetime (D2) and decides
    /// shutdown order, including calling <see cref="IShellToSkin.Shutdown"/>.</summary>
    void Quit();
}
