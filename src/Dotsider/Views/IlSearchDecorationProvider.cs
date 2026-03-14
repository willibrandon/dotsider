using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides search match highlighting for the IL disassembly editor.
/// Mutable properties are updated before each frame by <see cref="IlInspectorView"/>.
/// </summary>
public sealed class IlSearchDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration MatchDecoration = new()
    {
        Background = HighlightHelper.MatchBgColor
        // Foreground is null — syntax colors show through
    };

    private static readonly TextDecoration CurrentMatchDecoration = new()
    {
        Background = Hex1bColor.FromRgb(255, 165, 0),
        Foreground = Hex1bColor.Black
    };

    /// <summary>The current search query, or null/empty when no search is active.</summary>
    public string? Query { get; set; }

    /// <summary>The start position of the current match for distinct "active" styling, or null.</summary>
    public DocumentPosition? CurrentMatchStart { get; set; }

    /// <summary>The length of the current match text (used with <see cref="CurrentMatchStart"/>).</summary>
    public int CurrentMatchLength { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        if (string.IsNullOrEmpty(Query))
            return [];

        var spans = new List<TextDecorationSpan>();

        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            var text = document.GetLineText(line);
            if (string.IsNullOrEmpty(text))
                continue;

            var pos = 0;
            while (pos < text.Length)
            {
                var idx = text.IndexOf(Query, pos, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                var matchStart = new DocumentPosition(line, idx + 1); // 1-based
                var matchEnd = new DocumentPosition(line, idx + Query.Length + 1); // exclusive

                var isCurrent = CurrentMatchStart is { } cms
                    && cms.Line == line
                    && cms.Column == idx + 1;

                spans.Add(new TextDecorationSpan(
                    matchStart,
                    matchEnd,
                    isCurrent ? CurrentMatchDecoration : MatchDecoration,
                    isCurrent ? 20 : 10));

                pos = idx + Query.Length;
            }
        }

        return spans;
    }
}
