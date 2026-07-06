using Hex1b.Documents;

namespace Dotsider.Views;

/// <summary>
/// Provides search match highlighting for read-only editors.
/// Set <see cref="Query"/> before each frame to highlight matching text.
/// </summary>
public sealed class DiffSearchDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration MatchDecoration =
        HighlightHelper.CreateSearchMatchDecoration();

    /// <summary>The current search query, or null/empty when no search is active.</summary>
    public string? Query { get; set; }

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

                spans.Add(new TextDecorationSpan(
                    new DocumentPosition(line, idx + 1),
                    new DocumentPosition(line, idx + Query.Length + 1),
                    MatchDecoration,
                    10));

                pos = idx + Query.Length;
            }
        }

        return spans;
    }
}
