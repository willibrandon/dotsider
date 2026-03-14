using Hex1b.Documents;

namespace Dotsider.Tests;

public class HexRowDocumentTests
{
    [Fact(Timeout = 30_000)]
    public void GetLineText_BinaryContent_DoesNotThrow()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        for (var line = 1; line <= hexDoc.LineCount; line++)
            hexDoc.GetLineText(line); // should not throw
    }

    [Fact(Timeout = 30_000)]
    public void GetLineLength_BinaryContent_DoesNotThrow()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        for (var line = 1; line <= hexDoc.LineCount; line++)
            hexDoc.GetLineLength(line); // should not throw
    }

    [Fact(Timeout = 30_000)]
    public void GetLineText_LastRow_BinaryContent()
    {
        var bytes = new byte[256];
        for (var i = 0; i < 256; i++) bytes[i] = (byte)i;
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        // Last row is the worst case for byte-to-char misalignment
        var lastRow = hexDoc.LineCount;
        var text = hexDoc.GetLineText(lastRow);
        Assert.NotNull(text);
    }

    [Fact(Timeout = 30_000)]
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
            Assert.Equal(text.Length, length);
        }
    }

    [Fact(Timeout = 30_000)]
    public void GetLineText_EmptyDocument_ReturnsEmpty()
    {
        var doc = new Hex1bDocument([]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.Equal(1, hexDoc.LineCount);
        Assert.Equal("", hexDoc.GetLineText(1));
    }

    [Fact(Timeout = 30_000)]
    public void GetLineText_SingleByte()
    {
        var doc = new Hex1bDocument([0x42]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.Equal(1, hexDoc.LineCount);
        var text = hexDoc.GetLineText(1);
        Assert.NotEmpty(text);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(1, 1)]
    [InlineData(15, 1)]
    [InlineData(16, 1)]
    [InlineData(17, 2)]
    [InlineData(255, 16)]
    [InlineData(256, 16)]
    public void LineCount_MatchesCeilDivision(int byteCount, int expectedLines)
    {
        var bytes = new byte[byteCount];
        var doc = new Hex1bDocument(bytes);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.Equal(expectedLines, hexDoc.LineCount);
    }

    [Fact(Timeout = 30_000)]
    public void GetLineText_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument(new byte[32]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.Throws<ArgumentOutOfRangeException>(() => hexDoc.GetLineText(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => hexDoc.GetLineText(hexDoc.LineCount + 1));
    }

    [Fact(Timeout = 30_000)]
    public void GetLineLength_OutOfRange_Throws()
    {
        var doc = new Hex1bDocument(new byte[32]);
        var hexDoc = new HexRowDocument(doc) { BytesPerRow = 16 };

        Assert.Throws<ArgumentOutOfRangeException>(() => hexDoc.GetLineLength(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => hexDoc.GetLineLength(hexDoc.LineCount + 1));
    }
}
