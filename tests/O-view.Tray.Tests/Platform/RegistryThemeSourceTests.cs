using System.Runtime.InteropServices;
using Microsoft.Win32;
using OView.App;
using OView.Tray.Platform;

namespace OView.Tray.Tests.Platform;

/// <summary>
/// ADR-0009 D6/slice 5: the Windows theme read and its live-change notification. Exercises a
/// real HKCU round-trip against a GUID-suffixed scratch subkey under <c>HKCU\Software</c>,
/// never the real <c>...\Themes\Personalize</c> key — the same shape
/// <see cref="RegistryStartupRegistrationTests"/> already uses.
/// </summary>
public class RegistryThemeSourceTests : IDisposable
{
    // WM_SETTINGCHANGE is one of the handful of messages Windows refuses to queue via
    // PostMessage (ERROR_MESSAGE_SYNC_ONLY) — the real OS broadcasts it synchronously via
    // SendMessageTimeout, so the test does the same. SendMessage to a window owned by the
    // calling thread invokes its WndProc directly, with no message loop needed.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private readonly string _subKeyPath;

    public RegistryThemeSourceTests()
    {
        _subKeyPath = @"Software\OViewTests\" + Guid.NewGuid();
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_subKeyPath, throwOnMissingSubKey: false);
    }

    private RegistryThemeSource Subject() => new(_subKeyPath, "AppsUseLightTheme");

    [Fact]
    public void AbsentValueIsUnknown_NeverGuessed()
    {
        using var subject = Subject();

        Assert.Equal(ThemePreference.Unknown, subject.Current);
    }

    [Fact]
    public void ZeroIsDark()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKeyPath);
        key.SetValue("AppsUseLightTheme", 0, RegistryValueKind.DWord);
        using var subject = Subject();

        Assert.Equal(ThemePreference.Dark, subject.Current);
    }

    [Fact]
    public void NonZeroIsLight()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKeyPath);
        key.SetValue("AppsUseLightTheme", 1, RegistryValueKind.DWord);
        using var subject = Subject();

        Assert.Equal(ThemePreference.Light, subject.Current);
    }

    [Fact]
    public void CurrentIsReReadOnEveryAccess_NotCached()
    {
        using var subject = Subject();
        Assert.Equal(ThemePreference.Unknown, subject.Current);

        using var key = Registry.CurrentUser.CreateSubKey(_subKeyPath);
        key.SetValue("AppsUseLightTheme", 1, RegistryValueKind.DWord);

        Assert.Equal(ThemePreference.Light, subject.Current);
    }

    [Fact]
    public void RealWM_SETTINGCHANGE_PostedToTheWindowRaisesChangedWithTheFreshValue()
    {
        using var subject = Subject();
        ThemePreference? raised = null;
        subject.Changed += (_, preference) => raised = preference;

        using var key = Registry.CurrentUser.CreateSubKey(_subKeyPath);
        key.SetValue("AppsUseLightTheme", 0, RegistryValueKind.DWord);

        SendMessage(subject.WindowHandleForTests, RegistryThemeSource.SettingChangeMessageForTests, 0, 0);

        Assert.Equal(ThemePreference.Dark, raised);
    }

    [Fact]
    public void UnrelatedMessagesDoNotRaiseChanged()
    {
        using var subject = Subject();
        var raised = false;
        subject.Changed += (_, _) => raised = true;

        SendMessage(subject.WindowHandleForTests, 0x0001 /* WM_CREATE, never broadcast here */, 0, 0);

        Assert.False(raised);
    }

    [Fact]
    public void DefaultsMatchTheRealPersonalizeKey()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", RegistryThemeSource.DefaultSubKeyPath);
        Assert.Equal("AppsUseLightTheme", RegistryThemeSource.DefaultValueName);
    }
}
