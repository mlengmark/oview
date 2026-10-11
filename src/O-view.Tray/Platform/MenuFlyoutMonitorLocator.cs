using System.Runtime.InteropServices;
using OView.Tray.Presentation;

namespace OView.Tray.Platform;

/// <summary>
/// Gathers the two rectangles <see cref="MenuFlyoutPlacement.Compute"/> needs, plus the
/// monitor's effective DPI scale (gate G7 parity slice P21, OVI-738): the cursor's position
/// selects which monitor — never anything about where the flyout itself goes, and never
/// <c>Shell_NotifyIconGetRect</c>, which SNI does not support and this design does not use
/// (ADR-0008 D4).
///
/// <para>Adapter code, not unit-tested — the same boundary <c>DetailWindow</c> and
/// <see cref="OView.Tray.Platform.ForegroundWindowTaker"/>'s own P/Invoke calls already carry;
/// the decision this feeds, <see cref="MenuFlyoutPlacement.Compute"/>, is the tested half.
/// Falls back to a plausible 1080p monitor when the query fails, because a flyout placed
/// slightly wrong beats one not shown at all.</para>
/// </summary>
internal static class MenuFlyoutMonitorLocator
{
    public static (PixelRect Monitor, PixelRect Work, double Scale) Locate()
    {
        GetCursorPos(out var cursor);
        var hMonitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        RECT monitorRect, workRect;
        if (GetMonitorInfoW(hMonitor, ref info))
        {
            monitorRect = info.rcMonitor;
            workRect = info.rcWork;
        }
        else
        {
            monitorRect = workRect = new RECT { Right = 1920, Bottom = 1080 };
        }

        var scale = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0
            ? dpiX / 96.0
            : 1.0;

        return (ToPixelRect(monitorRect), ToPixelRect(workRect), scale);
    }

    private static PixelRect ToPixelRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    private const int MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
