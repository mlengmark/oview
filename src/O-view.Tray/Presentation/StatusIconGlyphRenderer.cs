using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// The pure half of ADR-0008 slice 4's status icon (OVI-371): maps a <see cref="UsageLevel"/>
/// and a DPI scale to a 32bpp BGRA pixel buffer. Takes no OS handle and owns no resource, so it
/// is fully unit-testable without a GDI call — the OS adapter (<c>StatusIconFactory</c>,
/// <c>TrayStatusIcon</c>) turns this buffer into a real icon.
/// </summary>
internal static class StatusIconGlyphRenderer
{
    /// <summary>The icon's logical (96 DPI / 100%) size. Windows notification-area icons are
    /// conventionally 16x16 at 100% scale.</summary>
    public const int LogicalSizePx = 16;

    /// <summary>Scales <see cref="LogicalSizePx"/> by a per-monitor DPI v2 scale factor
    /// (1.0 = 96 DPI), never below the logical size.</summary>
    public static int PixelSizeForDpiScale(double dpiScale)
    {
        if (dpiScale <= 0 || double.IsNaN(dpiScale))
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale), dpiScale, "DPI scale must be a positive number.");
        }

        return Math.Max(LogicalSizePx, (int)Math.Round(LogicalSizePx * dpiScale));
    }

    /// <summary>
    /// The colour a <see cref="UsageLevel"/> renders as. This is a presentation decision this
    /// skin owns outright (ADR-0008 D2) — it does not invent a new level, a new threshold, or a
    /// fourth band; it only picks the RGB a skin must pick for each of Core's three existing
    /// ones.
    /// </summary>
    public static (byte B, byte G, byte R, byte A) ColorFor(UsageLevel level) => level switch
    {
        UsageLevel.Green => ((byte)70, (byte)160, (byte)50, (byte)255),
        UsageLevel.Amber => ((byte)5, (byte)165, (byte)235, (byte)255),
        UsageLevel.Red => ((byte)45, (byte)50, (byte)210, (byte)255),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown UsageLevel."),
    };

    /// <summary>
    /// Builds a top-down, straight-alpha, 32-bit BGRA pixel buffer (row-major, 4 bytes/pixel) of
    /// a filled circle in <paramref name="level"/>'s colour on a transparent background — the one
    /// platform-neutral fact this slice's tests pin without touching a GDI handle.
    /// </summary>
    public static byte[] BuildBgra32(UsageLevel level, int sizePx)
    {
        if (sizePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizePx), sizePx, "Icon size must be positive.");
        }

        var (b, g, r, a) = ColorFor(level);
        var pixels = new byte[sizePx * sizePx * 4];

        var center = (sizePx - 1) / 2.0;
        var radius = sizePx / 2.0 - 0.5;
        var radiusSquared = radius * radius;

        for (var y = 0; y < sizePx; y++)
        {
            var dy = y - center;
            for (var x = 0; x < sizePx; x++)
            {
                var dx = x - center;
                var inside = (dx * dx) + (dy * dy) <= radiusSquared;

                var i = ((y * sizePx) + x) * 4;
                pixels[i + 0] = inside ? b : (byte)0;
                pixels[i + 1] = inside ? g : (byte)0;
                pixels[i + 2] = inside ? r : (byte)0;
                pixels[i + 3] = inside ? a : (byte)0;
            }
        }

        return pixels;
    }
}
