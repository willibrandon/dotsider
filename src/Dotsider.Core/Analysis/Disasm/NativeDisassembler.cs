using System.Text;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Disassembles a native code window into <see cref="NativeInstruction"/>s and a rendered listing,
/// dispatching to the table-driven x86-64 and A64 decoders. The listing mirrors the IL disassembly
/// shape (<c>IlDisassembler.DisassembleWithText</c>): an optional header, then one line per
/// instruction, <c>loc_…:</c> labels for intra-function targets, and each rendered line's column
/// spans recorded on <see cref="NativeInstruction.Layout"/> so the TUI decorates structurally.
/// Call/branch/data targets are resolved to names through a <see cref="NativeSymbolResolver"/>.
/// A byte the decoder cannot recognize renders as an exact-width <c>.byte</c>/<c>.word</c> safety
/// net that never desyncs the listing.
/// </summary>
public static class NativeDisassembler
{
    private const int MaxInstructions = 1 << 20;

    /// <summary>
    /// Decodes a code window into instructions, resolving call/branch/data targets to names and
    /// synthesizing labels for intra-window targets.
    /// </summary>
    /// <param name="code">The exact code bytes of the region to disassemble.</param>
    /// <param name="baseAddress">The virtual address the first byte maps to.</param>
    /// <param name="arch">The instruction-set architecture to decode as.</param>
    /// <param name="resolver">Resolves a target address to a symbol name, or null for no naming.</param>
    public static IReadOnlyList<NativeInstruction> Disassemble(
        ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch,
        NativeSymbolResolver? resolver = null)
    {
        var result = new List<NativeInstruction>();
        var windowEnd = baseAddress + (ulong)code.Length;

        try
        {
            var offset = 0;
            while (offset < code.Length && result.Count < MaxInstructions)
            {
                var insn = DecodeOne(arch, code, offset, baseAddress);
                var length = insn.Length < 1 ? 1 : insn.Length;
                result.Add(insn.Length < 1 ? insn with { Length = 1 } : insn);
                offset += length;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the instructions decoded before the damage.
        }

        return ResolveTargets(result, baseAddress, windowEnd, resolver);
    }

    /// <summary>
    /// Disassembles a code window and renders it to text, returning the text, the instruction list
    /// (each carrying its 1-based <see cref="NativeInstruction.DisplayLine"/> and
    /// <see cref="NativeInstruction.Layout"/>), and the header line count.
    /// </summary>
    /// <param name="code">The exact code bytes of the region to disassemble.</param>
    /// <param name="baseAddress">The virtual address the first byte maps to.</param>
    /// <param name="arch">The instruction-set architecture to decode as.</param>
    /// <param name="header">Optional header lines (without a trailing blank), or null.</param>
    /// <param name="resolver">Resolves a target address to a symbol name, or null for no naming.</param>
    public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)
        DisassembleWithText(
            ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch,
            string? header = null, NativeSymbolResolver? resolver = null)
    {
        var instructions = Disassemble(code, baseAddress, arch, resolver);
        return Render(instructions, header);
    }

    /// <summary>
    /// Decodes a single instruction at <paramref name="offset"/>, dispatching to the per-arch
    /// decoder. The A64 decoder lands later; until then A64 uses the exact-width word fallback.
    /// </summary>
    private static NativeInstruction DecodeOne(
        NativeArchitecture arch, ReadOnlySpan<byte> code, int offset, ulong baseAddress) =>
        arch == NativeArchitecture.Arm64
            ? FallbackWord(code, offset, baseAddress)
            : x64.XarchDecoder.Decode(code, offset, baseAddress + (ulong)offset);

    /// <summary>One-byte <c>.byte 0x..</c> safety net for x64.</summary>
    private static NativeInstruction FallbackByte(ReadOnlySpan<byte> code, int offset, ulong baseAddress)
    {
        var b = code[offset];
        return Fallback(baseAddress + (ulong)offset, [b], ".byte", $"0x{b:x2}");
    }

    /// <summary>One 32-bit <c>.word 0x........</c> safety net for A64 (or bytes at a truncated tail).</summary>
    private static NativeInstruction FallbackWord(ReadOnlySpan<byte> code, int offset, ulong baseAddress)
    {
        if (offset + 4 > code.Length)
        {
            var b = code[offset];
            return Fallback(baseAddress + (ulong)offset, [b], ".byte", $"0x{b:x2}");
        }

        var word = (uint)(code[offset] | (code[offset + 1] << 8) | (code[offset + 2] << 16) | (code[offset + 3] << 24));
        return Fallback(baseAddress + (ulong)offset, [.. code.Slice(offset, 4)], ".word", $"0x{word:x8}");
    }

    private static NativeInstruction Fallback(ulong address, byte[] bytes, string mnemonic, string operandText) =>
        new(
            Address: address,
            Rva: null,
            FileOffset: null,
            Bytes: bytes,
            Length: bytes.Length,
            Mnemonic: mnemonic,
            Operands: [new NativeOperand(NativeOperandKind.Immediate, operandText)],
            OperandText: operandText,
            Category: NativeInstructionCategory.Unknown,
            Flow: NativeFlowKind.Sequential,
            IsFallback: true);

    /// <summary>
    /// Names each instruction's target and refines its kind: a target inside the window becomes a
    /// synthesized <c>loc_…</c> label; a resolved symbol becomes <c>Name</c> or <c>Name+0x..</c>.
    /// </summary>
    private static List<NativeInstruction> ResolveTargets(
        List<NativeInstruction> instructions, ulong windowStart, ulong windowEnd, NativeSymbolResolver? resolver)
    {
        // Intra-window target addresses that land on an instruction boundary get a label.
        var boundaries = new HashSet<ulong>();
        foreach (var insn in instructions) boundaries.Add(insn.Address);

        for (var i = 0; i < instructions.Count; i++)
        {
            var insn = instructions[i];
            if (insn.TargetAddress is not { } target) continue;

            if (target >= windowStart && target < windowEnd && boundaries.Contains(target))
            {
                instructions[i] = insn with
                {
                    TargetKind = NativeTargetKind.LocalLabel,
                    TargetName = LocalLabel(target),
                };
                continue;
            }

            if (resolver is not null && resolver(target, out var sym))
            {
                var name = sym.Offset == 0 ? sym.Name : $"{sym.Name}+0x{sym.Offset:x}";
                var kind = insn.TargetKind == NativeTargetKind.None
                    ? (sym.Kind == NativeSymbolKind.Function ? NativeTargetKind.Function : NativeTargetKind.Data)
                    : insn.TargetKind;
                instructions[i] = insn with { TargetKind = kind, TargetName = name };
            }
        }

        return instructions;
    }

    /// <summary>The synthesized label name for an intra-function target address.</summary>
    internal static string LocalLabel(ulong address) => $"loc_{address:x}";

    private static (string, IReadOnlyList<NativeInstruction>, int) Render(
        IReadOnlyList<NativeInstruction> instructions, string? header)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(header))
        {
            lines.AddRange(header.Split('\n'));
            lines.Add("");
        }

        var headerLineCount = lines.Count;

        // Addresses that are the target of an intra-function label get a "loc_…:" line above them.
        var labels = new HashSet<ulong>();
        foreach (var insn in instructions)
        {
            if (insn.TargetKind == NativeTargetKind.LocalLabel && insn.TargetAddress is { } t)
                labels.Add(t);
        }

        var rendered = new List<NativeInstruction>(instructions.Count);
        var lastLine = 0;
        var lastFile = (string?)null;
        foreach (var insn in instructions)
        {
            if (labels.Contains(insn.Address))
                lines.Add($"{LocalLabel(insn.Address)}:");

            if (insn.Line is { } line && (line != lastLine || insn.SourceFile != lastFile))
            {
                lines.Add($"// {insn.SourceFile}:{line}");
                lastLine = line;
                lastFile = insn.SourceFile;
            }

            var (text, layout) = RenderLine(insn);
            var displayLine = lines.Count + 1;
            lines.Add(text);
            rendered.Add(insn with { DisplayLine = displayLine, Layout = layout });
        }

        return (string.Join('\n', lines), rendered, headerLineCount);
    }

    /// <summary>
    /// Renders one instruction as <c>0x{addr}: {bytes}  {mnemonic} {operands}  ; {target}</c> and
    /// records the mnemonic/operand/target column spans for the decoration providers.
    /// </summary>
    private static (string Text, NativeLineLayout Layout) RenderLine(NativeInstruction insn)
    {
        var sb = new StringBuilder();
        sb.Append("0x").Append(insn.Address.ToString("x")).Append(": ");

        var bytesHex = string.Join(' ', insn.Bytes.Select(b => b.ToString("x2")));
        sb.Append(bytesHex.PadRight(BytesColumnWidth)).Append("  ");

        var mnemonicStart = sb.Length;
        sb.Append(insn.Mnemonic);
        var mnemonicLength = insn.Mnemonic.Length;

        var operandsStart = -1;
        var operandsLength = 0;
        if (!string.IsNullOrEmpty(insn.OperandText))
        {
            sb.Append(' ', Math.Max(1, MnemonicColumnWidth - mnemonicLength));
            operandsStart = sb.Length;
            sb.Append(insn.OperandText);
            operandsLength = insn.OperandText.Length;
        }

        var targetStart = -1;
        var targetLength = 0;
        if (insn.TargetName is { Length: > 0 } targetName)
        {
            sb.Append("  ; ");
            targetStart = sb.Length;
            sb.Append(targetName);
            targetLength = targetName.Length;
        }

        return (sb.ToString(),
            new NativeLineLayout(mnemonicStart, mnemonicLength, operandsStart, operandsLength, targetStart, targetLength));
    }

    private const int BytesColumnWidth = 24;
    private const int MnemonicColumnWidth = 8;
}
