using Dotsider.Core.Analysis.Models;
using Hex1b.Documents;

namespace Dotsider.Views;

/// <summary>
/// Subtle syntax highlighting for the native disassembly listing, mirroring the managed IL
/// highlighting but driven entirely by the decoded <see cref="NativeInstruction"/> list and its
/// <see cref="NativeLineLayout"/> column spans (via <see cref="NativeInstruction.DisplayLine"/>) —
/// the address, mnemonic, per-operand registers/immediates, and the resolved target comment are
/// colored from structure, never by re-parsing the rendered text. Comment and label lines (which
/// have no instruction) are colored by their leading token. Inert (no decorations) until
/// <see cref="Instructions"/> is set, so it is safe to attach in managed mode too.
/// </summary>
public sealed class NativeSyntaxDecorationProvider : ITextDecorationProvider
{
    private static readonly TextDecoration Address = new() { Foreground = IlColorizer.AddressColor };
    private static readonly TextDecoration Opcode = new() { Foreground = IlColorizer.OpcodeColor };
    private static readonly TextDecoration Comment = new() { Foreground = IlColorizer.CommentColor };
    private static readonly TextDecoration Directive = new() { Foreground = IlColorizer.DirectiveColor };
    private static readonly TextDecoration Number = new() { Foreground = IlColorizer.StringColor };

    /// <summary>The decoded instructions of the current listing, keyed by their 1-based display line.</summary>
    public IReadOnlyList<NativeInstruction>? Instructions { get; set; }

    private Dictionary<int, NativeInstruction>? _byLine;
    private IReadOnlyList<NativeInstruction>? _indexed;

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        var spans = new List<TextDecorationSpan>();
        if (Instructions is not { } instructions)
            return spans;

        if (!ReferenceEquals(_indexed, instructions))
        {
            _indexed = instructions;
            _byLine = [];
            foreach (var insn in instructions)
                if (insn.DisplayLine is { } dl)
                    _byLine[dl] = insn;
        }

        for (var line = startLine; line <= endLine && line <= document.LineCount; line++)
        {
            if (_byLine!.TryGetValue(line, out var insn) && insn.Layout is { } layout)
            {
                DecorateInstruction(spans, document, line, insn, layout);
                continue;
            }

            // Comment (// …) and local-label (loc_…:) lines have no structured instruction.
            var text = document.GetLineText(line);
            var trimmed = text.AsSpan().TrimStart();
            if (trimmed.StartsWith("//"))
                spans.Add(Span(line, 1, text.Length + 1, Comment));
            else if (trimmed.StartsWith("loc_"))
                spans.Add(Span(line, 1, text.Length + 1, Directive));
        }

        return spans;
    }

    private static void DecorateInstruction(
        List<TextDecorationSpan> spans, IHex1bDocument document, int line, NativeInstruction insn, NativeLineLayout layout)
    {
        var text = document.GetLineText(line);

        // Address prefix up to and including the first ':'.
        var colon = text.IndexOf(':');
        if (colon > 0)
            spans.Add(Span(line, 1, colon + 2, Address));

        if (layout.MnemonicLength > 0)
            spans.Add(Span(line, layout.MnemonicStart + 1, layout.MnemonicStart + layout.MnemonicLength + 1, Opcode));

        // Per-operand: registers vs immediates/targets, positioned from the operand list.
        if (layout.OperandsStart >= 0 && insn.Operands.Count > 0)
        {
            var col = layout.OperandsStart;
            for (var i = 0; i < insn.Operands.Count; i++)
            {
                var op = insn.Operands[i];
                var len = op.Text.Length;
                var decoration = op.Kind switch
                {
                    NativeOperandKind.Register => Directive,
                    NativeOperandKind.Immediate or NativeOperandKind.RelativeTarget => Number,
                    _ => null,
                };
                if (decoration is not null && len > 0)
                    spans.Add(Span(line, col + 1, col + len + 1, decoration));

                col += len + 2; // operand text + ", " separator
            }
        }

        if (layout is { TargetStart: >= 0, TargetLength: > 0 })
            spans.Add(Span(line, layout.TargetStart + 1, layout.TargetStart + layout.TargetLength + 1, Comment));
    }

    private static TextDecorationSpan Span(int line, int startCol, int endCol, TextDecoration decoration) =>
        new(new DocumentPosition(line, startCol), new DocumentPosition(line, endCol), decoration);
}
