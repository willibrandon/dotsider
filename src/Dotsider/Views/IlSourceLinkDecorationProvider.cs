using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Underlines Source Link markers in IL source comments to indicate that the
/// resolved URL can be copied.
/// </summary>
public sealed class IlSourceLinkDecorationProvider : ITextDecorationProvider
{
    /// <summary>The compact marker shown on source-span comment lines.</summary>
    public const string SourceLinkMarker = "[source link]";

    private static readonly TextDecoration SourceLinkDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(100, 160, 220),
        UnderlineStyle = UnderlineStyle.Single
    };

    /// <summary>The instruction list for the current method.</summary>
    public IReadOnlyList<IlInstruction>? Instructions { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        if (Instructions is null || Instructions.Count == 0)
            return [];

        var spans = new List<TextDecorationSpan>();
        foreach (var instruction in Instructions)
        {
            if (instruction.DisplayLine is not { } instructionLine
                || instruction.SequenceStartLine is null
                || string.IsNullOrWhiteSpace(instruction.SourceLinkUrl))
                continue;

            var markerLine = instructionLine - 1;
            if (markerLine < startLine || markerLine > endLine || markerLine < 1
                || markerLine > document.LineCount)
                continue;

            var lineText = document.GetLineText(markerLine);
            var markerStart = lineText.IndexOf(SourceLinkMarker, StringComparison.Ordinal);
            if (markerStart < 0)
                continue;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(markerLine, markerStart + 1),
                new DocumentPosition(markerLine, markerStart + SourceLinkMarker.Length + 1),
                SourceLinkDecoration,
                Priority: 12));
        }

        return spans;
    }
}
