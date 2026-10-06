using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using OView.App;

namespace OView.Tray.Platform;

/// <summary>
/// Windows' half of ADR-0009 D6: reads <c>AppsUseLightTheme</c> under
/// <c>HKCU\...\Themes\Personalize</c> (CONFIRMED as the source app's mechanism, ADR-0002 line
/// 52) and re-reads on every <see cref="Current"/> access rather than caching (D6 point 4).
///
/// <para>Live changes arrive as a broadcast <c>WM_SETTINGCHANGE</c>, which only reaches
/// top-level windows — not a message-only (<c>HWND_MESSAGE</c>) window — so this owns one
/// invisible, unparented <see cref="NativeWindow"/> purely to receive that broadcast. The
/// window is never shown and never participates in any UI.</para>
///
/// <para>The subkey path and value name are injectable so tests exercise a real registry
/// round-trip against a disposable subkey under <c>HKCU\Software</c>, the same shape
/// <see cref="RegistryStartupRegistration"/> already uses.</para>
/// </summary>
public sealed class RegistryThemeSource : IThemeSource, IDisposable
{
    public const string DefaultSubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string DefaultValueName = "AppsUseLightTheme";

    private const int WM_SETTINGCHANGE = 0x001A;

    private readonly string _subKeyPath;
    private readonly string _valueName;
    private readonly ThemeChangeWindow _window;

    public event EventHandler<ThemePreference>? Changed;

    public RegistryThemeSource(string? subKeyPath = null, string? valueName = null)
    {
        _subKeyPath = subKeyPath ?? DefaultSubKeyPath;
        _valueName = valueName ?? DefaultValueName;
        _window = new ThemeChangeWindow(() => Changed?.Invoke(this, Current));
    }

    public ThemePreference Current
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath);
                return key?.GetValue(_valueName) switch
                {
                    int value => value == 0 ? ThemePreference.Dark : ThemePreference.Light,
                    _ => ThemePreference.Unknown
                };
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
            {
                return ThemePreference.Unknown;
            }
        }
    }

    public void Dispose() => _window.Dispose();

    /// <summary>
    /// Exposed only so tests can <c>SendMessage</c> a real <c>WM_SETTINGCHANGE</c> to this
    /// instance's own window, driving the real <see cref="ThemeChangeWindow.WndProc"/> — the
    /// same message the OS broadcasts, without depending on the OS to actually change its
    /// theme during a test run. <c>SendMessage</c> to a window owned by the calling thread
    /// invokes the window procedure directly, so no message loop is needed in tests.
    /// </summary>
    internal nint WindowHandleForTests => _window.Handle;

    internal static int SettingChangeMessageForTests => WM_SETTINGCHANGE;

    private sealed class ThemeChangeWindow : NativeWindow, IDisposable
    {
        private readonly Action _onSettingChange;

        public ThemeChangeWindow(Action onSettingChange)
        {
            _onSettingChange = onSettingChange;

            // WS_POPUP, no WS_VISIBLE: a real top-level window so it is reachable by the
            // OS's WM_SETTINGCHANGE broadcast, but it is never shown and owns no UI.
            CreateHandle(new CreateParams
            {
                Caption = "OView.Tray.ThemeChangeListener",
                Style = unchecked((int)0x80000000)
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETTINGCHANGE)
            {
                _onSettingChange();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }
}
