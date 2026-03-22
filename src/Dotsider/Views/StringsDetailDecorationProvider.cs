using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Provides label coloring for the Strings detail popup editor.
/// Only highlights the first line ("Length: N") — string content lines are left unstyled.
/// </summary>
public sealed class StringsDetailDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration LabelDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(100, 130, 160)
    };

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        // Only color the "Length:" label on line 1
        if (startLine > 1)
            return [];

        var text = document.GetLineText(1);
        if (string.IsNullOrEmpty(text))
            return [];

        var colonIdx = text.IndexOf(':');
        if (colonIdx < 0)
            return [];

        return
        [
            new TextDecorationSpan(
                new DocumentPosition(1, 1),
                new DocumentPosition(1, colonIdx + 2),
                LabelDecoration,
                5)
        ];
    }
}
