using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides label coloring for read-only info editors.
/// Colors all label:value patterns on each line with a dimmed label color.
/// Labels are identified by walking backwards from each colon to find
/// letters/digits, stopping at double-space boundaries between values.
/// </summary>
public sealed class InfoLabelDecorationProvider : ITextDecorationProvider
{
    private static readonly Hex1bColor DefaultLabelColor = Hex1bColor.FromRgb(100, 130, 160);
    private readonly TextDecoration _labelDecoration;

    /// <summary>
    /// Creates the default info label provider.
    /// </summary>
    public InfoLabelDecorationProvider()
        : this(DefaultLabelColor)
    {
    }

    /// <summary>
    /// Creates an info label provider with a caller-supplied label color.
    /// </summary>
    /// <param name="labelColor">The foreground color applied to label spans.</param>
    public InfoLabelDecorationProvider(Hex1bColor labelColor)
    {
        _labelDecoration = new TextDecoration { Foreground = labelColor };
    }

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
            var isFirstOnLine = true;

            while (searchStart < text.Length)
            {
                var colonIdx = text.IndexOf(':', searchStart);
                if (colonIdx < 0)
                    break;

                // Walk backwards from the colon to find the label start.
                var labelStart = colonIdx - 1;
                while (labelStart >= searchStart
                    && (char.IsLetterOrDigit(text[labelStart]) || text[labelStart] is ' ' or '-'))
                {
                    // Stop at double-space (gap between previous value and this label)
                    if (text[labelStart] == ' ' && labelStart > 0 && text[labelStart - 1] == ' ')
                    {
                        labelStart++;
                        break;
                    }
                    labelStart--;
                }
                if (labelStart < 0) labelStart = 0;

                // Skip leading spaces within the label
                while (labelStart < colonIdx && text[labelStart] == ' ')
                    labelStart++;

                // Validate: at least one letter before the colon (avoids matching
                // version numbers like "1.0.0.0") and the colon must be close to
                // the label start to avoid false positives on content colons.
                var hasLetter = false;
                for (var i = labelStart; i < colonIdx; i++)
                {
                    if (char.IsLetter(text[i]))
                    {
                        hasLetter = true;
                        break;
                    }
                }

                if (hasLetter && labelStart < colonIdx && (colonIdx - labelStart) <= 25)
                {
                    // First label on the line includes leading whitespace (column 1)
                    var spanCol = isFirstOnLine ? 1 : labelStart + 1;
                    spans.Add(new TextDecorationSpan(
                        new DocumentPosition(line, spanCol),
                        new DocumentPosition(line, colonIdx + 2),
                        _labelDecoration,
                        5));
                    isFirstOnLine = false;
                }

                // Skip past the value to the next multi-label separator.
                // First skip the padding between the label's colon and its value,
                // then look for 4+ consecutive spaces which indicate a real
                // value→label boundary ("Gen 0: 4    Gen 1: 4").
                var next = colonIdx + 1;
                // Skip immediate padding after colon
                while (next < text.Length && text[next] == ' ')
                    next++;
                // Skip through value content until a 4+ space gap.
                // If no gap is found, stop scanning this line entirely.
                var foundSeparator = false;
                while (next < text.Length - 3)
                {
                    if (text[next] == ' ' && text[next + 1] == ' '
                        && text[next + 2] == ' ' && text[next + 3] == ' ')
                    {
                        while (next < text.Length && text[next] == ' ')
                            next++;
                        foundSeparator = true;
                        break;
                    }
                    next++;
                }
                searchStart = foundSeparator ? next : text.Length;
            }
        }

        return spans;
    }
}
