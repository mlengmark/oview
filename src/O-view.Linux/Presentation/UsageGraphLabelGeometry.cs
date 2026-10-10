namespace OView.Linux.Presentation;

/// <summary>
/// Places a 90°-rotated date label under its own graph column (ADR-0008 D10b, gate G7 parity
/// slice P13), the Linux counterpart of <c>O-view.Tray</c>'s own type — independently
/// implemented, not shared (D1), though both happen to state the same 90°-rotation identity.
/// Ported from the source's own <c>BuildGraph</c>: a render transform does not move an element's
/// layout box, so the un-rotated <c>TextBlock</c>'s own measured size is not where the rotated
/// ink actually lands. The source's old code compensated with a constant offset that drifted
/// against the real value as soon as the font, DPI, or OS text scaling changed.
///
/// <para>This type takes the real measured size the caller got back from <c>TextBlock.Measure</c>
/// and returns where to place it — it never hard-codes an offset. <see cref="RotatedInkBounds"/>
/// states the 90° case as a closed-form rectangle, so the formula is directly testable with no
/// live visual tree.</para>
/// </summary>
public static class UsageGraphLabelGeometry
{
    /// <summary>
    /// The laid-out box <c>[0,w]×[0,h]</c>, rotated 90° about its own top-left corner, maps to
    /// <c>[-h,0]×[0,w]</c> — point <c>(x,y)</c> goes to <c>(-y,x)</c>, so the four corners land at
    /// <c>(0,0)</c>, <c>(0,w)</c>, <c>(-h,0)</c> and <c>(-h,w)</c>. The bounding box of those four
    /// points is exactly this rectangle.
    /// </summary>
    public static (double Left, double Top, double Width, double Height) RotatedInkBounds(
        double measuredWidth, double measuredHeight) =>
        (-measuredHeight, 0, measuredHeight, measuredWidth);

    /// <summary>
    /// The rotated label's left edge so its ink is horizontally centred on <paramref name="barCentreX"/>
    /// — the same column centre the bar above it is placed from, so a date can never drift from
    /// the bar it names as the column width or the gutter changes.
    /// </summary>
    public static double CentredLeft(double barCentreX, double inkLeft, double inkWidth) =>
        barCentreX - inkLeft - inkWidth / 2;

    /// <summary>The rotated label's top edge so its ink starts at <paramref name="labelAreaTop"/>.</summary>
    public static double Top(double labelAreaTop, double inkTop) => labelAreaTop - inkTop;

    /// <summary>A column's horizontal centre, shared by its bar and its date label so the two can
    /// never drift apart as the column width or gutter changes.</summary>
    public static double ColumnCentre(double columnLeft, double columnWidth) => columnLeft + columnWidth / 2;
}
