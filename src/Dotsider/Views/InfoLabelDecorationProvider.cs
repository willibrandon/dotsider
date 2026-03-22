using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides label coloring for read-only info editors.
/// Colors text from the start of each line up to and including the colon
/// with a dimmed label color, matching the original InfoLine styling.
/// </summary>
public sealed class InfoLabelDecorationProvider : ITextDecorationProvider
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

            // Only match label patterns: optional leading spaces, then letters/spaces, then colon.
            // The colon must appear within the first 25 characters to avoid false positives
            // on content lines that happen to contain colons.
            var colonIdx = text.IndexOf(':');
            if (colonIdx < 0 || colonIdx > 25)
                continue;

            // Verify everything before the colon is whitespace or letters (a label)
            var isLabel = true;
            for (var i = 0; i < colonIdx; i++)
            {
                if (!char.IsLetterOrDigit(text[i]) && text[i] is not ' ' and not '-')
                {
                    isLabel = false;
                    break;
                }
            }

            if (!isLabel)
                continue;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, 1),
                new DocumentPosition(line, colonIdx + 2),
                LabelDecoration,
                5));
        }

        return spans;
    }
}
