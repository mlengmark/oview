using OView.Core.Models;

namespace OView.App;

/// <summary>
/// The shell-to-skin half of the seam (ADR-0007 D6, lines 198-222): the skin implements
/// this, the shell calls it. No member here returns anything the shell waits on — the shell
/// pushes state and events; the skin decides what to do with them and never hands anything
/// back through this interface (that is <see cref="ISkinToShell"/>, the other direction).
/// </summary>
public interface IShellToSkin
{
    /// <summary>Called on every successful poll with the full contract snapshot
    /// (ADR-0001). The skin decides what, if anything, changes on screen.</summary>
    void ShowSnapshot(UsageSnapshot snapshot);

    /// <summary>An enum-plus-data event the shell decided to raise (D2 point 6). Never a
    /// sentence, never a title, never a severity colour.</summary>
    void RaiseEvent(UsageEvent usageEvent);

    /// <summary>Everything the detail window needs for one render (ADR-0008 D9a), pushed once
    /// when the widget becomes visible and again on every successful poll while it stays
    /// visible — never while hidden (D9b). <see cref="UsageDetail.Unavailable"/> when the
    /// ledger read behind it failed.</summary>
    void ShowDetail(UsageDetail detail);

    /// <summary>The widget's requested visibility. Lifecycle only — the shell says "the
    /// user asked for the widget," not where or how big.</summary>
    void SetVisible(bool visible);

    /// <summary>Skin tears down its OS integration; shell then disposes Core.</summary>
    void Shutdown();
}
