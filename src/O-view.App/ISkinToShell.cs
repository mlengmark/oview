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
