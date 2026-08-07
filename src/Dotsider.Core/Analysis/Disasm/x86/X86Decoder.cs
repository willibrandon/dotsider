using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm.x86;

/// <summary>
/// Decodes 32-bit x86 instructions emitted by .NET native code paths.
/// The decoder is intentionally table-oriented around common RyuJIT and ReadyToRun forms.
/// Unknown bytes fall back with exact length so listings do not desynchronize.
/// </summary>
internal static class X86Decoder
{
    private static readonly string[] R8 = ["al", "cl", "dl", "bl", "ah", "ch", "dh", "bh"];
    private static readonly string[] R16 = ["ax", "cx", "dx", "bx", "sp", "bp", "si", "di"];
    private static readonly string[] R32 = ["eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi"];
    private static readonly string[] Xmm = ["xmm0", "xmm1", "xmm2", "xmm3", "xmm4", "xmm5", "xmm6", "xmm7"];
    private static readonly string[] Jcc =
    [
        "jo", "jno", "jb", "jae", "je", "jne", "jbe", "ja",
        "js", "jns", "jp", "jnp", "jl", "jge", "jle", "jg"
    ];

    /// <summary>
    /// Decodes one x86 instruction beginning at the requested byte offset.
    /// Prefixes, ModRM, SIB, displacement, and immediate bytes are consumed as one instruction.
    /// The returned model carries structured operands for CLI, MCP, and TUI consumers.
    /// </summary>
    /// <param name="code">The code window.</param>
    /// <param name="start">The byte offset within <paramref name="code"/>.</param>
    /// <param name="address">The virtual address of the instruction's first byte.</param>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, code.Length);

        var pos = start;
        var operand16 = false;
        var address16 = false;

        while (pos < code.Length)
        {
            var p = code[pos];
            if (p == 0x66) { operand16 = true; pos++; continue; }
            if (p == 0x67) { address16 = true; pos++; continue; }
            if (p is 0xF0 or 0xF2 or 0xF3 or 0x26 or 0x2E or 0x36 or 0x3E or 0x64 or 0x65)
            {
                pos++;
                continue;
            }

            break;
        }

        if (pos >= code.Length)
            return NativeDecoderSupport.FallbackByte(code, start, address);

        var op = code[pos++];
        var size = operand16 ? 2 : 4;

        try
        {
            if (op is >= 0x40 and <= 0x47)
                return Simple(code, start, pos, address, "inc", [Reg(op & 7, size)]);
            if (op is >= 0x48 and <= 0x4F)
                return Simple(code, start, pos, address, "dec", [Reg(op & 7, size)]);
            if (op is >= 0x50 and <= 0x57)
                return Simple(code, start, pos, address, "push", [Reg(op & 7, 4)]);
            if (op is >= 0x58 and <= 0x5F)
                return Simple(code, start, pos, address, "pop", [Reg(op & 7, 4)]);
            if (op is >= 0x70 and <= 0x7F)
                return Branch8(code, start, pos, address, Jcc[op & 0xF], NativeFlowKind.ConditionalBranch);
            if (op is >= 0xB0 and <= 0xB7)
            {
                var imm = ReadU8(code, ref pos);
                return Simple(code, start, pos, address, "mov", [Reg(op & 7, 1), Imm(imm)]);
            }
            if (op is >= 0xB8 and <= 0xBF)
            {
                var imm = size == 2 ? ReadU16(code, ref pos) : ReadU32(code, ref pos);
                return Simple(code, start, pos, address, "mov", [Reg(op & 7, size), Imm(imm)]);
            }

            return op switch
            {
                0x90 => Simple(code, start, pos, address, "nop", [], NativeInstructionCategory.System),
                0xCC => Simple(code, start, pos, address, "int3", [], NativeInstructionCategory.System),
                0xC3 => Simple(code, start, pos, address, "ret", [], NativeInstructionCategory.Control, NativeFlowKind.Return),
                0xC2 => RetImm(code, start, ref pos, address),
                0xC9 => Simple(code, start, pos, address, "leave", []),
                0x68 => PushImm(code, start, ref pos, address, size),
                0x6A => PushImm8(code, start, ref pos, address),
                0xA1 => MovMoffsToEax(code, start, ref pos, address),
                0xA3 => MovEaxToMoffs(code, start, ref pos, address),
                0xE8 => Branch32(code, start, pos, address, "call", NativeFlowKind.Call),
                0xE9 => Branch32(code, start, pos, address, "jmp", NativeFlowKind.Jump),
                0xEB => Branch8(code, start, pos, address, "jmp", NativeFlowKind.Jump),
                0x0F => Decode0F(code, start, ref pos, address, size, address16),
                0x8A => ModRmBinary(code, start, ref pos, address, "mov", 1, Direction.RegFromRm, address16),
                0x88 => ModRmBinary(code, start, ref pos, address, "mov", 1, Direction.RmFromReg, address16),
                0x8B => ModRmBinary(code, start, ref pos, address, "mov", size, Direction.RegFromRm, address16),
                0x89 => ModRmBinary(code, start, ref pos, address, "mov", size, Direction.RmFromReg, address16),
                0x8D => ModRmBinary(code, start, ref pos, address, "lea", size, Direction.RegFromRm, address16),
                0x85 => ModRmBinary(code, start, ref pos, address, "test", size, Direction.RmFromReg, address16),
                0x84 => ModRmBinary(code, start, ref pos, address, "test", 1, Direction.RmFromReg, address16),
                0x3A => ModRmBinary(code, start, ref pos, address, "cmp", 1, Direction.RegFromRm, address16),
                0x38 => ModRmBinary(code, start, ref pos, address, "cmp", 1, Direction.RmFromReg, address16),
                0x3B => ModRmBinary(code, start, ref pos, address, "cmp", size, Direction.RegFromRm, address16),
                0x39 => ModRmBinary(code, start, ref pos, address, "cmp", size, Direction.RmFromReg, address16),
                0x33 => ModRmBinary(code, start, ref pos, address, "xor", size, Direction.RegFromRm, address16),
                0x31 => ModRmBinary(code, start, ref pos, address, "xor", size, Direction.RmFromReg, address16),
                0x03 => ModRmBinary(code, start, ref pos, address, "add", size, Direction.RegFromRm, address16),
                0x01 => ModRmBinary(code, start, ref pos, address, "add", size, Direction.RmFromReg, address16),
                0x2B => ModRmBinary(code, start, ref pos, address, "sub", size, Direction.RegFromRm, address16),
                0x29 => ModRmBinary(code, start, ref pos, address, "sub", size, Direction.RmFromReg, address16),
                0x23 => ModRmBinary(code, start, ref pos, address, "and", size, Direction.RegFromRm, address16),
                0x21 => ModRmBinary(code, start, ref pos, address, "and", size, Direction.RmFromReg, address16),
                0x0B => ModRmBinary(code, start, ref pos, address, "or", size, Direction.RegFromRm, address16),
                0x09 => ModRmBinary(code, start, ref pos, address, "or", size, Direction.RmFromReg, address16),
                0x83 => Group83(code, start, ref pos, address, size, address16),
                0xC6 => MovImmRm8(code, start, ref pos, address, address16),
                0xC7 => MovImmRm(code, start, ref pos, address, size, address16),
                0xFF => GroupFf(code, start, ref pos, address, address16),
                _ => NativeDecoderSupport.FallbackByte(code, start, address),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return NativeDecoderSupport.FallbackByte(code, start, address);
        }
    }

    private static NativeInstruction Decode0F(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, int size, bool address16)
    {
        var op = ReadU8(code, ref pos);
        if (op == 0x0B)
            return Simple(code, start, pos, address, "ud2", [], NativeInstructionCategory.System);
        if (op == 0x1F)
            return ModRmUnary(code, start, ref pos, address, "nop", size, address16, NativeInstructionCategory.System);
        if (op == 0x11)
            return ModRmVectorBinary(code, start, ref pos, address, "movups", Direction.RmFromReg, address16);
        if (op == 0x57)
            return ModRmVectorBinary(code, start, ref pos, address, "xorps", Direction.RegFromRm, address16);
        if (op is >= 0x80 and <= 0x8F)
            return Branch32(code, start, pos, address, Jcc[op & 0xF], NativeFlowKind.ConditionalBranch);

        if (op is >= 0x90 and <= 0x9F)
            return ModRmUnary(code, start, ref pos, address, $"set{Jcc[op & 0xF][1..]}", 1, address16);

        if (op == 0xB6)
            return ModRmBinary(code, start, ref pos, address, "movzx", 1, Direction.Reg32FromRm, address16);
        if (op == 0xB7)
            return ModRmBinary(code, start, ref pos, address, "movzx", 2, Direction.Reg32FromRm, address16);

        return NativeDecoderSupport.FallbackByte(code, start, address);
    }

    private static NativeInstruction Group83(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, int size, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        var group = (modrm >> 3) & 7;
        var rm = DecodeRm(code, ref pos, modrm, size, address16);
        var imm = (sbyte)ReadU8(code, ref pos);
        var mnemonic = group switch
        {
            0 => "add",
            1 => "or",
            4 => "and",
            5 => "sub",
            6 => "xor",
            7 => "cmp",
            _ => null
        };
        if (mnemonic is null)
            return NativeDecoderSupport.FallbackByte(code, start, address);
        return Simple(code, start, pos, address, mnemonic, [rm, Imm(imm)]);
    }

    private static NativeInstruction GroupFf(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        var group = (modrm >> 3) & 7;
        var rm = DecodeRm(code, ref pos, modrm, 4, address16);
        return group switch
        {
            0 => Simple(code, start, pos, address, "inc", [rm]),
            1 => Simple(code, start, pos, address, "dec", [rm]),
            2 => Simple(code, start, pos, address, "call", [rm], NativeInstructionCategory.Control, NativeFlowKind.IndirectCall),
            4 => Simple(code, start, pos, address, "jmp", [rm], NativeInstructionCategory.Control, NativeFlowKind.IndirectJump),
            6 => Simple(code, start, pos, address, "push", [rm]),
            _ => NativeDecoderSupport.FallbackByte(code, start, address),
        };
    }

    private static NativeInstruction MovImmRm(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, int size, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        if (((modrm >> 3) & 7) != 0)
            return NativeDecoderSupport.FallbackByte(code, start, address);
        var rm = DecodeRm(code, ref pos, modrm, size, address16);
        var imm = size == 2 ? ReadU16(code, ref pos) : ReadU32(code, ref pos);
        return Simple(code, start, pos, address, "mov", [rm, Imm(imm)]);
    }

    private static NativeInstruction MovImmRm8(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        if (((modrm >> 3) & 7) != 0)
            return NativeDecoderSupport.FallbackByte(code, start, address);
        var rm = DecodeRm(code, ref pos, modrm, 1, address16);
        var imm = ReadU8(code, ref pos);
        return Simple(code, start, pos, address, "mov", [rm, Imm(imm)]);
    }

    private static NativeInstruction ModRmBinary(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic, int size,
        Direction direction, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        var regSize = direction == Direction.Reg32FromRm ? 4 : size;
        var reg = Reg((modrm >> 3) & 7, regSize);
        var rm = DecodeRm(code, ref pos, modrm, size, address16);
        var operands = direction is Direction.RegFromRm or Direction.Reg32FromRm ? new[] { reg, rm } : [rm, reg];
        return Simple(code, start, pos, address, mnemonic, operands);
    }

    private static NativeInstruction ModRmVectorBinary(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic,
        Direction direction, bool address16)
    {
        var modrm = ReadU8(code, ref pos);
        var reg = NativeDecoderSupport.Reg(Xmm[(modrm >> 3) & 7]);
        var rm = DecodeVectorRm(code, ref pos, modrm, address16);
        var operands = direction == Direction.RegFromRm ? new[] { reg, rm } : [rm, reg];
        return Simple(code, start, pos, address, mnemonic, operands, NativeInstructionCategory.Vector);
    }

    private static NativeInstruction ModRmUnary(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic, int size,
        bool address16, NativeInstructionCategory category = NativeInstructionCategory.Integer)
    {
        var modrm = ReadU8(code, ref pos);
        var rm = DecodeRm(code, ref pos, modrm, size, address16);
        return Simple(code, start, pos, address, mnemonic, [rm], category);
    }

    private static NativeOperand DecodeRm(ReadOnlySpan<byte> code, ref int pos, int modrm, int size, bool address16)
    {
        var mod = (modrm >> 6) & 3;
        var rm = modrm & 7;
        if (mod == 3)
            return Reg(rm, size);
        if (address16)
            return Mem16(code, ref pos, mod, rm);

        string? baseReg = null;
        string? indexReg = null;
        var scale = 1;
        long disp = 0;

        if (rm == 4)
        {
            var sib = ReadU8(code, ref pos);
            scale = 1 << ((sib >> 6) & 3);
            var index = (sib >> 3) & 7;
            var b = sib & 7;
            if (index != 4) indexReg = R32[index];
            if (mod == 0 && b == 5) disp = (int)ReadU32(code, ref pos);
            else baseReg = R32[b];
        }
        else if (mod == 0 && rm == 5)
        {
            disp = (int)ReadU32(code, ref pos);
        }
        else
        {
            baseReg = R32[rm];
        }

        if (mod == 1) disp = (sbyte)ReadU8(code, ref pos);
        else if (mod == 2) disp = (int)ReadU32(code, ref pos);

        return NativeDecoderSupport.Mem(MemText(baseReg, indexReg, scale, disp), baseReg, disp);
    }

    private static NativeOperand Mem16(ReadOnlySpan<byte> code, ref int pos, int mod, int rm)
    {
        var bases = new[] { "bx+si", "bx+di", "bp+si", "bp+di", "si", "di", "bp", "bx" };
        long disp = 0;
        string? baseReg = bases[rm];
        if (mod == 0 && rm == 6)
        {
            baseReg = null;
            disp = (short)ReadU16(code, ref pos);
        }
        else if (mod == 1)
        {
            disp = (sbyte)ReadU8(code, ref pos);
        }
        else if (mod == 2)
        {
            disp = (short)ReadU16(code, ref pos);
        }

        return NativeDecoderSupport.Mem(MemText(baseReg, null, 1, disp), baseReg, disp);
    }

    private static NativeOperand DecodeVectorRm(ReadOnlySpan<byte> code, ref int pos, int modrm, bool address16)
    {
        var mod = (modrm >> 6) & 3;
        if (mod == 3)
            return NativeDecoderSupport.Reg(Xmm[modrm & 7]);

        return DecodeRm(code, ref pos, modrm, 4, address16);
    }

    private static string MemText(string? baseReg, string? indexReg, int scale, long disp)
    {
        var parts = new List<string>();
        if (baseReg is not null) parts.Add(baseReg);
        if (indexReg is not null) parts.Add(scale == 1 ? indexReg : $"{indexReg}*{scale}");
        var text = string.Join("+", parts);
        if (disp != 0 || text.Length == 0)
        {
            var sign = disp < 0 ? "-" : text.Length == 0 ? "" : "+";
            text += $"{sign}0x{Math.Abs(disp):x}";
        }

        return $"[{text}]";
    }

    private static NativeInstruction PushImm(ReadOnlySpan<byte> code, int start, ref int pos, ulong address, int size)
    {
        var imm = size == 2 ? ReadU16(code, ref pos) : ReadU32(code, ref pos);
        return Simple(code, start, pos, address, "push", [Imm(imm)]);
    }

    private static NativeInstruction PushImm8(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var imm = (sbyte)ReadU8(code, ref pos);
        return Simple(code, start, pos, address, "push", [Imm(imm)]);
    }

    private static NativeInstruction RetImm(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var imm = ReadU16(code, ref pos);
        return Simple(code, start, pos, address, "ret", [Imm(imm)], NativeInstructionCategory.Control, NativeFlowKind.Return);
    }

    private static NativeInstruction MovMoffsToEax(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var absolute = ReadU32(code, ref pos);
        return Simple(code, start, pos, address, "mov", [Reg(0, 4), NativeDecoderSupport.Mem($"[0x{absolute:x}]", displacement: absolute)]);
    }

    private static NativeInstruction MovEaxToMoffs(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var absolute = ReadU32(code, ref pos);
        return Simple(code, start, pos, address, "mov", [NativeDecoderSupport.Mem($"[0x{absolute:x}]", displacement: absolute), Reg(0, 4)]);
    }

    private static NativeInstruction Branch8(
        ReadOnlySpan<byte> code, int start, int pos, ulong address, string mnemonic, NativeFlowKind flow)
    {
        var disp = (sbyte)ReadU8(code, ref pos);
        var target = (ulong)((long)address + (pos - start) + disp);
        return Simple(code, start, pos, address, mnemonic, [NativeDecoderSupport.Target(target)],
            NativeInstructionCategory.Control, flow, target);
    }

    private static NativeInstruction Branch32(
        ReadOnlySpan<byte> code, int start, int pos, ulong address, string mnemonic, NativeFlowKind flow)
    {
        var disp = (int)ReadU32(code, ref pos);
        var target = (ulong)((long)address + (pos - start) + disp);
        return Simple(code, start, pos, address, mnemonic, [NativeDecoderSupport.Target(target)],
            NativeInstructionCategory.Control, flow, target);
    }

    private static NativeInstruction Simple(
        ReadOnlySpan<byte> code, int start, int pos, ulong address, string mnemonic, IReadOnlyList<NativeOperand> operands,
        NativeInstructionCategory category = NativeInstructionCategory.Integer,
        NativeFlowKind flow = NativeFlowKind.Sequential,
        ulong? target = null) =>
        NativeDecoderSupport.Build(
            address, code[start..pos].ToArray(), mnemonic, operands,
            category, flow, target,
            target is null ? NativeTargetKind.None : NativeTargetKind.Function);

    private static NativeOperand Reg(int index, int size) =>
        NativeDecoderSupport.Reg(size switch { 1 => R8[index], 2 => R16[index], _ => R32[index] });

    private static NativeOperand Imm(long value) => NativeDecoderSupport.Imm(value);

    private static byte ReadU8(ReadOnlySpan<byte> code, ref int pos)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pos, code.Length);
        return code[pos++];
    }

    private static ushort ReadU16(ReadOnlySpan<byte> code, ref int pos)
    {
        if (pos + 2 > code.Length) throw new ArgumentOutOfRangeException(nameof(pos));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(code[pos..]);
        pos += 2;
        return value;
    }

    private static uint ReadU32(ReadOnlySpan<byte> code, ref int pos)
    {
        if (pos + 4 > code.Length) throw new ArgumentOutOfRangeException(nameof(pos));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(code[pos..]);
        pos += 4;
        return value;
    }

    private enum Direction
    {
        RegFromRm,
        Reg32FromRm,
        RmFromReg,
    }
}
