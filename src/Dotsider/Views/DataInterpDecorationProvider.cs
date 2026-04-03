using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides label coloring for the Data Interpretation panel's read-only editor.
/// Unlike <see cref="InfoLabelDecorationProvider"/> which only highlights the first
/// label per line, this provider highlights all label:value pairs on each line,
/// supporting the 4-column grid layout.
/// </summary>
public sealed class DataInterpDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration LabelDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(100, 130, 160)
    };

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            var text = document.GetLineText(line);
            if (string.IsNullOrEmpty(text))
                continue;

            var searchStart = 0;
            while (searchStart < text.Length)
            {
                var colonIdx = text.IndexOf(':', searchStart);
                if (colonIdx < 0)
                    break;

                // Walk backwards from the colon to find the label start.
                // Labels are preceded by whitespace and contain only letters/digits.
                var labelStart = colonIdx - 1;
                while (labelStart >= 0 && char.IsLetterOrDigit(text[labelStart]))
                    labelStart--;
                labelStart++; // move past the non-label character

                // Must have at least one letter/digit before the colon
                if (labelStart < colonIdx)
                {
                    // Column positions are 1-based in DocumentPosition
                    spans.Add(new TextDecorationSpan(
                        new DocumentPosition(line, labelStart + 1),
                        new DocumentPosition(line, colonIdx + 2),
                        LabelDecoration,
                        5));
                }

                searchStart = colonIdx + 1;
            }
        }

        return spans;
    }
}
