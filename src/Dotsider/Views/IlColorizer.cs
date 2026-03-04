using Hex1b.Theming;
using System.Text;

namespace Dotsider.Views;

/// <summary>
/// Provides IL disassembly syntax coloring via inline ANSI escape codes.
/// Colors are deliberately muted to match the dotsider theme aesthetic.
/// </summary>
public static class IlColorizer
{
    // IL offset labels (IL_XXXX:) — dim structural color, matches hex view addresses
    private static readonly string AddressFg = Hex1bColor.FromRgb(100, 100, 130).ToForegroundAnsi();
    // Comments (// ...) — dim gray
    private static readonly string CommentFg = Hex1bColor.FromRgb(90, 90, 110).ToForegroundAnsi();
    // Opcodes — muted teal, slightly softer than the primary theme accent
    private static readonly string OpcodeFg = Hex1bColor.FromRgb(0, 170, 160).ToForegroundAnsi();
    // String operands ("...") — muted green
    private static readonly string StringFg = Hex1bColor.FromRgb(100, 180, 100).ToForegroundAnsi();
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

        return line;
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
