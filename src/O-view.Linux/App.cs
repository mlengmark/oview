using Avalonia;

namespace OView.Linux;

/// <summary>
/// The Linux skin's Avalonia application (ADR-0008 slice 8, OVI-397). No <c>.axaml</c>, no
/// styles, no window — this slice renders nothing, the same boundary slice 3 drew for
/// <c>O-view.Tray</c>. Later slices (9-11) are the first to put anything on screen.
/// </summary>
internal sealed class App : Application
{
}
