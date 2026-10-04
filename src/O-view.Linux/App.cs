using Avalonia;
using OView.Linux.Presentation;

namespace OView.Linux;

/// <summary>
/// The Linux skin's Avalonia application. Slice 8 (OVI-397) left this with no <c>.axaml</c>,
/// no styles, no window, rendering nothing. Slice 9 (OVI-403) is the first to put anything on
/// screen: the constructor takes the already-built <see cref="LinuxStatusIcon"/> (composed in
/// <c>Program.cs</c> before Avalonia's lifetime starts) and attaches it here, in
/// <see cref="OnFrameworkInitializationCompleted"/> — the first point at which
/// <c>Application.Current</c> and the platform render interface are guaranteed to exist. Still
/// no window: a detail window is a separately scoped, later slice (10).
/// </summary>
internal sealed class App : Application
{
    private readonly LinuxStatusIcon? _statusIcon;

    public App()
    {
    }

    public App(LinuxStatusIcon statusIcon)
    {
        _statusIcon = statusIcon;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        _statusIcon?.AttachTo(this);
    }
}
