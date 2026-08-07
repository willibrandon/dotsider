using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm.riscv64;

/// <summary>
/// Decodes RV64 instructions emitted by the .NET RISC-V64 backend.
/// The decoder handles base and compressed forms needed by RyuJIT and ReadyToRun samples.
/// Unknown halfwords or words fall back with exact length so listings do not desynchronize.
/// </summary>
internal static class RiscV64Decoder
{
    private static readonly string[] Registers =
    [
        "zero", "ra", "sp", "gp", "tp", "t0", "t1", "t2",
        "s0", "s1", "a0", "a1", "a2", "a3", "a4", "a5",
        "a6", "a7", "s2", "s3", "s4", "s5", "s6", "s7",
        "s8", "s9", "s10", "s11", "t3", "t4", "t5", "t6"
    ];

    private static readonly string[] FRegisters =
    [
        "ft0", "ft1", "ft2", "ft3", "ft4", "ft5", "ft6", "ft7",
        "fs0", "fs1", "fa0", "fa1", "fa2", "fa3", "fa4", "fa5",
        "fa6", "fa7", "fs2", "fs3", "fs4", "fs5", "fs6", "fs7",
        "fs8", "fs9", "fs10", "fs11", "ft8", "ft9", "ft10", "ft11"
    ];

    /// <summary>
    /// Decodes one RISC-V64 instruction beginning at the requested byte offset.
    /// Compressed instructions are returned as two-byte instructions and base forms as four-byte instructions.
    /// The returned model carries structured operands for CLI, MCP, and TUI consumers.
    /// </summary>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        if (start + 2 > code.Length)
            return NativeDecoderSupport.FallbackByte(code, start, address);

        var half = BinaryPrimitives.ReadUInt16LittleEndian(code[start..]);
        return (half & 0x3) != 0x3
            ? DecodeCompressed(code, start, address, half)
            : DecodeWord(code, start, address);
    }

    private static NativeInstruction DecodeCompressed(ReadOnlySpan<byte> code, int start, ulong address, ushort h)
    {
        var bytes = code.Slice(start, 2).ToArray();
        var quadrant = h & 0x3;
        var funct3 = (h >> 13) & 0x7;

        if (quadrant == 1 && funct3 == 0)
        {
            var rd = (h >> 7) & 0x1F;
            var imm = CImm6(h);
            return rd == 0 && imm == 0
                ? Build(address, bytes, "c.nop", [])
                : Build(address, bytes, "c.addi", [Reg(rd), Imm(imm)]);
        }
        if (quadrant == 1 && funct3 == 2)
            return Build(address, bytes, "c.li", [Reg((h >> 7) & 0x1F), Imm(CImm6(h))]);
        if (quadrant == 1 && funct3 == 3)
            return Build(address, bytes, ((h >> 7) & 0x1F) == 2 ? "c.addi16sp" : "c.lui",
                [Reg((h >> 7) & 0x1F), Imm(CImm6(h))]);
        if (quadrant == 1 && funct3 is 5 or 1)
        {
            var target = (ulong)((long)address + CJImm(h));
            return Build(address, bytes, funct3 == 5 ? "c.j" : "c.jal", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, funct3 == 5 ? NativeFlowKind.Jump : NativeFlowKind.Call, target);
        }
        if (quadrant == 1 && funct3 is 6 or 7)
        {
            var rs1 = 8 + ((h >> 7) & 7);
            var target = (ulong)((long)address + CBImm(h));
            return Build(address, bytes, funct3 == 6 ? "c.beqz" : "c.bnez", [Reg(rs1), NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.ConditionalBranch, target);
        }
        if (quadrant == 2 && funct3 == 4)
        {
            var rd = (h >> 7) & 0x1F;
            var rs2 = (h >> 2) & 0x1F;
            if (((h >> 12) & 1) == 0)
                return rs2 == 0
                    ? Build(address, bytes, "c.jr", [Reg(rd)], NativeInstructionCategory.Control, NativeFlowKind.IndirectJump)
                    : Build(address, bytes, "c.mv", [Reg(rd), Reg(rs2)]);
            if (rd == 0 && rs2 == 0)
                return Build(address, bytes, "c.ebreak", [], NativeInstructionCategory.System);
            return rs2 == 0
                ? Build(address, bytes, "c.jalr", [Reg(rd)], NativeInstructionCategory.Control, NativeFlowKind.IndirectCall)
                : Build(address, bytes, "c.add", [Reg(rd), Reg(rs2)]);
        }
        if (quadrant == 2 && funct3 == 2)
            return Build(address, bytes, "c.lwsp", [Reg((h >> 7) & 0x1F), Mem($"0x{CLwspImm(h):x}(sp)", "sp", CLwspImm(h))]);
        if (quadrant == 2 && funct3 == 3)
            return Build(address, bytes, "c.ldsp", [Reg((h >> 7) & 0x1F), Mem($"0x{CLdspImm(h):x}(sp)", "sp", CLdspImm(h))]);
        if (quadrant == 2 && funct3 == 6)
            return Build(address, bytes, "c.swsp", [Reg((h >> 2) & 0x1F), Mem($"0x{CSwspImm(h):x}(sp)", "sp", CSwspImm(h))]);
        if (quadrant == 2 && funct3 == 7)
            return Build(address, bytes, "c.sdsp", [Reg((h >> 2) & 0x1F), Mem($"0x{CSdspImm(h):x}(sp)", "sp", CSdspImm(h))]);
        if (quadrant == 0 && funct3 == 0)
        {
            var rd = 8 + ((h >> 2) & 7);
            return Build(address, bytes, "c.addi4spn", [Reg(rd), Reg(2), Imm(CAddi4SpnImm(h))]);
        }
        if (quadrant == 0 && funct3 == 2)
        {
            var rd = 8 + ((h >> 2) & 7);
            var rs1 = 8 + ((h >> 7) & 7);
            return Build(address, bytes, "c.lw", [Reg(rd), Mem($"0x{CLwImm(h):x}({Registers[rs1]})", Registers[rs1], CLwImm(h))]);
        }
        if (quadrant == 0 && funct3 == 3)
        {
            var rd = 8 + ((h >> 2) & 7);
            var rs1 = 8 + ((h >> 7) & 7);
            return Build(address, bytes, "c.ld", [Reg(rd), Mem($"0x{CLdImm(h):x}({Registers[rs1]})", Registers[rs1], CLdImm(h))]);
        }
        if (quadrant == 0 && funct3 == 6)
        {
            var rs2 = 8 + ((h >> 2) & 7);
            var rs1 = 8 + ((h >> 7) & 7);
            return Build(address, bytes, "c.sw", [Reg(rs2), Mem($"0x{CLwImm(h):x}({Registers[rs1]})", Registers[rs1], CLwImm(h))]);
        }
        if (quadrant == 0 && funct3 == 7)
        {
            var rs2 = 8 + ((h >> 2) & 7);
            var rs1 = 8 + ((h >> 7) & 7);
            return Build(address, bytes, "c.sd", [Reg(rs2), Mem($"0x{CLdImm(h):x}({Registers[rs1]})", Registers[rs1], CLdImm(h))]);
        }

        return NativeDecoderSupport.FallbackHalf(code, start, address);
    }

    private static NativeInstruction DecodeWord(ReadOnlySpan<byte> code, int start, ulong address)
    {
        if (start + 4 > code.Length)
            return NativeDecoderSupport.FallbackByte(code, start, address);

        var word = BinaryPrimitives.ReadUInt32LittleEndian(code[start..]);
        var bytes = code.Slice(start, 4).ToArray();
        var opcode = word & 0x7F;
        var rd = (int)((word >> 7) & 0x1F);
        var funct3 = (int)((word >> 12) & 0x7);
        var rs1 = (int)((word >> 15) & 0x1F);
        var rs2 = (int)((word >> 20) & 0x1F);
        var funct7 = (int)((word >> 25) & 0x7F);

        return opcode switch
        {
            0x37 => Build(address, bytes, "lui", [Reg(rd), Imm((int)(word & 0xFFFFF000))]),
            0x17 => Build(address, bytes, "auipc", [Reg(rd), Imm((int)(word & 0xFFFFF000))]),
            0x6F => Jal(address, bytes, rd, JImm(word)),
            0x67 => Jalr(address, bytes, rd, rs1, IImm(word)),
            0x63 => Branch(address, bytes, funct3, rs1, rs2, BImm(word)),
            0x03 => Load(address, bytes, funct3, rd, rs1, IImm(word)),
            0x23 => Store(address, bytes, funct3, rs1, rs2, SImm(word)),
            0x13 => OpImm(address, bytes, funct3, funct7, rd, rs1, IImm(word)),
            0x1B => OpImm32(address, bytes, funct3, funct7, rd, rs1, IImm(word)),
            0x33 => Op(address, bytes, funct3, funct7, rd, rs1, rs2),
            0x3B => Op32(address, bytes, funct3, funct7, rd, rs1, rs2),
            0x2F => Atomic(address, bytes, word, funct3, rd, rs1, rs2),
            0x0F => Build(address, bytes, funct3 == 1 ? "fence.i" : "fence", [], NativeInstructionCategory.System),
            0x73 => System(address, bytes, word, funct3, rd, rs1),
            0x07 => FLoad(address, bytes, funct3, rd, rs1, IImm(word)),
            0x27 => FStore(address, bytes, funct3, rs1, rs2, SImm(word)),
            0x53 => FpOp(address, bytes, word, rd, rs1, rs2),
            _ => NativeDecoderSupport.FallbackWord(code, start, address),
        };
    }

    private static NativeInstruction Jal(ulong address, byte[] bytes, int rd, long disp)
    {
        var target = (ulong)((long)address + disp);
        var mnemonic = rd == 0 ? "j" : "jal";
        var ops = rd == 0 ? [NativeDecoderSupport.Target(target)] : new[] { Reg(rd), NativeDecoderSupport.Target(target) };
        return Build(address, bytes, mnemonic, ops, NativeInstructionCategory.Control,
            rd == 0 ? NativeFlowKind.Jump : NativeFlowKind.Call, target);
    }

    private static NativeInstruction Jalr(ulong address, byte[] bytes, int rd, int rs1, long imm)
    {
        var flow = rd == 0 && rs1 == 1 && imm == 0
            ? NativeFlowKind.Return
            : rd == 0 ? NativeFlowKind.IndirectJump : NativeFlowKind.IndirectCall;
        return Build(address, bytes, "jalr", [Reg(rd), Mem($"{imm}({Registers[rs1]})", Registers[rs1], imm)],
            NativeInstructionCategory.Control, flow);
    }

    private static NativeInstruction Branch(ulong address, byte[] bytes, int funct3, int rs1, int rs2, long disp)
    {
        var mnemonic = funct3 switch
        {
            0 => "beq",
            1 => "bne",
            4 => "blt",
            5 => "bge",
            6 => "bltu",
            7 => "bgeu",
            _ => null
        };
        if (mnemonic is null)
            return NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}");
        var target = (ulong)((long)address + disp);
        return Build(address, bytes, mnemonic, [Reg(rs1), Reg(rs2), NativeDecoderSupport.Target(target)],
            NativeInstructionCategory.Control, NativeFlowKind.ConditionalBranch, target);
    }

    private static NativeInstruction Load(ulong address, byte[] bytes, int funct3, int rd, int rs1, long imm)
    {
        var mnemonic = funct3 switch { 0 => "lb", 1 => "lh", 2 => "lw", 3 => "ld", 4 => "lbu", 5 => "lhu", 6 => "lwu", _ => null };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [Reg(rd), Mem($"{imm}({Registers[rs1]})", Registers[rs1], imm)]);
    }

    private static NativeInstruction Store(ulong address, byte[] bytes, int funct3, int rs1, int rs2, long imm)
    {
        var mnemonic = funct3 switch { 0 => "sb", 1 => "sh", 2 => "sw", 3 => "sd", _ => null };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [Reg(rs2), Mem($"{imm}({Registers[rs1]})", Registers[rs1], imm)]);
    }

    private static NativeInstruction OpImm(ulong address, byte[] bytes, int funct3, int funct7, int rd, int rs1, long imm)
    {
        var unary = funct3 == 1 ? ZbbUnaryImm(bytes) : null;
        var mnemonic = funct3 switch
        {
            0 => "addi",
            2 => "slti",
            3 => "sltiu",
            4 => "xori",
            6 => "ori",
            7 => "andi",
            1 => unary ?? "slli",
            5 => funct7 == 0x20 ? "srai" : funct7 == 0x30 ? "rori" : "srli",
            _ => null
        };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, unary is null
                ? [Reg(rd), Reg(rs1), Imm(mnemonic is "slli" or "srli" or "srai" or "rori" ? imm & 0x3F : imm)]
                : [Reg(rd), Reg(rs1)]);
    }

    private static NativeInstruction OpImm32(ulong address, byte[] bytes, int funct3, int funct7, int rd, int rs1, long imm)
    {
        var unary = funct3 == 1 ? ZbbUnaryImm32(bytes) : null;
        var mnemonic = funct3 switch
        {
            0 => "addiw",
            1 => unary ?? "slliw",
            5 => funct7 == 0x20 ? "sraiw" : funct7 == 0x30 ? "roriw" : "srliw",
            _ => null
        };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, unary is null ? [Reg(rd), Reg(rs1), Imm(imm & 0x1F)] : [Reg(rd), Reg(rs1)]);
    }

    private static NativeInstruction Op(ulong address, byte[] bytes, int funct3, int funct7, int rd, int rs1, int rs2)
    {
        var mnemonic = (funct7, funct3) switch
        {
            (0x00, 0) => "add",
            (0x20, 0) => "sub",
            (0x00, 1) => "sll",
            (0x00, 2) => "slt",
            (0x00, 3) => "sltu",
            (0x00, 4) => "xor",
            (0x00, 5) => "srl",
            (0x20, 5) => "sra",
            (0x00, 6) => "or",
            (0x00, 7) => "and",
            (0x01, 0) => "mul",
            (0x01, 4) => "div",
            (0x01, 5) => "divu",
            (0x01, 6) => "rem",
            (0x01, 7) => "remu",
            (0x20, 4) => "xnor",
            (0x20, 6) => "orn",
            (0x20, 7) => "andn",
            (0x05, 1) => "clmul",
            (0x05, 2) => "clmulr",
            (0x05, 3) => "clmulh",
            (0x05, 4) => "min",
            (0x05, 5) => "minu",
            (0x05, 6) => "max",
            (0x05, 7) => "maxu",
            (0x10, 2) => "sh1add",
            (0x10, 4) => "sh2add",
            (0x10, 6) => "sh3add",
            _ => null
        };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [Reg(rd), Reg(rs1), Reg(rs2)]);
    }

    private static NativeInstruction Op32(ulong address, byte[] bytes, int funct3, int funct7, int rd, int rs1, int rs2)
    {
        var mnemonic = (funct7, funct3) switch
        {
            (0x00, 0) => "addw",
            (0x20, 0) => "subw",
            (0x00, 1) => "sllw",
            (0x00, 5) => "srlw",
            (0x20, 5) => "sraw",
            (0x01, 0) => "mulw",
            (0x01, 4) => "divw",
            (0x01, 5) => "divuw",
            (0x01, 6) => "remw",
            (0x01, 7) => "remuw",
            (0x10, 2) => "sh1add.uw",
            (0x10, 4) => "sh2add.uw",
            (0x10, 6) => "sh3add.uw",
            _ => null
        };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [Reg(rd), Reg(rs1), Reg(rs2)]);
    }

    private static string? ZbbUnaryImm(byte[] bytes)
    {
        var word = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var imm12 = (word >> 20) & 0xFFF;
        return imm12 switch
        {
            0x600 => "clz",
            0x601 => "ctz",
            0x602 => "cpop",
            0x604 => "sext.b",
            0x605 => "sext.h",
            _ => null,
        };
    }

    private static string? ZbbUnaryImm32(byte[] bytes)
    {
        var word = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var imm12 = (word >> 20) & 0xFFF;
        return imm12 switch
        {
            0x600 => "clzw",
            0x601 => "ctzw",
            0x602 => "cpopw",
            _ => null,
        };
    }

    private static NativeInstruction Atomic(ulong address, byte[] bytes, uint word, int funct3, int rd, int rs1, int rs2)
    {
        var width = funct3 switch { 2 => ".w", 3 => ".d", _ => null };
        if (width is null)
            return NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{word:x8}");

        var funct5 = (word >> 27) & 0x1F;
        var mnemonic = funct5 switch
        {
            0x00 => "amoadd",
            0x01 => "amoswap",
            0x02 => "lr",
            0x03 => "sc",
            0x04 => "amoxor",
            0x08 => "amoor",
            0x0C => "amoand",
            0x10 => "amomin",
            0x14 => "amomax",
            0x18 => "amominu",
            0x1C => "amomaxu",
            _ => null,
        };
        if (mnemonic is null)
            return NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{word:x8}");

        mnemonic += width;
        return funct5 == 0x02
            ? Build(address, bytes, mnemonic, [Reg(rd), Mem($"({Registers[rs1]})", Registers[rs1], 0)])
            : Build(address, bytes, mnemonic, [Reg(rd), Reg(rs2), Mem($"({Registers[rs1]})", Registers[rs1], 0)]);
    }

    private static NativeInstruction System(ulong address, byte[] bytes, uint word, int funct3, int rd, int rs1)
    {
        if (word == 0x00000073) return Build(address, bytes, "ecall", [], NativeInstructionCategory.System);
        if (word == 0x00100073) return Build(address, bytes, "ebreak", [], NativeInstructionCategory.System);
        var mnemonic = funct3 switch { 1 => "csrrw", 2 => "csrrs", 3 => "csrrc", 5 => "csrrwi", 6 => "csrrsi", 7 => "csrrci", _ => null };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{word:x8}")
            : Build(address, bytes, mnemonic, [Reg(rd), Imm((word >> 20) & 0xFFF), Reg(rs1)], NativeInstructionCategory.System);
    }

    private static NativeInstruction FLoad(ulong address, byte[] bytes, int funct3, int rd, int rs1, long imm)
    {
        var mnemonic = funct3 switch { 2 => "flw", 3 => "fld", _ => null };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [FReg(rd), Mem($"{imm}({Registers[rs1]})", Registers[rs1], imm)], NativeInstructionCategory.Float);
    }

    private static NativeInstruction FStore(ulong address, byte[] bytes, int funct3, int rs1, int rs2, long imm)
    {
        var mnemonic = funct3 switch { 2 => "fsw", 3 => "fsd", _ => null };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8}")
            : Build(address, bytes, mnemonic, [FReg(rs2), Mem($"{imm}({Registers[rs1]})", Registers[rs1], imm)], NativeInstructionCategory.Float);
    }

    private static NativeInstruction FpOp(ulong address, byte[] bytes, uint word, int rd, int rs1, int rs2)
    {
        var funct7 = (word >> 25) & 0x7F;
        var mnemonic = funct7 switch
        {
            0x00 => "fadd.s",
            0x01 => "fadd.d",
            0x04 => "fsub.s",
            0x05 => "fsub.d",
            0x08 => "fmul.s",
            0x09 => "fmul.d",
            0x0C => "fdiv.s",
            0x0D => "fdiv.d",
            0x2C => "fsqrt.s",
            0x2D => "fsqrt.d",
            _ => null
        };
        return mnemonic is null
            ? NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{word:x8}")
            : Build(address, bytes, mnemonic, [FReg(rd), FReg(rs1), FReg(rs2)], NativeInstructionCategory.Float);
    }

    private static long IImm(uint w) => NativeDecoderSupport.SignExtend(w >> 20, 12);
    private static long SImm(uint w) => NativeDecoderSupport.SignExtend(((w >> 7) & 0x1F) | (((ulong)w >> 25) << 5), 12);
    private static long BImm(uint w) => NativeDecoderSupport.SignExtend(
        (((ulong)w >> 31) << 12) | (((ulong)w >> 7 & 1) << 11) | (((ulong)w >> 25 & 0x3F) << 5) | (((ulong)w >> 8 & 0xF) << 1), 13);
    private static long JImm(uint w) => NativeDecoderSupport.SignExtend(
        (((ulong)w >> 31) << 20) | (((ulong)w >> 12 & 0xFF) << 12) | (((ulong)w >> 20 & 1) << 11) | (((ulong)w >> 21 & 0x3FF) << 1), 21);

    private static long CImm6(ushort h) => NativeDecoderSupport.SignExtend((ulong)(((h >> 2) & 0x1F) | ((h >> 7) & 0x20)), 6);
    private static long CJImm(ushort h) => NativeDecoderSupport.SignExtend(
        (ulong)(((h >> 12) & 1) << 11 | ((h >> 11) & 1) << 4 | ((h >> 9) & 0x3) << 8 | ((h >> 8) & 1) << 10 |
                ((h >> 7) & 1) << 6 | ((h >> 6) & 1) << 7 | ((h >> 3) & 0x7) << 1 | ((h >> 2) & 1) << 5), 12);
    private static long CBImm(ushort h) => NativeDecoderSupport.SignExtend(
        (ulong)(((h >> 12) & 1) << 8 | ((h >> 10) & 0x3) << 3 | ((h >> 5) & 0x3) << 6 |
                ((h >> 3) & 0x3) << 1 | ((h >> 2) & 1) << 5), 9);
    private static int CLwspImm(ushort h) => ((h >> 4) & 0x7) << 2 | ((h >> 12) & 1) << 5 | ((h >> 2) & 0x3) << 6;
    private static int CLdspImm(ushort h) => ((h >> 5) & 0x3) << 3 | ((h >> 12) & 1) << 5 | ((h >> 2) & 0x7) << 6;
    private static int CSwspImm(ushort h) => ((h >> 9) & 0xF) << 2 | ((h >> 7) & 0x3) << 6;
    private static int CSdspImm(ushort h) => ((h >> 10) & 0x7) << 3 | ((h >> 7) & 0x7) << 6;
    private static int CAddi4SpnImm(ushort h) => ((h >> 7) & 0xF) << 6 | ((h >> 11) & 0x3) << 4 | ((h >> 5) & 1) << 3 | ((h >> 6) & 1) << 2;
    private static int CLwImm(ushort h) => ((h >> 10) & 0x7) << 3 | ((h >> 6) & 1) << 2 | ((h >> 5) & 1) << 6;
    private static int CLdImm(ushort h) => ((h >> 10) & 0x7) << 3 | ((h >> 5) & 0x3) << 6;

    private static NativeOperand Reg(int index) => NativeDecoderSupport.Reg(Registers[index]);
    private static NativeOperand FReg(int index) => NativeDecoderSupport.Reg(FRegisters[index]);
    private static NativeOperand Imm(long value) => NativeDecoderSupport.Imm(value);
    private static NativeOperand Mem(string text, string? @base, long disp) => NativeDecoderSupport.Mem(text, @base, disp);

    private static NativeInstruction Build(
        ulong address,
        byte[] bytes,
        string mnemonic,
        IReadOnlyList<NativeOperand> operands,
        NativeInstructionCategory category = NativeInstructionCategory.Integer,
        NativeFlowKind flow = NativeFlowKind.Sequential,
        ulong? target = null) =>
        NativeDecoderSupport.Build(
            address, bytes, mnemonic, operands, category, flow, target,
            target is null ? NativeTargetKind.None : NativeTargetKind.Function);
}
