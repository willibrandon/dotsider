using Hex1b.Documents;

namespace Dotsider.Views;

/// <summary>
/// Provides IL syntax highlighting via the <see cref="ITextDecorationProvider"/> API.
/// Parses each visible line the same way <see cref="IlColorizer.ColorizeInstruction"/> does,
/// but returns <see cref="TextDecorationSpan"/> objects instead of ANSI strings.
/// </summary>
public sealed class IlSyntaxDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration AddressDecoration = new() { Foreground = IlColorizer.AddressColor };
    private static readonly TextDecoration CommentDecoration = new() { Foreground = IlColorizer.CommentColor };
    private static readonly TextDecoration OpcodeDecoration = new() { Foreground = IlColorizer.OpcodeColor };
    private static readonly TextDecoration StringDecoration = new() { Foreground = IlColorizer.StringColor };

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            var text = document.GetLineText(line);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var trimmed = text.AsSpan().TrimStart();
            if (trimmed.StartsWith("//"))
            {
                // Comment: full line
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, 1),
                    new DocumentPosition(line, text.Length + 1),
                    CommentDecoration));
                continue;
            }

            if (text.StartsWith("IL_"))
            {
                ParseInstruction(spans, text, line);
            }
        }

        return spans;
    }

    private static void ParseInstruction(List<TextDecorationSpan> spans, string line, int lineNum)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex < 0)
            return;

        // Address: "IL_XXXX:" (columns 1 through separatorIndex+1 inclusive)
        spans.Add(new TextDecorationSpan(
            new DocumentPosition(lineNum, 1),
            new DocumentPosition(lineNum, separatorIndex + 2), // +2: separator ':' is at separatorIndex, end is exclusive
            AddressDecoration));

        var bodyStart = separatorIndex + 2; // after ": "
        if (bodyStart >= line.Length)
            return;

        var body = line[bodyStart..];

        var opcodeSeparatorIndex = body.IndexOf(' ');
        if (opcodeSeparatorIndex < 0)
        {
            // Opcode only, no operand
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(lineNum, bodyStart + 1), // 1-based
                new DocumentPosition(lineNum, bodyStart + body.Length + 1),
                OpcodeDecoration));
            return;
        }

        // Opcode
        spans.Add(new TextDecorationSpan(
            new DocumentPosition(lineNum, bodyStart + 1),
            new DocumentPosition(lineNum, bodyStart + opcodeSeparatorIndex + 1),
            OpcodeDecoration));

        // Operand: look for quoted string segments
        var operandStart = bodyStart + opcodeSeparatorIndex;
        ParseQuotedStrings(spans, line, lineNum, operandStart);
    }

    private static void ParseQuotedStrings(
        List<TextDecorationSpan> spans, string line, int lineNum, int startCol)
    {
        var inQuote = false;
        var isEscaped = false;
        var quoteStart = 0;

        for (var i = startCol; i < line.Length; i++)
        {
            var ch = line[i];

            if (!inQuote)
            {
                if (ch == '"')
                {
                    quoteStart = i;
                    inQuote = true;
                    isEscaped = false;
                }
                continue;
            }

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
                // End of quoted string — span from quoteStart to i (inclusive)
                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(lineNum, quoteStart + 1), // 1-based
                    new DocumentPosition(lineNum, i + 2),          // exclusive end
                    StringDecoration));
                inQuote = false;
            }
        }

        // Unterminated quote
        if (inQuote)
        {
            spans.Add(new TextDecorationSpan(
                new DocumentPosition(lineNum, quoteStart + 1),
                new DocumentPosition(lineNum, line.Length + 1),
                StringDecoration));
        }
    }
}
