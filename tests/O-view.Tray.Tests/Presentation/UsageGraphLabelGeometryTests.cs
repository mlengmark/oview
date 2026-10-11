using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>
/// Proves <see cref="UsageGraphLabelGeometry"/> (ADR-0008 D10b, gate G7 parity slice P13) centres
/// a rotated date label from its own <em>measured</em> size, not a constant offset: two labels
/// with different measured widths (different text length or font) land at different left edges
/// even when centred on the same column, and the formula is exactly the one the type's own
/// remarks state — nothing here could pass by coincidence from a hard-coded number.
/// </summary>
public sealed class UsageGraphLabelGeometryTests
{
    [Fact]
    public void RotatedInkBoundsMapsTheLaidOutBoxByTheNinetyDegreeIdentity()
    {
        var (left, top, width, height) = UsageGraphLabelGeometry.RotatedInkBounds(measuredWidth: 20, measuredHeight: 8);

        Assert.Equal(-8, left);
        Assert.Equal(0, top);
        Assert.Equal(8, width);
        Assert.Equal(20, height);
    }

    /// <summary>
    /// After a 90° rotation, the text's own measured <em>height</em> (its line height — font
    /// size, DPI, or OS text-scaling) becomes the rotated box's horizontal extent — the measured
    /// <em>width</em> (how long the string is) becomes its vertical extent instead. So it is line
    /// height, not text length, that a correct centring formula must track: the source's own old
    /// bug used a constant that stood in for "about half a line height" and drifted the moment
    /// the font, DPI or OS text-scaling setting changed — exactly the dependency this proves.
    /// </summary>
    [Fact]
    public void CentredLeftDependsOnTheMeasuredLineHeightNotAConstant()
    {
        const double barCentreX = 100;

        var smallFont = UsageGraphLabelGeometry.RotatedInkBounds(measuredWidth: 20, measuredHeight: 8);
        var largeFont = UsageGraphLabelGeometry.RotatedInkBounds(measuredWidth: 20, measuredHeight: 12);

        var smallFontLeft = UsageGraphLabelGeometry.CentredLeft(barCentreX, smallFont.Left, smallFont.Width);
        var largeFontLeft = UsageGraphLabelGeometry.CentredLeft(barCentreX, largeFont.Left, largeFont.Width);

        // Same column centre, same text length, but a taller measured line height must sit at a
        // different left edge — a constant offset could never distinguish these two.
        Assert.NotEqual(smallFontLeft, largeFontLeft);

        // And the formula itself, not just "they differ": centred means the ink's own midpoint
        // lands exactly on barCentreX, for either measured size.
        Assert.Equal(barCentreX, smallFontLeft + smallFont.Left + smallFont.Width / 2, precision: 10);
        Assert.Equal(barCentreX, largeFontLeft + largeFont.Left + largeFont.Width / 2, precision: 10);
    }

    [Fact]
    public void TopPlacesTheInksOwnTopEdgeAtTheLabelAreaTop()
    {
        const double labelAreaTop = 58;
        var ink = UsageGraphLabelGeometry.RotatedInkBounds(measuredWidth: 20, measuredHeight: 8);

        var top = UsageGraphLabelGeometry.Top(labelAreaTop, ink.Top);

        Assert.Equal(labelAreaTop, top + ink.Top, precision: 10);
    }

    [Fact]
    public void ColumnCentreIsTheMidpointOfTheColumnsOwnWidth()
    {
        Assert.Equal(15, UsageGraphLabelGeometry.ColumnCentre(columnLeft: 10, columnWidth: 10));
        Assert.Equal(0, UsageGraphLabelGeometry.ColumnCentre(columnLeft: -5, columnWidth: 10));
    }
}
