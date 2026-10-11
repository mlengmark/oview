using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OView.App;

namespace OView.Tray.Presentation;

/// <summary>Which edge of the monitor a synthetic render-proof scene puts the taskbar on
/// (gate G7 parity slice P21, OVI-738) — the four positions the issue requires a fixture
/// for.</summary>
public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>
/// ADR-0008 D11a's render-proof hook for the branded menu flyout (gate G7 parity slice P21,
/// OVI-738): the same offscreen mechanism <see cref="DetailWindowRenderProof"/> and
/// <see cref="BrandedDialogRenderProof"/> already established — a dedicated STA thread builds the
/// visual tree and renders it, without ever calling <see cref="Window.Show"/>.
///
/// <para>Unlike those two, what this proves is as much about <i>where</i> the card lands as what
/// it looks like: each render draws a synthetic monitor/work-area scene for one
/// <see cref="TaskbarEdge"/> and places the flyout card at the corner
/// <see cref="MenuFlyoutPlacement.Compute"/> actually picked for it, scaled down to a reasonable
/// image size. A reviewer comparing the four renders side by side can see the card always sits
/// adjacent to the drawn taskbar strip and never at a fixed screen corner regardless of
/// position.</para>
/// </summary>
internal static class MenuFlyoutRenderProof
{
    /// <summary>A synthetic 1920x1080 monitor, scaled down by this factor for the rendered
    /// scene — large enough that the card's proportions stay legible, small enough that the PNG
    /// is not a full-resolution screenshot of nothing.</summary>
    private const double DemoScale = 1.0 / 4.0;

    private const int MonitorWidthPx = 1920;
    private const int MonitorHeightPx = 1080;
    private const int TaskbarThicknessPx = 48;

    private static readonly string[] PlaceholderRows = { "Open O-view", "Run at startup", "Check for updates…", "Exit" };

    public static void RenderToFile(TaskbarEdge edge, ThemePreference theme, string filePath)
    {
        File.WriteAllBytes(filePath, Render(edge, theme));
    }

    public static byte[] Render(TaskbarEdge edge, ThemePreference theme)
    {
        byte[]? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = RenderOnStaThread(edge, theme);
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

    private static byte[] RenderOnStaThread(TaskbarEdge edge, ThemePreference theme)
    {
        var (monitor, work) = Scene(edge);
        var colors = WindowThemePalette.Resolve(theme);

        var cardRowCount = PlaceholderRows.Length;
        var cardWidthPx = (int)Math.Round(MenuFlyoutChrome.CardWidth);
        var cardHeightPx = (int)Math.Round(MenuFlyoutChrome.ContentHeight(cardRowCount));
        var placement = MenuFlyoutPlacement.Compute(monitor, work, cardWidthPx, cardHeightPx);

        var sceneRoot = new Canvas
        {
            Width = monitor.Width * DemoScale,
            Height = monitor.Height * DemoScale,
        };

        sceneRoot.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = monitor.Width * DemoScale,
            Height = monitor.Height * DemoScale,
            Fill = ToBrush(colors.Background),
            Stroke = ToBrush(colors.Border),
            StrokeThickness = 1,
        });

        var taskbarRect = TaskbarRect(edge, monitor);
        var taskbarVisual = new System.Windows.Shapes.Rectangle
        {
            Width = taskbarRect.Width * DemoScale,
            Height = taskbarRect.Height * DemoScale,
            Fill = ToBrush(colors.Border),
        };
        Canvas.SetLeft(taskbarVisual, taskbarRect.X * DemoScale);
        Canvas.SetTop(taskbarVisual, taskbarRect.Y * DemoScale);
        sceneRoot.Children.Add(taskbarVisual);

        var card = MenuFlyoutChrome.Build(colors, "v1.0.0", PlaceholderRows).Root;
        Canvas.SetLeft(card, placement.Left * DemoScale);
        Canvas.SetTop(card, placement.Top * DemoScale);
        sceneRoot.Children.Add(card);

        sceneRoot.Measure(new System.Windows.Size(sceneRoot.Width, sceneRoot.Height));
        sceneRoot.Arrange(new Rect(0, 0, sceneRoot.Width, sceneRoot.Height));

        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)sceneRoot.Width), Math.Max(1, (int)sceneRoot.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sceneRoot);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>The monitor/work-area pair for a given taskbar edge — a full monitor and a work
    /// area inset by <see cref="TaskbarThicknessPx"/> on that one edge, matching what
    /// <c>GetMonitorInfoW</c> would report for a real taskbar docked there.</summary>
    private static (PixelRect Monitor, PixelRect Work) Scene(TaskbarEdge edge)
    {
        var monitor = new PixelRect(0, 0, MonitorWidthPx, MonitorHeightPx);
        var work = edge switch
        {
            TaskbarEdge.Top => new PixelRect(0, TaskbarThicknessPx, MonitorWidthPx, MonitorHeightPx - TaskbarThicknessPx),
            TaskbarEdge.Left => new PixelRect(TaskbarThicknessPx, 0, MonitorWidthPx - TaskbarThicknessPx, MonitorHeightPx),
            TaskbarEdge.Right => new PixelRect(0, 0, MonitorWidthPx - TaskbarThicknessPx, MonitorHeightPx),
            _ => new PixelRect(0, 0, MonitorWidthPx, MonitorHeightPx - TaskbarThicknessPx),
        };

        return (monitor, work);
    }

    private static PixelRect TaskbarRect(TaskbarEdge edge, PixelRect monitor) => edge switch
    {
        TaskbarEdge.Top => new PixelRect(monitor.X, monitor.Y, monitor.Width, TaskbarThicknessPx),
        TaskbarEdge.Left => new PixelRect(monitor.X, monitor.Y, TaskbarThicknessPx, monitor.Height),
        TaskbarEdge.Right => new PixelRect(monitor.Right - TaskbarThicknessPx, monitor.Y, TaskbarThicknessPx, monitor.Height),
        _ => new PixelRect(monitor.X, monitor.Bottom - TaskbarThicknessPx, monitor.Width, TaskbarThicknessPx),
    };

    private static SolidColorBrush ToBrush(RgbColor color) => new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
}
