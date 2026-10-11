using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OView.App;

namespace OView.Tray.Presentation;

/// <summary>
/// ADR-0008 D11a's render-proof hook (gate G7 parity slice P24, OVI-718) for
/// <see cref="BrandedDialogWindow"/> and <see cref="WeeklyResetEntryDialog"/> — the same
/// mechanism <see cref="DetailWindowRenderProof"/> established for slice P0: a dedicated STA
/// thread builds the visual tree, measures it, and renders it to an offscreen
/// <see cref="RenderTargetBitmap"/>, without ever calling <see cref="Window.ShowDialog"/>. Source
/// <c>ui-spec.md</c> §5's own reasoning applies doubly to a dialog: "a modal cannot be
/// screenshotted by the run that would take the screenshot, so it has to be rendered."
/// </summary>
public static class BrandedDialogRenderProof
{
    public static void RenderConfirmDialogToFile(
        string title, string message, string confirmLabel, string cancelLabel, string detail,
        ThemePreference theme, string filePath)
    {
        File.WriteAllBytes(filePath, RenderConfirmDialog(title, message, confirmLabel, cancelLabel, detail, theme));
    }

    public static byte[] RenderConfirmDialog(
        string title, string message, string confirmLabel, string cancelLabel, string detail, ThemePreference theme) =>
        OnStaThread(() =>
        {
            var colors = WindowThemePalette.Resolve(theme);
            var visual = BrandedDialogLayout.Build(colors, confirmLabel, cancelLabel);
            visual.Title.Text = title;
            visual.Message.Text = message;
            visual.Detail.Text = detail;
            visual.Detail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Rasterize(visual.Root);
        });

    public static void RenderWeeklyResetDialogToFile(
        WeeklyResetEntry? current, ThemePreference theme, string filePath, string? error = null)
    {
        File.WriteAllBytes(filePath, RenderWeeklyResetDialog(current, theme, error));
    }

    public static byte[] RenderWeeklyResetDialog(WeeklyResetEntry? current, ThemePreference theme, string? error = null) =>
        OnStaThread(() =>
        {
            var colors = WindowThemePalette.Resolve(theme);
            var visual = WeeklyResetEntryLayout.Build(colors);
            visual.DayBox.SelectedItem = (current?.Day ?? DayOfWeek.Monday).ToString();
            visual.TimeBox.Text = current?.TimeText ?? "";
            visual.ClearButton.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;

            if (!string.IsNullOrEmpty(error))
            {
                visual.ErrorText.Text = error;
                visual.ErrorText.Visibility = Visibility.Visible;
            }

            return Rasterize(visual.Root);
        });

    /// <summary>
    /// Every WPF type below <see cref="FrameworkElement"/> asserts it was created on an STA
    /// thread — xUnit does not run tests on one, so each render spins up a dedicated STA thread
    /// for the whole build-measure-arrange-render sequence and joins it, the same pattern
    /// <see cref="DetailWindowRenderProof.Render"/> already established.
    /// </summary>
    private static byte[] OnStaThread(Func<byte[]> body)
    {
        byte[]? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = body();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    private static byte[] Rasterize(FrameworkElement root)
    {
        root.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)root.DesiredSize.Width), Math.Max(1, (int)root.DesiredSize.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
