using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for hex dump search: ASCII byte search, hex pattern parsing,
/// multiple match offsets, and edge cases.
/// </summary>
public class HexSearchTests
{
    /// <summary>
    /// Verifies parse hex pattern valid bytes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ParseHexPattern_ValidBytes()
    {
        var (bytes, error) = HexDumpView.ParseHexPattern("FF D8 FF E0");
        Assert.Null(error);
        Assert.NotNull(bytes);
        Assert.Equal(4, bytes!.Length);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        Assert.Equal(0xFF, bytes[2]);
        Assert.Equal(0xE0, bytes[3]);
    }

    /// <summary>
    /// Verifies parse hex pattern no spaces.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ParseHexPattern_NoSpaces()
    {
        var (bytes, error) = HexDumpView.ParseHexPattern("FFD8FFE0");
        Assert.Null(error);
        Assert.NotNull(bytes);
        Assert.Equal(4, bytes!.Length);
    }

    /// <summary>
    /// Verifies parse hex pattern odd digits error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ParseHexPattern_OddDigits_Error()
    {
        var (bytes, error) = HexDumpView.ParseHexPattern("FD8");
        Assert.Null(bytes);
        Assert.Equal("Invalid hex: odd number of digits", error);
    }

    /// <summary>
    /// Verifies parse hex pattern invalid chars error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ParseHexPattern_InvalidChars_Error()
    {
        var (bytes, error) = HexDumpView.ParseHexPattern("GGZZ");
        Assert.Null(bytes);
        Assert.Equal("Invalid hex pattern", error);
    }

    /// <summary>
    /// Verifies parse hex pattern empty error.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ParseHexPattern_Empty_Error()
    {
        var (bytes, error) = HexDumpView.ParseHexPattern("");
        Assert.Null(bytes);
        Assert.Equal("Invalid hex: empty pattern", error);
    }

    /// <summary>
    /// Verifies find byte pattern single match.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_SingleMatch()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];
        byte[] pattern = [0x03, 0x04];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Single(offsets);
        Assert.Equal(2, offsets[0]);
    }

    /// <summary>
    /// Verifies find byte pattern multiple matches.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_MultipleMatches()
    {
        byte[] data = [0xAA, 0xBB, 0xAA, 0xBB, 0xAA, 0xBB];
        byte[] pattern = [0xAA, 0xBB];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Equal(3, offsets.Count);
        Assert.Equal(0, offsets[0]);
        Assert.Equal(2, offsets[1]);
        Assert.Equal(4, offsets[2]);
    }

    /// <summary>
    /// Verifies find byte pattern no match.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_NoMatch()
    {
        byte[] data = [0x01, 0x02, 0x03];
        byte[] pattern = [0xFF, 0xFE];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Empty(offsets);
    }

    /// <summary>
    /// Verifies find byte pattern empty data.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_EmptyData()
    {
        byte[] data = [];
        byte[] pattern = [0xFF];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Empty(offsets);
    }

    /// <summary>
    /// Verifies find byte pattern empty pattern.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_EmptyPattern()
    {
        byte[] data = [0x01, 0x02];
        byte[] pattern = [];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Empty(offsets);
    }

    /// <summary>
    /// Verifies find byte pattern pattern longer than data.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_PatternLongerThanData()
    {
        byte[] data = [0x01];
        byte[] pattern = [0x01, 0x02, 0x03];
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Empty(offsets);
    }

    /// <summary>
    /// Verifies find byte pattern ascii text search.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FindBytePattern_AsciiTextSearch()
    {
        byte[] data = System.Text.Encoding.ASCII.GetBytes("Hello World Hello");
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes("Hello");
        var offsets = HexDumpView.FindBytePattern(data, pattern);
        Assert.Equal(2, offsets.Count);
        Assert.Equal(0, offsets[0]);
        Assert.Equal(12, offsets[1]);
    }
}
