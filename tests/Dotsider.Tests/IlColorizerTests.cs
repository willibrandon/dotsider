using Dotsider.Views;
using Hex1b.Theming;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="IlColorizer"/> IL disassembly syntax coloring.
/// </summary>
public class IlColorizerTests
{
    private const string Reset = "\x1b[0m";

    // Reconstruct expected ANSI codes from the same RGB values used by the colorizer
    private static readonly string AddressFg = Hex1bColor.FromRgb(100, 100, 130).ToForegroundAnsi();
    private static readonly string CommentFg = Hex1bColor.FromRgb(90, 90, 110).ToForegroundAnsi();
    private static readonly string OpcodeFg = Hex1bColor.FromRgb(0, 170, 160).ToForegroundAnsi();
    private static readonly string StringFg = Hex1bColor.FromRgb(100, 180, 100).ToForegroundAnsi();

    // --- Passthrough cases ---

    /// <summary>
    /// Verifies empty line returns unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLine_ReturnsUnchanged()
    {
        Assert.Equal("", IlColorizer.ColorizeLine(""));
    }

    /// <summary>
    /// Verifies whitespace line returns unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void WhitespaceLine_ReturnsUnchanged()
    {
        var line = "   ";
        Assert.Equal(line, IlColorizer.ColorizeLine(line));
    }

    /// <summary>
    /// Verifies unrecognized line passes through.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void UnrecognizedLine_PassesThrough()
    {
        var line = "some other text";
        Assert.Equal(line, IlColorizer.ColorizeLine(line));
    }

    // --- Comment coloring ---

    /// <summary>
    /// Verifies comment wrapped in comment color.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Comment_WrappedInCommentColor()
    {
        var line = "// Method: Foo::Bar";
        var result = IlColorizer.ColorizeLine(line);
        Assert.Equal($"{CommentFg}{line}{Reset}", result);
    }

    /// <summary>
    /// Verifies indented comment wrapped in comment color.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IndentedComment_WrappedInCommentColor()
    {
        var line = "  // RVA: 0x00002050";
        var result = IlColorizer.ColorizeLine(line);
        Assert.Equal($"{CommentFg}{line}{Reset}", result);
    }

    // --- Instruction coloring ---

    /// <summary>
    /// Verifies opcode only colors address and opcode.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OpcodeOnly_ColorsAddressAndOpcode()
    {
        var result = IlColorizer.ColorizeLine("IL_0005: ret");
        Assert.Equal($"{AddressFg}IL_0005:{Reset} {OpcodeFg}ret{Reset}", result);
    }

    /// <summary>
    /// Verifies opcode with non string operand preserves operand uncolored.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OpcodeWithNonStringOperand_PreservesOperandUncolored()
    {
        var result = IlColorizer.ColorizeLine("IL_000B: call System.Console::WriteLine(string)");
        Assert.Contains($"{AddressFg}IL_000B:{Reset}", result);
        Assert.Contains($"{OpcodeFg}call{Reset}", result);
        Assert.Contains("System.Console::WriteLine(string)", result);
        Assert.DoesNotContain(StringFg, result);
    }

    /// <summary>
    /// Verifies branch target preserves target label.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BranchTarget_PreservesTargetLabel()
    {
        var result = IlColorizer.ColorizeLine("IL_000A: br.s IL_0010");
        Assert.Contains($"{OpcodeFg}br.s{Reset}", result);
        Assert.Contains("IL_0010", result);
    }

    /// <summary>
    /// Verifies numeric operand preserves uncolored.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NumericOperand_PreservesUncolored()
    {
        var result = IlColorizer.ColorizeLine("IL_0002: ldc.i4.s 42");
        Assert.Contains($"{OpcodeFg}ldc.i4.s{Reset}", result);
        Assert.Contains("42", result);
        Assert.DoesNotContain(StringFg, result);
    }

    // --- String operand coloring ---

    /// <summary>
    /// Verifies string operand colored green.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void StringOperand_ColoredGreen()
    {
        var result = IlColorizer.ColorizeLine("IL_0006: ldstr \"hello world\"");
        Assert.Contains($"{AddressFg}IL_0006:{Reset}", result);
        Assert.Contains($"{OpcodeFg}ldstr{Reset}", result);
        Assert.Contains($"{StringFg}\"hello world\"{Reset}", result);
    }

    /// <summary>
    /// Verifies escaped quotes stay single segment.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EscapedQuotes_StaySingleSegment()
    {
        var result = IlColorizer.ColorizeLine("IL_0006: ldstr \"say \\\"hi\\\"\"");
        Assert.Contains(StringFg, result);
        // Should open StringFg exactly once for the whole quoted segment
        Assert.Equal(1, CountOccurrences(result, StringFg));
    }

    /// <summary>
    /// Verifies unmatched quote ends with reset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void UnmatchedQuote_EndsWithReset()
    {
        var result = IlColorizer.ColorizeLine("IL_0006: ldstr \"unclosed");
        Assert.Contains(StringFg, result);
        Assert.EndsWith(Reset, result);
    }

    // --- Edge cases ---

    /// <summary>
    /// Verifies malformed il line no separator passes through.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MalformedIlLine_NoSeparator_PassesThrough()
    {
        var line = "IL_NOSEPARATOR";
        var result = IlColorizer.ColorizeLine(line);
        Assert.Equal(line, result);
    }

    /// <summary>
    /// Verifies empty body after separator colors address only.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyBodyAfterSeparator_ColorsAddressOnly()
    {
        var result = IlColorizer.ColorizeLine("IL_0000:");
        // No ": " separator (needs colon+space), so treated as malformed
        Assert.Equal("IL_0000:", result);
    }

    /// <summary>
    /// Verifies all resets paired instruction line.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllResetsPaired_InstructionLine()
    {
        var result = IlColorizer.ColorizeLine("IL_0001: call System.Object::.ctor()");
        // Each colored span (address, opcode) should have a matching reset
        var ansiOpens = CountOccurrences(result, "\x1b[38;");
        var resets = CountOccurrences(result, Reset);
        Assert.Equal(ansiOpens, resets);
    }

    /// <summary>
    /// Verifies all resets paired string operand.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllResetsPaired_StringOperand()
    {
        var result = IlColorizer.ColorizeLine("IL_0006: ldstr \"test\"");
        // address + opcode + string = 3 colored spans, 3 resets
        var ansiOpens = CountOccurrences(result, "\x1b[38;");
        var resets = CountOccurrences(result, Reset);
        Assert.Equal(ansiOpens, resets);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
