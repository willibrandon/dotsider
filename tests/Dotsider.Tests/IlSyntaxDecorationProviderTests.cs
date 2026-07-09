using Dotsider.Views;
using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="IlSyntaxDecorationProvider"/> IL syntax highlighting via decoration spans.
/// </summary>
[TestClass]
public class IlSyntaxDecorationProviderTests
{
    private readonly IlSyntaxDecorationProvider _provider = new();

    /// <summary>
    /// Verifies comment line returns comment span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CommentLine_ReturnsCommentSpan()
    {
        var line = "// Method: Foo::Bar";
        var doc = new Hex1bDocument(line);

        var spans = _provider.GetDecorations(1, 1, doc);

        var span = Assert.ContainsSingle(spans);
        Assert.AreEqual(IlColorizer.CommentColor, span.Decoration.Foreground);
        Assert.AreEqual(1, span.Start.Line);
        Assert.AreEqual(1, span.Start.Column);
        Assert.AreEqual(1, span.End.Line);
        Assert.AreEqual(line.Length + 1, span.End.Column);
    }

    /// <summary>
    /// Verifies instruction line returns address and opcode spans.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InstructionLine_ReturnsAddressAndOpcodeSpans()
    {
        var doc = new Hex1bDocument("IL_0000: nop");

        var spans = _provider.GetDecorations(1, 1, doc);

        Assert.HasCount(2, spans);

        // Address span: "IL_0000:" (columns 1..9, end exclusive at 9)
        var address = spans[0];
        Assert.AreEqual(IlColorizer.AddressColor, address.Decoration.Foreground);
        Assert.AreEqual(1, address.Start.Column);
        // separatorIndex for "IL_0000: nop" is 7, so end column = 7 + 2 = 9
        Assert.AreEqual(9, address.End.Column);

        // Opcode span: "nop" (starts at column 10, length 3, end exclusive at 13)
        var opcode = spans[1];
        Assert.AreEqual(IlColorizer.OpcodeColor, opcode.Decoration.Foreground);
        Assert.AreEqual(10, opcode.Start.Column);
        Assert.AreEqual(13, opcode.End.Column);
    }

    /// <summary>
    /// Verifies instruction with operand includes opcode only.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InstructionWithOperand_IncludesOpcodeOnly()
    {
        var line = "IL_0007: callvirt System.String::IsNullOrEmpty";
        var doc = new Hex1bDocument(line);

        var spans = _provider.GetDecorations(1, 1, doc);

        // Address + opcode only; no string operand, so no string span
        Assert.HasCount(2, spans);

        var address = spans[0];
        Assert.AreEqual(IlColorizer.AddressColor, address.Decoration.Foreground);

        var opcode = spans[1];
        Assert.AreEqual(IlColorizer.OpcodeColor, opcode.Decoration.Foreground);

        // "callvirt" starts at column 10 (after "IL_0007: ")
        Assert.AreEqual(10, opcode.Start.Column);
        // "callvirt" is 8 chars, so end column = 10 + 8 = 18
        Assert.AreEqual(18, opcode.End.Column);

        // No string-colored span
        Assert.DoesNotContain(s => Equals(s.Decoration.Foreground, IlColorizer.StringColor), spans);
    }

    /// <summary>
    /// Verifies string operand returns string span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void StringOperand_ReturnsStringSpan()
    {
        var line = "IL_000A: ldstr \"hello world\"";
        var doc = new Hex1bDocument(line);

        var spans = _provider.GetDecorations(1, 1, doc);

        // Address + opcode + string = 3 spans
        Assert.HasCount(3, spans);

        var address = spans[0];
        Assert.AreEqual(IlColorizer.AddressColor, address.Decoration.Foreground);

        var opcode = spans[1];
        Assert.AreEqual(IlColorizer.OpcodeColor, opcode.Decoration.Foreground);

        var str = spans[2];
        Assert.AreEqual(IlColorizer.StringColor, str.Decoration.Foreground);

        // "hello world" with quotes starts at index 15 in the line → column 16
        var quoteStart = line.IndexOf('"');
        var quoteEnd = line.LastIndexOf('"');
        Assert.AreEqual(quoteStart + 1, str.Start.Column); // 1-based
        Assert.AreEqual(quoteEnd + 2, str.End.Column);      // exclusive end
    }

    /// <summary>
    /// Verifies locals init line returns directive span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LocalsInitLine_ReturnsDirectiveSpan()
    {
        var line = "    .locals init (";
        var doc = new Hex1bDocument(line);

        var spans = _provider.GetDecorations(1, 1, doc);

        var span = Assert.ContainsSingle(spans);
        Assert.AreEqual(IlColorizer.DirectiveColor, span.Decoration.Foreground);
        Assert.AreEqual(5, span.Start.Column);
        Assert.AreEqual(17, span.End.Column);
    }

    /// <summary>
    /// Verifies blank line no spans.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BlankLine_NoSpans()
    {
        var doc = new Hex1bDocument("   ");

        var spans = _provider.GetDecorations(1, 1, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies viewport range respects start and end.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ViewportRange_RespectsStartAndEnd()
    {
        var doc = new Hex1bDocument("// line1\nIL_0000: nop\n// line3\nIL_0001: ret");

        // Only request lines 2-3 (IL_0000: nop and // line3)
        var spans = _provider.GetDecorations(2, 3, doc);

        // Line 2 produces address + opcode = 2 spans
        // Line 3 produces comment = 1 span
        Assert.HasCount(3, spans);

        // All spans should be on lines 2 or 3
        TestAssert.All(spans, s => Assert.IsInRange(2, 3, s.Start.Line));

        // Line 1 and line 4 should not appear
        Assert.DoesNotContain(s => s.Start.Line == 1, spans);
        Assert.DoesNotContain(s => s.Start.Line == 4, spans);
    }

    /// <summary>
    /// Verifies non il line comment metadata returns comment span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NonIlLine_CommentMetadata_ReturnsCommentSpan()
    {
        var line = "// Max stack: 5";
        var doc = new Hex1bDocument(line);

        var spans = _provider.GetDecorations(1, 1, doc);

        var span = Assert.ContainsSingle(spans);
        Assert.AreEqual(IlColorizer.CommentColor, span.Decoration.Foreground);
        Assert.AreEqual(1, span.Start.Column);
        Assert.AreEqual(line.Length + 1, span.End.Column);
    }
}
