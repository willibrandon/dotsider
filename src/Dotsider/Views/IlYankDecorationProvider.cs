using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides a brief flash highlight over the yanked text range in the IL editor,
/// matching neovim's <c>IncSearch</c> yank feedback behavior.
/// </summary>
public sealed class IlYankDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration YankDecoration = new()
    {
        Background = Hex1bColor.FromRgb(126, 201, 216), // IncSearch bg (#7ec9d8)
        Foreground = Hex1bColor.FromRgb(24, 24, 37)     // IncSearch fg (#181825)
    };

    /// <summary>The range to highlight, or null when no yank flash is active.</summary>
    public (DocumentPosition Start, DocumentPosition End)? HighlightRange { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        if (HighlightRange is not { } range)
            return [];

        var (start, end) = range;

        // Only emit if the highlight overlaps the visible viewport
        if (end.Line < startLine || start.Line > endLine)
            return [];

        return [new TextDecorationSpan(start, end, YankDecoration, Priority: 30)];
    }
}
