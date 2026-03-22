using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides color coding for change statistics in the diff summary editor.
/// Colors +N values green, -N values red, and ~N values yellow.
/// </summary>
public sealed class DiffStatsDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration GreenDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(80, 200, 120)
    };

    private static readonly TextDecoration RedDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(200, 80, 80)
    };

    private static readonly TextDecoration YellowDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(200, 200, 80)
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

            // Only color lines that contain all three markers (+N, -N, ~N)
            // to avoid false positives on "Size delta: +10.0 KB"
            if (!text.Contains('~'))
                continue;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch is not ('+' or '-' or '~')) continue;
                if (i + 1 >= text.Length || !char.IsDigit(text[i + 1])) continue;

                var start = i;
                i++;
                while (i < text.Length && char.IsDigit(text[i]))
                    i++;

                var decoration = ch switch
                {
                    '+' => GreenDecoration,
                    '-' => RedDecoration,
                    '~' => YellowDecoration,
                    _ => null
                };

                if (decoration is not null)
                {
                    spans.Add(new TextDecorationSpan(
                        new DocumentPosition(line, start + 1),
                        new DocumentPosition(line, i + 1),
                        decoration,
                        5));
                }

                i--;
            }
        }

        return spans;
    }
}
