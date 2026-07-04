using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;

namespace Dotsider.Views;

/// <summary>
/// Underlines the resolved call/branch/data target of a native instruction to signal that
/// go-to-definition (Enter) will navigate there. Driven by the decoded instruction list and its
/// <see cref="NativeLineLayout.TargetStart"/> span — never by re-parsing the rendered text. Inert
/// until <see cref="Instructions"/> is set.
/// </summary>
public sealed class NativeNavigationDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration Underline = new() { UnderlineStyle = UnderlineStyle.Single };

    /// <summary>The decoded instructions of the current listing.</summary>
    public IReadOnlyList<NativeInstruction>? Instructions { get; set; }

    private Dictionary<int, NativeInstruction>? _byLine;
    private IReadOnlyList<NativeInstruction>? _indexed;

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        if (Instructions is not { Count: > 0 } instructions) return [];

        // Hex1b calls this for the visible range each render; index by display line once so a long
        // listing costs O(visible lines), not O(all instructions), per repaint.
        if (!ReferenceEquals(_indexed, instructions))
        {
            _indexed = instructions;
            _byLine = [];
            foreach (var insn in instructions)
                if (insn.DisplayLine is { } dl)
                    _byLine[dl] = insn;
        }

        var spans = new List<TextDecorationSpan>();
        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            if (!_byLine!.TryGetValue(line, out var insn)) continue;
            if (insn.TargetKind == NativeTargetKind.None || insn.TargetName is not { Length: > 0 }) continue;
            if (insn.Layout is not { TargetStart: >= 0, TargetLength: > 0 } layout) continue;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, layout.TargetStart + 1),
                new DocumentPosition(line, layout.TargetStart + layout.TargetLength + 1),
                Underline));
        }

        return spans;
    }
}
