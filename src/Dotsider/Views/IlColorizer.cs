using Hex1b.Theming;
using System.Text;

namespace Dotsider.Views;

/// <summary>
/// Provides IL disassembly syntax coloring via inline ANSI escape codes.
/// Colors are deliberately muted to match the dotsider theme aesthetic.
/// </summary>
public static class IlColorizer
{
    /// <summary>IL offset labels (IL_XXXX:) — dim structural color, matches hex view addresses.</summary>
    public static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);

    /// <summary>Comments (// ...) — dim gray.</summary>
    public static readonly Hex1bColor CommentColor = Hex1bColor.FromRgb(90, 90, 110);

    /// <summary>Opcodes — muted teal, slightly softer than the primary theme accent.</summary>
    public static readonly Hex1bColor OpcodeColor = Hex1bColor.FromRgb(0, 170, 160);

    /// <summary>IL directives such as .locals init — muted blue-gray.</summary>
    public static readonly Hex1bColor DirectiveColor = Hex1bColor.FromRgb(125, 130, 170);

    /// <summary>String operands ("...") — muted green.</summary>
    public static readonly Hex1bColor StringColor = Hex1bColor.FromRgb(100, 180, 100);

    private static readonly string AddressFg = AddressColor.ToForegroundAnsi();
    private static readonly string CommentFg = CommentColor.ToForegroundAnsi();
    private static readonly string OpcodeFg = OpcodeColor.ToForegroundAnsi();
    private static readonly string DirectiveFg = DirectiveColor.ToForegroundAnsi();
    private static readonly string StringFg = StringColor.ToForegroundAnsi();
    // Reset to default terminal color
    private const string Reset = "\x1b[0m";

    /// <summary>
    /// Colorizes a single line of IL disassembly output.
    /// Handles comments, empty lines, and delegates instruction coloring.
    /// </summary>
    public static string ColorizeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        if (line.TrimStart().StartsWith("//"))
            return $"{CommentFg}{line}{Reset}";

        if (line.StartsWith("IL_"))
            return ColorizeInstruction(line);

        if (TryGetLocalsInitSpan(line, out var startIndex, out var length))
            return line[..startIndex] + DirectiveFg + line.Substring(startIndex, length) + Reset + line[(startIndex + length)..];

        return line;
    }

    internal static bool TryGetLocalsInitSpan(string line, out int startIndex, out int length)
    {
        startIndex = 0;
        length = 0;

        const string directive = ".locals init";
        while (startIndex < line.Length && char.IsWhiteSpace(line[startIndex]))
            startIndex++;

        if (!line.AsSpan(startIndex).StartsWith(directive, StringComparison.Ordinal))
            return false;

        var endIndex = startIndex + directive.Length;
        if (endIndex < line.Length && !char.IsWhiteSpace(line[endIndex]) && line[endIndex] != '(')
            return false;

        length = directive.Length;
        return true;
    }

    // Parse "IL_XXXX: opcode operand", color the address and opcode,
    // and highlight quoted string segments within the operand.
    private static string ColorizeInstruction(string line)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex < 0)
            return line;

        var address = line[..(separatorIndex + 1)];
        var body = line[(separatorIndex + 2)..];

        if (body.Length == 0)
            return $"{AddressFg}{address}{Reset}";

        var opcodeSeparatorIndex = body.IndexOf(' ');
        if (opcodeSeparatorIndex < 0)
            return $"{AddressFg}{address}{Reset} {OpcodeFg}{body}{Reset}";

        var opcode = body[..opcodeSeparatorIndex];
        var operandPart = body[opcodeSeparatorIndex..];

        return $"{AddressFg}{address}{Reset} {OpcodeFg}{opcode}{Reset}{ColorizeQuotedSegments(operandPart)}";
    }

    private static string ColorizeQuotedSegments(string text)
    {
        if (text.IndexOf('"') < 0)
            return text;

        var sb = new StringBuilder(text.Length + 32);
        var inQuote = false;
        var isEscaped = false;

        foreach (var ch in text)
        {
            if (!inQuote)
            {
                if (ch == '"')
                {
                    sb.Append(StringFg);
                    sb.Append(ch);
                    inQuote = true;
                    isEscaped = false;
                    continue;
                }

                sb.Append(ch);
                continue;
            }

            sb.Append(ch);

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (ch == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (ch == '"')
            {
                sb.Append(Reset);
                inQuote = false;
            }
        }

        // Keep terminal state sane if an unmatched quote appears.
        if (inQuote)
            sb.Append(Reset);

        return sb.ToString();
    }
}
