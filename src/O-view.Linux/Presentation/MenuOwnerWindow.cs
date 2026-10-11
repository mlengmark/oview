using Avalonia.Controls;

namespace OView.Linux.Presentation;

/// <summary>
/// The one <see cref="Window"/> <see cref="LinuxTrayMenu"/>'s "Weekly reset…" row needs as an
/// owner for <c>WeeklyResetEntryDialog.ShowAsync</c>'s modal <c>Window.ShowDialog(Window)</c>
/// call (G7 parity slice P23, OVI-744) — and the one Avalonia dependency <c>Program.cs</c> has
/// no window for yet when it composes <see cref="LinuxStatusIcon"/>/<see cref="LinuxTrayMenu"/>:
/// the real <see cref="DetailWindow"/> is not built until <c>App.OnFrameworkInitializationCompleted</c>
/// runs, after the tray icon and its menu already exist. A plain mutable holder rather than a
/// second constructor parameter threaded back in later, because nothing here is a decision to
/// test — it is a box <c>Program.cs</c> hands to the menu empty and <c>App.cs</c> fills in once
/// the detail window exists.
/// </summary>
internal sealed class MenuOwnerWindow
{
    public Window? Current { get; set; }
}
