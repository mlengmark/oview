using Microsoft.Win32;
using OView.App;

namespace OView.Tray.Platform;

/// <summary>
/// Run-at-startup via <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> — per-user,
/// no elevation. The registry value is the single source of truth (ADR-0007 D4); nothing is
/// duplicated into the shell's settings file.
///
/// <para>The subkey path, value name, and executable-path lookup are injectable so tests
/// exercise a real registry round-trip against a disposable subkey under
/// <c>HKCU\Software</c> rather than touching the real Run key.</para>
/// </summary>
public sealed class RegistryStartupRegistration : IStartupRegistration
{
    public const string DefaultSubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "O-view";

    private readonly string _subKeyPath;
    private readonly string _valueName;
    private readonly Func<string?> _executablePath;

    /// <param name="subKeyPath">Subkey under HKCU. Null means the real Run key.</param>
    /// <param name="valueName">Value name within the subkey. Null means the real value name.</param>
    /// <param name="executablePath">
    /// How to find the binary to register. Injectable because <see cref="Environment.ProcessPath"/>
    /// is the test host's path under a test runner, not O-view's.
    /// </param>
    public RegistryStartupRegistration(string? subKeyPath = null, string? valueName = null, Func<string?>? executablePath = null)
    {
        _subKeyPath = subKeyPath ?? DefaultSubKeyPath;
        _valueName = valueName ?? DefaultValueName;
        _executablePath = executablePath ?? (() => Environment.ProcessPath);
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath);
            return key?.GetValue(_valueName) is not null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    public bool Enable()
    {
        if (_executablePath() is not { Length: > 0 } exe)
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(_subKeyPath);
            key.SetValue(_valueName, $"\"{exe}\"");
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKeyPath, writable: true);
            key?.DeleteValue(_valueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
