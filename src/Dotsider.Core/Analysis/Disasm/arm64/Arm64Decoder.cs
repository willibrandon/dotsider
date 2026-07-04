using System.Buffers.Binary;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

/// <summary>
/// The table-driven A64 (AArch64) instruction decoder. Every instruction is one 32-bit little-
/// endian word: the word is matched against the dispatched decode group's mask/match rows, then the
/// matched row's <see cref="Arm64Format"/> drives operand extraction and rendering. Length is always
/// four bytes; a word matching no defined row decodes as a <c>.word</c> that never desyncs.
/// </summary>
internal static class Arm64Decoder
{
    /// <summary>Decodes one instruction word beginning at <paramref name="start"/>.</summary>
    /// <param name="code">The code window.</param>
    /// <param name="start">The byte offset of the instruction within <paramref name="code"/>.</param>
    /// <param name="address">The virtual address of the instruction's first byte.</param>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        if (start + 4 > code.Length)
        {
            var b = code[start];
            return Fallback(address, [b], $"0x{b:x2}");
        }

        var word = BinaryPrimitives.ReadUInt32LittleEndian(code[start..]);
        var bytes = code.Slice(start, 4).ToArray();

        var match = Arm64Tables.Decode(word);
        if (match is not { } entry)
            return Fallback(address, bytes, $"0x{word:x8}");

        var decoded = Arm64OperandFormatter.Format(entry, word, address);
        var operandText = string.Join(", ", decoded.Operands.Select(o => o.Text));

        return new NativeInstruction(
            Address: address, Rva: null, FileOffset: null, Bytes: bytes, Length: 4,
            Mnemonic: decoded.Mnemonic, Operands: decoded.Operands, OperandText: operandText,
            Category: decoded.Category, Flow: decoded.Flow,
            TargetAddress: decoded.Target,
            TargetKind: decoded.Target is null ? NativeTargetKind.None
                : decoded.Flow is NativeFlowKind.Call or NativeFlowKind.Jump or NativeFlowKind.ConditionalBranch
                    ? NativeTargetKind.Function
                    : NativeTargetKind.Data);
    }

    private static NativeInstruction Fallback(ulong address, byte[] bytes, string operandText) =>
        new(
            Address: address, Rva: null, FileOffset: null, Bytes: bytes, Length: bytes.Length,
            Mnemonic: bytes.Length == 4 ? ".word" : ".byte",
            Operands: [new NativeOperand(NativeOperandKind.Immediate, operandText)],
            OperandText: operandText,
            Category: NativeInstructionCategory.Unknown, Flow: NativeFlowKind.Sequential, IsFallback: true);
}
