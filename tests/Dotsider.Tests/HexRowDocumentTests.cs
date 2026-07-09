using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Hex Row Document.
/// </summary>
[TestClass]
public class HexRowDocumentTests
{
    /// <summary>
    /// Verifies get line text binary content does not throw.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineText_BinaryContent_DoesNotThrow()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        for (var line = 1; line <= hexDoc.LineCount; line++)
            hexDoc.GetLineText(line); // should not throw
    }

    /// <summary>
    /// Verifies get line length binary content does not throw.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineLength_BinaryContent_DoesNotThrow()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        for (var line = 1; line <= hexDoc.LineCount; line++)
            hexDoc.GetLineLength(line); // should not throw
    }

    /// <summary>
    /// Verifies get line text last row binary content.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineText_LastRow_BinaryContent()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        // Last row is the worst case for byte-to-char misalignment
        var lastRow = hexDoc.LineCount;
        var text = hexDoc.GetLineText(lastRow);
        Assert.IsNotNull(text);
    }

    /// <summary>
    /// Verifies get line length consistent with get line text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineLength_ConsistentWithGetLineText()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        for (var line = 1; line <= hexDoc.LineCount; line++)
        {
            var text = hexDoc.GetLineText(line);
            var length = hexDoc.GetLineLength(line);
            Assert.AreEqual(text.Length, length);
        }
    }

    /// <summary>
    /// Verifies get line text empty document returns empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineText_EmptyDocument_ReturnsEmpty()
    {
        var doc = new Hex1bDocument([]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.AreEqual(1, hexDoc.LineCount);
        Assert.AreEqual("", hexDoc.GetLineText(1));
    }

    /// <summary>
    /// Verifies get line text single byte.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineText_SingleByte()
    {
        var doc = new Hex1bDocument([0x42]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.AreEqual(1, hexDoc.LineCount);
        var text = hexDoc.GetLineText(1);
        Assert.IsNotEmpty(text);
    }

    /// <summary>
    /// Verifies line count matches ceil division.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(1, 1)]
    [DataRow(15, 1)]
    [DataRow(16, 1)]
    [DataRow(17, 2)]
    [DataRow(255, 16)]
    [DataRow(256, 16)]
    public void LineCount_MatchesCeilDivision(int byteCount, int expectedLines)
    {
        var bytes = new byte[byteCount];
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.AreEqual(expectedLines, hexDoc.LineCount);
    }

    /// <summary>
    /// Verifies get line text out of range throws.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineText_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument(new byte[32]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => hexDoc.GetLineText(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => hexDoc.GetLineText(hexDoc.LineCount + 1));
    }

    /// <summary>
    /// Verifies get line length out of range throws.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetLineLength_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument(new byte[32]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => hexDoc.GetLineLength(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => hexDoc.GetLineLength(hexDoc.LineCount + 1));
    }
}
