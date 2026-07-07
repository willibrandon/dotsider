using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Provides shared helpers for native instruction decoders.
/// Architecture-specific decoders use these methods to build the common instruction model.
/// Keeping fallback, operand, and immediate handling here keeps the decoders consistent.
/// </summary>
internal static class NativeDecoderSupport
{
    /// <summary>
    /// Creates a structured native instruction.
    /// The rendered operand text is derived from the provided operands.
    /// Decoders use this as the normal construction path for recognized instructions.
    /// </summary>
    public static NativeInstruction Build(
        ulong address,
        byte[] bytes,
        string mnemonic,
        IReadOnlyList<NativeOperand> operands,
        NativeInstructionCategory category = NativeInstructionCategory.Integer,
        NativeFlowKind flow = NativeFlowKind.Sequential,
        ulong? target = null,
        NativeTargetKind targetKind = NativeTargetKind.None)
    {
        var operandText = string.Join(", ", operands.Select(o => o.Text));
        return new NativeInstruction(
            Address: address, Rva: null, FileOffset: null, Bytes: bytes, Length: bytes.Length,
            Mnemonic: mnemonic, Operands: operands, OperandText: operandText,
            Category: category, Flow: flow, TargetAddress: target, TargetKind: targetKind);
    }

    /// <summary>
    /// Creates a one-byte fallback instruction.
    /// The fallback keeps the listing length-exact when a byte cannot be decoded.
    /// Callers should use it only for genuinely unrecognized or truncated input.
    /// </summary>
    public static NativeInstruction FallbackByte(ReadOnlySpan<byte> code, int offset, ulong address)
    {
        var value = code[offset];
        return Fallback(address, [value], ".byte", $"0x{value:x2}");
    }

    /// <summary>
    /// Creates a two-byte fallback instruction.
    /// A truncated tail falls back to the one-byte form to preserve forward progress.
    /// Fixed-width architectures use this for unknown halfword encodings.
    /// </summary>
    public static NativeInstruction FallbackHalf(ReadOnlySpan<byte> code, int offset, ulong address)
    {
        if (offset + 2 > code.Length)
            return FallbackByte(code, offset, address);

        var value = BinaryPrimitives.ReadUInt16LittleEndian(code[offset..]);
        return Fallback(address, code.Slice(offset, 2).ToArray(), ".hword", $"0x{value:x4}");
    }

    /// <summary>
    /// Creates a four-byte fallback instruction.
    /// A truncated tail falls back to the one-byte form to preserve forward progress.
    /// Fixed-width architectures use this for unknown word encodings.
    /// </summary>
    public static NativeInstruction FallbackWord(ReadOnlySpan<byte> code, int offset, ulong address)
    {
        if (offset + 4 > code.Length)
            return FallbackByte(code, offset, address);

        var value = BinaryPrimitives.ReadUInt32LittleEndian(code[offset..]);
        return Fallback(address, code.Slice(offset, 4).ToArray(), ".word", $"0x{value:x8}");
    }

    /// <summary>
    /// Creates a one-byte fallback for WebAssembly bytecode.
    /// Wasm bytecode is byte-oriented, so a single unrecognized opcode is the resync unit.
    /// The returned instruction still carries exact bytes and fallback metadata.
    /// </summary>
    public static NativeInstruction FallbackWasm(ReadOnlySpan<byte> code, int offset, ulong address)
    {
        var value = code[offset];
        return Fallback(address, [value], ".byte", $"0x{value:x2}");
    }

    /// <summary>
    /// Creates a fallback instruction with an explicit directive.
    /// This is the shared implementation for byte, halfword, word, and Wasm fallbacks.
    /// The instruction is marked with <see cref="NativeInstruction.IsFallback"/>.
    /// </summary>
    public static NativeInstruction Fallback(ulong address, byte[] bytes, string mnemonic, string operandText) =>
        new(
            Address: address, Rva: null, FileOffset: null, Bytes: bytes, Length: bytes.Length,
            Mnemonic: mnemonic,
            Operands: [new NativeOperand(NativeOperandKind.Immediate, operandText, Immediate: ParseImmediate(operandText))],
            OperandText: operandText,
            Category: NativeInstructionCategory.Unknown,
            Flow: NativeFlowKind.Sequential,
            IsFallback: true);

    /// <summary>
    /// Creates a register operand.
    /// The same text is used for both the rendered operand and structured register value.
    /// Decoders pass already-formatted architecture register names.
    /// </summary>
    public static NativeOperand Reg(string name) =>
        new(NativeOperandKind.Register, name, Register: name);

    /// <summary>
    /// Creates an immediate operand.
    /// The rendered text defaults to the shared signed hexadecimal style.
    /// Decoders may pass architecture-specific text such as a prefixed immediate.
    /// </summary>
    public static NativeOperand Imm(long value, string? text = null) =>
        new(NativeOperandKind.Immediate, text ?? FormatSigned(value), Immediate: value);

    /// <summary>
    /// Creates a relative branch or call target operand.
    /// The operand records the absolute target address as structured data.
    /// Rendered text uses the same hexadecimal form as native listings.
    /// </summary>
    public static NativeOperand Target(ulong address) =>
        new(NativeOperandKind.RelativeTarget, $"0x{address:x}", Immediate: unchecked((long)address));

    /// <summary>
    /// Creates a memory operand.
    /// Optional base-register and displacement facts are preserved for structured consumers.
    /// The text remains the exact architecture-specific memory expression.
    /// </summary>
    public static NativeOperand Mem(string text, string? @base = null, long displacement = 0) =>
        new(NativeOperandKind.Memory, text, MemoryBase: @base, MemoryDisplacement: displacement);

    /// <summary>
    /// Sign-extends a value from an encoded bit width.
    /// Branch, load, and immediate decoders use this before scaling displacements.
    /// The return value is a signed 64-bit integer ready for address arithmetic.
    /// </summary>
    public static long SignExtend(ulong value, int bits)
    {
        var shift = 64 - bits;
        return ((long)(value << shift)) >> shift;
    }

    /// <summary>
    /// Reads a signed LEB128 value.
    /// The cursor is advanced past the consumed bytes when decoding succeeds.
    /// Malformed or overlong encodings throw so the caller can emit a fallback.
    /// </summary>
    public static long ReadSleb(ReadOnlySpan<byte> code, ref int position, int maxBytes = 5)
    {
        long result = 0;
        var shift = 0;
        byte b;
        var count = 0;
        do
        {
            if (position >= code.Length || count++ >= maxBytes)
                throw new ArgumentOutOfRangeException(nameof(position));

            b = code[position++];
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
        }
        while ((b & 0x80) != 0);

        if (shift < 64 && (b & 0x40) != 0)
            result |= -1L << shift;
        return result;
    }

    /// <summary>
    /// Reads an unsigned LEB128 value.
    /// The cursor is advanced past the consumed bytes when decoding succeeds.
    /// Malformed or overlong encodings throw so the caller can emit a fallback.
    /// </summary>
    public static ulong ReadUleb(ReadOnlySpan<byte> code, ref int position, int maxBytes = 5)
    {
        ulong result = 0;
        var shift = 0;
        var count = 0;
        while (true)
        {
            if (position >= code.Length || count++ >= maxBytes)
                throw new ArgumentOutOfRangeException(nameof(position));

            var b = code[position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }
    }

    /// <summary>
    /// Formats a signed integer for native disassembly.
    /// Positive values render as hexadecimal and negative values keep the sign outside.
    /// This keeps immediates stable across all architecture decoders.
    /// </summary>
    public static string FormatSigned(long value) =>
        value < 0 ? $"-0x{-value:x}" : $"0x{value:x}";

    private static long? ParseImmediate(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var value)
                ? value
                : null;
}
