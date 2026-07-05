using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Disassembles a precompiled ReadyToRun method by walking its code ranges and slicing each from
/// the <em>code image</em> — which for a composite component is a different file than the metadata.
/// Each range (hot entry, funclets, cold) is rendered as its own block, so a method with funclets
/// or split hot/cold code shows every block rather than a single slice.
/// </summary>
public static class ReadyToRunDisassembler
{
    /// <summary>The result of disassembling a method across its ranges.</summary>
    /// <param name="Text">The concatenated, block-separated disassembly text.</param>
    /// <param name="Instructions">Every decoded instruction across all ranges, in order.</param>
    public readonly record struct MethodDisassembly(string Text, IReadOnlyList<NativeInstruction> Instructions);

    /// <summary>
    /// Disassembles <paramref name="entry"/>'s ranges from <paramref name="codeImage"/>. Returns
    /// null when no range is disassemblable (unsupported architecture or no file-backed bytes) —
    /// callers distinguish that from "not precompiled".
    /// </summary>
    /// <param name="codeImage">The analyzer whose bytes hold the native code (self, or the owner composite).</param>
    /// <param name="entry">The method whose ranges to disassemble.</param>
    /// <param name="managedNameResolver">Resolves a call/branch target VA to a managed name, or null.</param>
    public static MethodDisassembly? DisassembleMethod(
        AssemblyAnalyzer codeImage, ReadyToRunMethodEntry entry, Func<ulong, string?>? managedNameResolver)
    {
        if (codeImage.NativeSymbols is not { } info)
            return null;

        var text = new System.Text.StringBuilder();
        var instructions = new List<NativeInstruction>();
        var newlineOffset = 0; // lines already emitted, so each block's DisplayLine re-bases into the joined document
        var rendered = false;

        foreach (var range in entry.CodeRanges)
        {
            if (!info.TryFindByAddress(range.VirtualAddress, out var symbol))
                continue;

            var result = NativeDisassembler.DisassembleSymbol(codeImage, symbol, managedNameResolver);
            if (result is not { } r)
                continue;

            if (rendered)
            {
                text.Append("\n\n");
                newlineOffset += 2;
            }

            var offset = newlineOffset;
            foreach (var instruction in r.Instructions)
                instructions.Add(instruction.DisplayLine is { } line
                    ? instruction with { DisplayLine = line + offset }
                    : instruction);

            text.Append(r.Text);
            foreach (var c in r.Text)
                if (c == '\n')
                    newlineOffset++;
            rendered = true;
        }

        return rendered ? new MethodDisassembly(text.ToString(), instructions) : null;
    }
}
