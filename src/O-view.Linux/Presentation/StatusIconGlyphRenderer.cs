using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// The pure half of ADR-0008 slice 9's status icon (OVI-403): maps a <see cref="UsageLevel"/>
/// to a 32bpp BGRA pixel buffer. Structurally mirrors <c>O-view.Tray</c>'s
/// <c>StatusIconGlyphRenderer</c> (slice 4, OVI-371) but is an independent implementation — no
/// shared code between the two skins (ADR-0008 D1). Takes no toolkit handle and owns no
/// resource, so it is fully unit-testable without a display; the Avalonia adapter
/// (<c>StatusIconFactory</c>, <c>LinuxStatusIcon</c>) turns this buffer into a real icon on
/// every snapshot — never a static themed icon name, which cannot render a live usage gauge
/// (CONFIRMED by the source's own spike).
/// </summary>
internal static class StatusIconGlyphRenderer
{
    /// <summary>The icon's pixel size. 24px matches the common freedesktop
    /// StatusNotifierItem/panel icon size; unlike Windows' per-monitor DPI v2, no per-host
    /// scale signal exists on this platform for a status-notifier icon, so this is the only
    /// size this slice renders.</summary>
    public const int IconSizePx = 24;

    /// <summary>
    /// The colour a <see cref="UsageLevel"/> renders as (ADR-0008 D2 — this skin's own
    /// presentation decision, independently picked from <c>O-view.Tray</c>'s). Values are
    /// straight (non-premultiplied) BGRA, matching <see cref="BuildBgra32"/>'s output.
    /// </summary>
    public static (byte B, byte G, byte R, byte A) ColorFor(UsageLevel level) => level switch
    {
        UsageLevel.Green => ((byte)80, (byte)170, (byte)60, (byte)255),
        UsageLevel.Amber => ((byte)10, (byte)175, (byte)245, (byte)255),
        UsageLevel.Red => ((byte)50, (byte)55, (byte)220, (byte)255),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown UsageLevel."),
    };

    /// <summary>
    /// Builds a top-down, straight-alpha, 32-bit BGRA pixel buffer (row-major, 4 bytes/pixel) of
    /// a filled circle in <paramref name="level"/>'s colour on a transparent background — the
    /// one platform-neutral fact this slice's tests pin without touching Avalonia.
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
