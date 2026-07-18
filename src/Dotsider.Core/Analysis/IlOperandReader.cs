using System.Buffers.Binary;
using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Performs bounds-checked reads of ECMA-335 IL opcodes and operands.
/// </summary>
internal static class IlOperandReader
{
    /// <summary>
    /// Reads one complete one-byte or two-byte IL opcode.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The current byte offset, advanced only for bytes that are present.</param>
    /// <param name="opCode">The decoded opcode when the read succeeds.</param>
    /// <returns><see langword="true"/> when a complete opcode was read; otherwise, <see langword="false"/>.</returns>
    internal static bool TryReadOpCode(ReadOnlySpan<byte> il, ref int offset, out ILOpCode opCode)
    {
        opCode = default;
        if (offset >= il.Length)
        {
            return false;
        }

        byte first = il[offset++];
        if (first != 0xFE)
        {
            opCode = (ILOpCode)first;
            return true;
        }

        if (offset >= il.Length)
        {
            return false;
        }

        opCode = (ILOpCode)(0xFE00 | il[offset++]);
        return true;
    }

    /// <summary>
    /// Gets the complete encoded length of an operand without reading beyond the IL buffer.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset where the operand begins.</param>
    /// <param name="operandKind">The operand encoding to validate.</param>
    /// <param name="length">The validated operand length when the method succeeds.</param>
    /// <returns><see langword="true"/> when the complete operand is present; otherwise, <see langword="false"/>.</returns>
    internal static bool TryGetOperandLength(
        ReadOnlySpan<byte> il,
        int offset,
        OperandKind operandKind,
        out int length)
    {
        return operandKind switch
        {
            OperandKind.None => TryGetFixedLength(il, offset, 0, out length),
            OperandKind.ShortBranchTarget or OperandKind.ShortInlineI or OperandKind.ShortInlineVar
                => TryGetFixedLength(il, offset, 1, out length),
            OperandKind.InlineVar => TryGetFixedLength(il, offset, 2, out length),
            OperandKind.BranchTarget or OperandKind.InlineI or OperandKind.ShortInlineR
                or OperandKind.InlineString or OperandKind.InlineMethod or OperandKind.InlineField
                or OperandKind.InlineType or OperandKind.InlineTok or OperandKind.InlineSig
                => TryGetFixedLength(il, offset, 4, out length),
            OperandKind.InlineI8 or OperandKind.InlineR => TryGetFixedLength(il, offset, 8, out length),
            OperandKind.InlineSwitch => TryGetSwitchLength(il, offset, out length),
            _ => Fail(out length)
        };
    }

    /// <summary>
    /// Reads a signed byte from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The signed byte value.</returns>
    internal static sbyte ReadSByte(ReadOnlySpan<byte> il, int offset) => unchecked((sbyte)il[offset]);

    /// <summary>
    /// Reads an unsigned byte from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The unsigned byte value.</returns>
    internal static byte ReadByte(ReadOnlySpan<byte> il, int offset) => il[offset];

    /// <summary>
    /// Reads a little-endian unsigned 16-bit integer from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The unsigned 16-bit value.</returns>
    internal static ushort ReadUInt16(ReadOnlySpan<byte> il, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(il[offset..]);

    /// <summary>
    /// Reads a little-endian signed 32-bit integer from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The signed 32-bit value.</returns>
    internal static int ReadInt32(ReadOnlySpan<byte> il, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(il[offset..]);

    /// <summary>
    /// Reads a little-endian signed 64-bit integer from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The signed 64-bit value.</returns>
    internal static long ReadInt64(ReadOnlySpan<byte> il, int offset) =>
        BinaryPrimitives.ReadInt64LittleEndian(il[offset..]);

    /// <summary>
    /// Reads a little-endian single-precision floating-point value from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The single-precision floating-point value.</returns>
    internal static float ReadSingle(ReadOnlySpan<byte> il, int offset) =>
        BitConverter.Int32BitsToSingle(ReadInt32(il, offset));

    /// <summary>
    /// Reads a little-endian double-precision floating-point value from a previously validated operand.
    /// </summary>
    /// <param name="il">The method IL bytes.</param>
    /// <param name="offset">The byte offset to read.</param>
    /// <returns>The double-precision floating-point value.</returns>
    internal static double ReadDouble(ReadOnlySpan<byte> il, int offset) =>
        BitConverter.Int64BitsToDouble(ReadInt64(il, offset));

    private static bool TryGetFixedLength(ReadOnlySpan<byte> il, int offset, int length, out int operandLength)
    {
        operandLength = 0;
        if (offset < 0 || offset > il.Length || length > il.Length - offset)
        {
            return false;
        }

        operandLength = length;
        return true;
    }

    private static bool TryGetSwitchLength(ReadOnlySpan<byte> il, int offset, out int length)
    {
        length = 0;
        if (!TryGetFixedLength(il, offset, sizeof(int), out _))
        {
            return false;
        }

        int count = ReadInt32(il, offset);
        if (count < 0)
        {
            return false;
        }

        int targetsOffset = offset + sizeof(int);
        int remaining = il.Length - targetsOffset;
        if (count > remaining / sizeof(int))
        {
            return false;
        }

        length = sizeof(int) + (count * sizeof(int));
        return true;
    }

    private static bool Fail(out int length)
    {
        length = 0;
        return false;
    }
}
