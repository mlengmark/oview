using System.Globalization;
using OView.App;

namespace OView.Linux.Platform;

/// <summary>
/// Run-at-startup via the XDG Autostart spec — the Linux equivalent of the Windows Run key.
/// A <c>.desktop</c> file in <c>$XDG_CONFIG_HOME/autostart/</c> (normally
/// <c>~/.config/autostart/</c>) is launched at session start; deleting it disables that. The
/// file's presence is the single source of truth (ADR-0007 D4), exactly as the registry value
/// is on Windows, so nothing is duplicated into the shell's settings file.
///
/// <para><b>This is not the packaged application's <c>.desktop</c> file.</b> The one under
/// <c>/usr/share/applications</c> is root-owned, describes the app to the launcher, and must
/// never be touched by a settings toggle. This one is per-user and disposable. Do not make
/// either a symlink to the other.</para>
///
/// <para>This is ordinary text-file IO against an injectable directory — no Linux-only API —
/// so it is exercised by this project's own test suite wherever <c>dotnet test</c> runs. That
/// does not confirm a real desktop environment actually honours the file, which this
/// repository has never had a Linux runner to observe (ADR-0002/0004's "never observed"
/// caveat).</para>
/// </summary>
public sealed class XdgAutostartRegistration : IStartupRegistration
{
    public const string FileName = "o-view.desktop";

    private readonly string _directory;
    private readonly Func<string?> _executablePath;

    /// <summary><c>$XDG_CONFIG_HOME/autostart</c>, falling back to <c>~/.config/autostart</c>.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } configHome
            ? configHome
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart");

    /// <param name="directory">Autostart directory. Null means the real one.</param>
    /// <param name="executablePath">
    /// How to find the binary to launch. Injectable because <see cref="Environment.ProcessPath"/>
    /// is the test host's path under a test runner, not O-view's.
    /// </param>
    public XdgAutostartRegistration(string? directory = null, Func<string?>? executablePath = null)
    {
        _directory = directory ?? DefaultDirectory;
        _executablePath = executablePath ?? (() => Environment.ProcessPath);
    }

    public string FilePath => Path.Combine(_directory, FileName);

    public bool IsEnabled()
    {
        try
        {
            return File.Exists(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            Directory.CreateDirectory(_directory);
            File.WriteAllText(FilePath, BuildEntry(exe));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Disable()
    {
        try
        {
            File.Delete(FilePath);   // deleting a missing file is not an error
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The desktop entry. <c>X-GNOME-Autostart-enabled</c> is included because GNOME honours
    /// it to disable an entry without deleting the file — so an entry lacking it can be left
    /// in a state this class would report as enabled while the session ignores it.
    /// <c>Exec</c> is quoted: an install path containing a space would otherwise be read as a
    /// command plus arguments. The wording here is this skin's own (ADR-0007 D1 — the shell
    /// owns no wording), not shared with O-view.Tray.
    /// </summary>
    private static string BuildEntry(string executablePath) =>
        string.Join('\n',
        [
            "[Desktop Entry]",
            "Type=Application",
            "Name=O-view",
            "Comment=Claude usage and time until the next limit reset",
            string.Create(CultureInfo.InvariantCulture, $"Exec=\"{executablePath}\""),
            "Icon=o-view",
            "Terminal=false",
            "Categories=Utility;Monitor;",
            "X-GNOME-Autostart-enabled=true",
            "",
        ]);
}
