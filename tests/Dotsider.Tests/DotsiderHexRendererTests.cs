using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dotsider Hex Renderer.
/// </summary>
[TestClass]
public class DotsiderHexRendererTests
{
    // --- CalculateLayout tests ---

    /// <summary>
    /// Verifies calculate layout snaps to expected bytes per row.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(140, 32)]  // Wide terminal: max snap point (4*32+11=139)
    [DataRow(120, 16)]  // 120 cols: (120-11)/4=27, snaps to 16
    [DataRow(80, 16)]   // Standard terminal
    [DataRow(50, 8)]    // Narrow terminal
    [DataRow(20, 1)]    // Very narrow: minimum snap
    [DataRow(11, 1)]    // Minimum viable width (4*1+11=15, so 1 fits barely)
    public void CalculateLayout_SnapsToExpectedBytesPerRow(int width, int expectedBytesPerRow)
    {
        var result = DotsiderHexRenderer.CalculateLayout(width);
        Assert.AreEqual(expectedBytesPerRow, result);
    }

    /// <summary>
    /// Verifies calculate layout always returns snap point.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CalculateLayout_AlwaysReturnsSnapPoint()
    {
        int[] snaps = [1, 8, 16, 32];
        for (var width = 1; width <= 300; width++)
        {
            var result = DotsiderHexRenderer.CalculateLayout(width);
            Assert.Contains(result, snaps);
        }
    }

    /// <summary>
    /// Verifies calculate layout never exceeds max32.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CalculateLayout_NeverExceedsMax32()
    {
        var result = DotsiderHexRenderer.CalculateLayout(10_000);
        Assert.AreEqual(32, result);
    }

    /// <summary>
    /// Verifies calculate layout monotonically increases.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CalculateLayout_MonotonicallyIncreases()
    {
        var prev = DotsiderHexRenderer.CalculateLayout(1);
        for (var width = 2; width <= 300; width++)
        {
            var current = DotsiderHexRenderer.CalculateLayout(width);
            Assert.IsGreaterThanOrEqualTo(prev, current, $"Width {width}: {current} < {prev}");
            prev = current;
        }
    }

    // --- GetByteCategoryFgAnsi tests ---

    /// <summary>
    /// Verifies get byte category fg ansi null byte returns null color.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_NullByte_ReturnsNullColor()
    {
        var result = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x00);
        Assert.IsNotNull(result);
        Assert.Contains("\x1b[", result); // ANSI escape
    }

    /// <summary>
    /// Verifies get byte category fg ansi printable ascii returns printable color.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_PrintableAscii_ReturnsPrintableColor()
    {
        var letterA = DotsiderHexRenderer.GetByteCategoryFgAnsi((byte)'A');
        var digit0 = DotsiderHexRenderer.GetByteCategoryFgAnsi((byte)'0');
        var tilde = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x7E);

        // All printable bytes should get the same color
        Assert.AreEqual(letterA, digit0);
        Assert.AreEqual(letterA, tilde);
    }

    /// <summary>
    /// Verifies get byte category fg ansi whitespace returns whitespace color.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_Whitespace_ReturnsWhitespaceColor()
    {
        var tab = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x09);
        var lf = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x0A);
        var cr = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x0D);

        Assert.AreEqual(tab, lf);
        Assert.AreEqual(tab, cr);
    }

    /// <summary>
    /// Verifies get byte category fg ansi control returns control color.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_Control_ReturnsControlColor()
    {
        var bel = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x07);
        var esc = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x1B);

        // Control chars (excluding whitespace) get the same color
        Assert.AreEqual(bel, esc);
    }

    /// <summary>
    /// Verifies get byte category fg ansi high byte returns high byte color.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_HighByte_ReturnsHighByteColor()
    {
        var result = DotsiderHexRenderer.GetByteCategoryFgAnsi(0xFF);
        Assert.IsNotNull(result);

        // Should differ from printable
        var printable = DotsiderHexRenderer.GetByteCategoryFgAnsi((byte)'A');
        Assert.AreNotEqual(printable, result);
    }

    /// <summary>
    /// Verifies get byte category fg ansi all categories are distinct.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetByteCategoryFgAnsi_AllCategories_AreDistinct()
    {
        var nullColor = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x00);
        var whitespace = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x09);
        var control = DotsiderHexRenderer.GetByteCategoryFgAnsi(0x01);
        var printable = DotsiderHexRenderer.GetByteCategoryFgAnsi((byte)'A');
        var high = DotsiderHexRenderer.GetByteCategoryFgAnsi(0xFF);

        var colors = new HashSet<string> { nullColor, whitespace, control, printable, high };
        Assert.HasCount(5, colors);
    }
}
