using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OView.Tray.Presentation;

/// <summary>
/// Turns <see cref="StatusIconGlyphRenderer.BuildBgra32"/>'s pixel buffer into a real
/// <see cref="Icon"/>. Covered directly by <c>StatusIconFactoryTests</c> — allocating a GDI
/// bitmap and icon handle needs no interactive desktop, so this runs for real on CI, unlike
/// the <c>NotifyIcon</c>/native-window wiring in <c>TrayStatusIcon</c>.
/// </summary>
internal static class StatusIconFactory
{
    /// <summary>
    /// <see cref="Icon.FromHandle"/> wraps a GDI icon handle it does not own, which leaks if
    /// never destroyed (documented .NET gotcha). Cloning the icon forces a private copy, so the
    /// source handle can be destroyed immediately after.
    /// </summary>
    public static Icon CreateIcon(byte[] bgra32, int sizePx)
    {
        ArgumentNullException.ThrowIfNull(bgra32);
        if (sizePx <= 0 || bgra32.Length != sizePx * sizePx * 4)
        {
            throw new ArgumentException("Pixel buffer size does not match the requested icon size.", nameof(bgra32));
        }

        using var bitmap = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, sizePx, sizePx);
        var bits = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = sizePx * 4;
            if (bits.Stride == rowBytes)
            {
                Marshal.Copy(bgra32, 0, bits.Scan0, bgra32.Length);
            }
            else
            {
                // A 32bpp row never needs padding, but copy per-row defensively in case a
                // future GDI+ implementation ever returns a padded stride.
                for (var y = 0; y < sizePx; y++)
                {
                    Marshal.Copy(bgra32, y * rowBytes, bits.Scan0 + (y * bits.Stride), rowBytes);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
