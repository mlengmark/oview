using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OView.Linux.Presentation;

/// <summary>
/// Turns <see cref="StatusIconGlyphRenderer.BuildBgra32"/>'s pixel buffer into a real Avalonia
/// <see cref="WindowIcon"/>. The Linux counterpart of <c>O-view.Tray</c>'s
/// <c>StatusIconFactory</c> (slice 4) — independently implemented (D1): that one allocates a
/// GDI bitmap/icon handle, which needs no interactive desktop and is covered directly by
/// <c>StatusIconFactoryTests</c> on Windows. This one constructs an Avalonia
/// <see cref="Bitmap"/>, which requires <see cref="IPlatformRenderInterface"/> to already be
/// registered — true for real at runtime (<c>Program.cs</c> calls
/// <c>AppBuilder.UsePlatformDetect</c> before this is ever invoked) but not in a bare xUnit
/// process with no Avalonia platform initialized, so unlike the Windows factory this one is
/// <b>not</b> covered by a direct unit test — the same "ships labelled unverified" boundary
/// ADR-0008's slicing table already records for this entire slice.
/// </summary>
internal static class StatusIconFactory
{
    public static WindowIcon CreateIcon(byte[] bgra32, int sizePx)
    {
        ArgumentNullException.ThrowIfNull(bgra32);
        if (sizePx <= 0 || bgra32.Length != sizePx * sizePx * 4)
        {
            throw new ArgumentException("Pixel buffer size does not match the requested icon size.", nameof(bgra32));
        }

        var stride = sizePx * 4;
        var handle = Marshal.AllocHGlobal(bgra32.Length);
        try
        {
            Marshal.Copy(bgra32, 0, handle, bgra32.Length);

            // Avalonia's Bitmap constructor copies the pixel data into its own backing store
            // synchronously, so the unmanaged buffer below is safe to free as soon as it
            // returns — the same "copy, then free the temporary handle" shape
            // O-view.Tray's StatusIconFactory uses around Bitmap.GetHicon.
            using var bitmap = new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul,
                handle,
                new PixelSize(sizePx, sizePx),
                new Vector(96, 96),
                stride);

            return new WindowIcon(bitmap);
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }
    }
}
