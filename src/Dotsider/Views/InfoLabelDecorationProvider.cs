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

                var labelStart = FindLabelStart(text, searchStart, colonIdx);

                if (IsLabel(text.AsSpan(labelStart, colonIdx - labelStart)))
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

                searchStart = FindNextLabel(text, colonIdx);
            }
        }

        return spans;
    }

    private static int FindLabelStart(string text, int searchStart, int colonIdx)
    {
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

        return labelStart;
    }

    private static bool IsLabel(ReadOnlySpan<char> label)
    {
        if (label.Length is 0 or > 25) return false;
        foreach (var character in label)
            if (char.IsLetter(character)) return true;
        return false;
    }

    private static int FindNextLabel(string text, int colonIdx)
    {
        // Skip colon padding before looking for the four-space gap after a value.
        var next = colonIdx + 1;
        while (next < text.Length && text[next] == ' ') next++;
        next = text.IndexOf("    ", next, StringComparison.Ordinal);
        if (next < 0) return text.Length;
        while (next < text.Length && text[next] == ' ') next++;
        return next;
    }

}
