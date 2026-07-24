using Dotsider.Infrastructure;

namespace Dotsider.Tests;

/// <summary>
/// Tests terminal-safe presentation of untrusted text.
/// </summary>
[TestClass]
public sealed class TerminalTextTests
{
    /// <summary>
    /// Verifies printable text, including supplementary Unicode, takes the identity fast path.
    /// </summary>
    [TestMethod]
    public void Escape_PrintableText_ReturnsOriginalInstance()
    {
        var value = "ordinary Καλημέρα \U0001F600";

        var result = TerminalText.Escape(value);

        Assert.AreSame(value, result);
    }

    /// <summary>
    /// Verifies every C0 control is replaced with its visible control-picture character.
    /// </summary>
    [TestMethod]
    public void Escape_AllC0Controls_UsesControlPictures()
    {
        for (var value = 0; value <= 0x1F; value++)
        {
            var result = TerminalText.Escape(((char)value).ToString());

            Assert.AreEqual(((char)(0x2400 + value)).ToString(), result);
        }
    }

    /// <summary>
    /// Verifies DEL and every C1 control are rendered visibly.
    /// </summary>
    [TestMethod]
    public void Escape_DelAndAllC1Controls_UsesVisibleEscapes()
    {
        Assert.AreEqual("\u2421", TerminalText.Escape("\u007F"));

        for (var value = 0x80; value <= 0x9F; value++)
        {
            var result = TerminalText.Escape(((char)value).ToString());

            Assert.AreEqual($"\\u{value:X4}", result);
        }
    }

    /// <summary>
    /// Verifies terminal sequences, bidirectional controls, separators, and malformed UTF-16
    /// cannot survive the display projection.
    /// </summary>
    [TestMethod]
    public void Escape_TerminalAndUnicodeFormattingPayloads_RendersVisibleText()
    {
        var value = "\u001B]52;c;cHduZWQ=\u0007-\u001B[31mX\u001B[0m"
            + "\u202E\u2066\u2028\u2029\uD800";

        var result = TerminalText.Escape(value);

        Assert.AreEqual(
            "␛]52;c;cHduZWQ=␇-␛[31mX␛[0m"
            + "\\u202E\\u2066\\u2028\\u2029\\uD800",
            result);
    }

    /// <summary>
    /// Verifies multiline text normalizes logical line endings while escaping every other control.
    /// </summary>
    [TestMethod]
    public void EscapeMultiline_MixedLineEndings_PreservesOnlyLogicalLines()
    {
        var result = TerminalText.EscapeMultiline("a\r\nb\rc\nd\t\u0085\u2028");

        Assert.AreEqual("a\nb\nc\nd␉\\u0085\\u2028", result);
    }

    /// <summary>
    /// Verifies truncation never leaves an unmatched surrogate at the preview boundary.
    /// </summary>
    [TestMethod]
    public void TruncateWithEllipsis_SupplementaryRuneAtBoundary_PreservesValidUtf16()
    {
        var value = new string('a', 36) + "\U0001F600tail";

        var result = TerminalText.TruncateWithEllipsis(value, 40);

        Assert.AreEqual(new string('a', 36) + "...", result);
    }
}
