using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OView.App;

namespace OView.Linux.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook (gate G7 parity slice P24, OVI-718) for
/// <see cref="BrandedDialogWindow"/> and <see cref="WeeklyResetEntryDialog"/>, the Linux
/// counterpart of <c>O-view.Tray</c>'s own <c>BrandedDialogRenderProof</c> — independently
/// implemented, not shared (D1): the same mechanism this skin's own
/// <c>DetailWindowRenderProof</c> established for slice P0 — <see cref="EnsurePlatformInitialized"/>
/// registers Avalonia's render interface once per process without ever entering a message loop
/// or showing a window.
///
/// <para><b>Discovered proving this slice</b> (CONFIRMED by grep: no
/// <c>DetailWindowRenderProofTests</c> file exists, so no prior xUnit test in this project had
/// ever called <c>AppBuilder.SetupWithoutStarting()</c>): Avalonia's
/// <see cref="Avalonia.Threading.Dispatcher"/> binds a single, process-wide singleton to
/// whichever thread first touches it, and xUnit's default parallel test collections can race two
/// test classes onto two different threads — one merely constructing a plain Avalonia control
/// (<c>HoverCardTests</c>' own <c>TextBlock()</c>), the other calling <c>AppBuilder.Setup()</c>
/// here — which throws regardless of call order. <c>tests/O-view.Linux.Tests/AssemblyFixtures.cs</c>
/// disables collection parallelization for this whole test project so every test, across every
/// class, runs on one consistent thread instead; this type adds no thread-marshalling of its own
/// beyond that, same as its Windows-skin counterpart needs none.</para>
/// </summary>
public static class BrandedDialogRenderProof
{
    private static readonly object InitLock = new();
    private static bool _platformInitialized;

    public static void RenderConfirmDialogToFile(
        string title, string message, string confirmLabel, string cancelLabel, string detail,
        ThemePreference theme, string filePath)
    {
        File.WriteAllBytes(filePath, RenderConfirmDialog(title, message, confirmLabel, cancelLabel, detail, theme));
    }

    public static byte[] RenderConfirmDialog(
        string title, string message, string confirmLabel, string cancelLabel, string detail, ThemePreference theme)
    {
        EnsurePlatformInitialized();

        var colors = LinuxWindowThemePalette.Resolve(theme);
        var visual = BrandedDialogLayout.Build(colors, confirmLabel, cancelLabel);
        visual.Title.Text = title;
        visual.Message.Text = message;
        visual.Detail.Text = detail;
        visual.Detail.IsVisible = detail.Length > 0;
        return Rasterize(visual.Root);
    }

    public static void RenderWeeklyResetDialogToFile(
        WeeklyResetEntry? current, ThemePreference theme, string filePath, string? error = null)
    {
        File.WriteAllBytes(filePath, RenderWeeklyResetDialog(current, theme, error));
    }

    public static byte[] RenderWeeklyResetDialog(WeeklyResetEntry? current, ThemePreference theme, string? error = null)
    {
        EnsurePlatformInitialized();

        var colors = LinuxWindowThemePalette.Resolve(theme);
        var visual = WeeklyResetEntryLayout.Build(colors);
        visual.DayBox.SelectedItem = (current?.Day ?? DayOfWeek.Monday).ToString();
        visual.TimeBox.Text = current?.TimeText ?? "";
        visual.ClearButton.IsVisible = current is not null;

        if (!string.IsNullOrEmpty(error))
        {
            visual.ErrorText.Text = error;
            visual.ErrorText.IsVisible = true;
        }

        return Rasterize(visual.Root);
    }

    /// <summary>Exposed for <c>BrandedDialogRenderProofTests</c>' own "primary names the action"
    /// assertion: building the confirm/cancel content needs no rendering, but constructing its
    /// secondary button's <see cref="Avalonia.Input.Cursor"/> still needs Avalonia's platform
    /// registered, same as every render call above.</summary>
    internal static string ResolveConfirmLabelText(string confirmLabel, string cancelLabel, ThemePreference theme)
    {
        EnsurePlatformInitialized();
        return BrandedDialogLayout.Build(LinuxWindowThemePalette.Resolve(theme), confirmLabel, cancelLabel).ConfirmLabel.Text ?? "";
    }

    /// <summary>Same one-time registration <c>DetailWindowRenderProof.EnsurePlatformInitialized</c>
    /// already uses: <c>AppBuilder.Configure&lt;App&gt;().UsePlatformDetect().SetupWithoutStarting()</c>
    /// registers Avalonia's render interface (needed by <see cref="RenderTargetBitmap"/>) without
    /// creating a window or entering a message loop, and may only run once per process.</summary>
    private static void EnsurePlatformInitialized()
    {
        if (_platformInitialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_platformInitialized)
            {
                return;
            }

            AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
            _platformInitialized = true;
        }
    }

    private static byte[] Rasterize(Border root)
    {
        root.Measure(Size.Infinity);
        root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)root.DesiredSize.Width), Math.Max(1, (int)root.DesiredSize.Height)));
        bitmap.Render(root);

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }
}
