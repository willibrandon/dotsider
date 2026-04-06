using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Views;

/// <summary>
/// Underlines operands of navigable IL instructions to indicate go-to-definition targets.
/// </summary>
public sealed class IlNavigationDecorationProvider : ITextDecorationProvider
{
    private static readonly HashSet<string> NavigableOpcodes =
    [
        "call", "callvirt", "newobj", "ldftn", "ldvirtftn", "jmp",
        "ldfld", "ldflda", "stfld", "ldsfld", "ldsflda", "stsfld",
        "castclass", "isinst", "newarr", "box", "unbox", "unbox.any",
        "ldelem", "stelem", "ldobj", "stobj", "cpobj", "initobj",
        "constrained", "sizeof", "mkrefany", "refanyval", "ldtoken"
    ];

    private static readonly TextDecoration UnderlineDecoration = new()
    {
        Foreground = Hex1bColor.FromRgb(100, 160, 220),
        UnderlineStyle = UnderlineStyle.Single
    };

    /// <summary>The instruction list for the current method.</summary>
    public IReadOnlyList<IlInstruction>? Instructions { get; set; }

    /// <summary>The number of header lines before instructions start.</summary>
    public int HeaderLineCount { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(
        int startLine, int endLine, IHex1bDocument document)
    {
        if (Instructions is null || Instructions.Count == 0) return [];

        var spans = new List<TextDecorationSpan>();
        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            var idx = line - HeaderLineCount - 1;
            if (idx < 0 || idx >= Instructions.Count) continue;
            var inst = Instructions[idx];
            if (inst.MetadataToken is null || string.IsNullOrEmpty(inst.Operand)
                || !NavigableOpcodes.Contains(inst.OpCode)) continue;

            var lineText = document.GetLineText(line);
            var opcodeIdx = lineText.IndexOf(inst.OpCode, StringComparison.Ordinal);
            if (opcodeIdx < 0) continue;

            var operandStart = opcodeIdx + inst.OpCode.Length;
            while (operandStart < lineText.Length && lineText[operandStart] == ' ') operandStart++;
            if (operandStart >= lineText.Length) continue;

            spans.Add(new TextDecorationSpan(
                new DocumentPosition(line, operandStart + 1),
                new DocumentPosition(line, lineText.Length + 1),
                UnderlineDecoration));
        }
        return spans;
    }
}
