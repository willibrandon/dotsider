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

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        if (Instructions is not { Count: > 0 } instructions) return [];

        var spans = new List<TextDecorationSpan>();
        foreach (var insn in instructions)
        {
            if (insn.DisplayLine is not { } line || line < startLine || line > endLine) continue;
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
