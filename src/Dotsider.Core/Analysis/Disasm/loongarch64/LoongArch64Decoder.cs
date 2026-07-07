using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm.loongarch64;

/// <summary>
/// Decodes LoongArch64 instructions emitted by the .NET backend.
/// The decoder uses explicit opcode rows for the LoongArch64 forms dotsider renders.
/// Unknown words fall back with exact length so listings do not desynchronize.
/// </summary>
internal static class LoongArch64Decoder
{
    private static readonly Row[] Rows =
    [
        new("nop", 0xffffffff, 0x03400000, Format.Alias),
        new("mov", 0xfffffc00, 0x03800000, Format.Alias2R),
        new("add.w", 0xffff8000, 0x00100000, Format.Rrr),
        new("add.d", 0xffff8000, 0x00108000, Format.Rrr),
        new("sub.w", 0xffff8000, 0x00110000, Format.Rrr),
        new("sub.d", 0xffff8000, 0x00118000, Format.Rrr),
        new("slt", 0xffff8000, 0x00120000, Format.Rrr),
        new("sltu", 0xffff8000, 0x00128000, Format.Rrr),
        new("nor", 0xffff8000, 0x00140000, Format.Rrr),
        new("and", 0xffff8000, 0x00148000, Format.Rrr),
        new("or", 0xffff8000, 0x00150000, Format.Rrr),
        new("xor", 0xffff8000, 0x00158000, Format.Rrr),
        new("mul.w", 0xffff8000, 0x001c0000, Format.Rrr),
        new("mul.d", 0xffff8000, 0x001d8000, Format.Rrr),
        new("div.w", 0xffff8000, 0x00200000, Format.Rrr),
        new("mod.w", 0xffff8000, 0x00208000, Format.Rrr),
        new("div.d", 0xffff8000, 0x00220000, Format.Rrr),
        new("mod.d", 0xffff8000, 0x00228000, Format.Rrr),
        new("sll.w", 0xffff8000, 0x00170000, Format.Rrr),
        new("srl.w", 0xffff8000, 0x00178000, Format.Rrr),
        new("sra.w", 0xffff8000, 0x00180000, Format.Rrr),
        new("sll.d", 0xffff8000, 0x00188000, Format.Rrr),
        new("srl.d", 0xffff8000, 0x00190000, Format.Rrr),
        new("sra.d", 0xffff8000, 0x00198000, Format.Rrr),
        new("addi.w", 0xffc00000, 0x02800000, Format.RrI12),
        new("addi.d", 0xffc00000, 0x02c00000, Format.RrI12),
        new("ld.b", 0xffc00000, 0x28000000, Format.LoadI12),
        new("ld.h", 0xffc00000, 0x28400000, Format.LoadI12),
        new("ld.w", 0xffc00000, 0x28800000, Format.LoadI12),
        new("ld.d", 0xffc00000, 0x28c00000, Format.LoadI12),
        new("st.b", 0xffc00000, 0x29000000, Format.StoreI12),
        new("st.h", 0xffc00000, 0x29400000, Format.StoreI12),
        new("ld.bu", 0xffc00000, 0x2a000000, Format.LoadI12),
        new("ld.hu", 0xffc00000, 0x2a400000, Format.LoadI12),
        new("ld.wu", 0xffc00000, 0x2a800000, Format.LoadI12),
        new("st.w", 0xffc00000, 0x29800000, Format.StoreI12),
        new("st.d", 0xffc00000, 0x29c00000, Format.StoreI12),
        new("ldptr.w", 0xff000000, 0x24000000, Format.LoadI14),
        new("ldptr.d", 0xff000000, 0x26000000, Format.LoadI14),
        new("stptr.w", 0xff000000, 0x25000000, Format.StoreI14),
        new("stptr.d", 0xff000000, 0x27000000, Format.StoreI14),
        new("lu12i.w", 0xfe000000, 0x14000000, Format.RI20),
        new("lu32i.d", 0xfe000000, 0x16000000, Format.RI20),
        new("pcaddi", 0xfe000000, 0x18000000, Format.PcI20),
        new("pcalau12i", 0xfe000000, 0x1a000000, Format.PcI20Shift12),
        new("pcaddu12i", 0xfe000000, 0x1c000000, Format.PcI20Shift12),
        new("pcaddu18i", 0xfe000000, 0x1e000000, Format.PcI20Shift18),
        new("beq", 0xfc000000, 0x58000000, Format.Br2),
        new("bne", 0xfc000000, 0x5c000000, Format.Br2),
        new("blt", 0xfc000000, 0x60000000, Format.Br2),
        new("bge", 0xfc000000, 0x64000000, Format.Br2),
        new("bltu", 0xfc000000, 0x68000000, Format.Br2),
        new("bgeu", 0xfc000000, 0x6c000000, Format.Br2),
        new("beqz", 0xfc000000, 0x40000000, Format.Br1),
        new("bnez", 0xfc000000, 0x44000000, Format.Br1),
        new("bceqz", 0xfc000300, 0x48000000, Format.BrCsr),
        new("bcnez", 0xfc000300, 0x48000100, Format.BrCsr),
        new("jirl", 0xfc000000, 0x4c000000, Format.Jirl),
        new("b", 0xfc000000, 0x50000000, Format.Br0),
        new("bl", 0xfc000000, 0x54000000, Format.Br0Call),
        new("fld.s", 0xffc00000, 0x2b000000, Format.FLoadI12),
        new("fld.d", 0xffc00000, 0x2b800000, Format.FLoadI12),
        new("fst.s", 0xffc00000, 0x2b400000, Format.FStoreI12),
        new("fst.d", 0xffc00000, 0x2bc00000, Format.FStoreI12),
    ];

    /// <summary>
    /// Decodes one LoongArch64 instruction beginning at the requested byte offset.
    /// LoongArch64 instructions are fixed-width four-byte words.
    /// The returned model carries structured operands for CLI, MCP, and TUI consumers.
    /// </summary>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        if (start + 4 > code.Length)
            return NativeDecoderSupport.FallbackByte(code, start, address);

        var word = BinaryPrimitives.ReadUInt32LittleEndian(code[start..]);
        var bytes = code.Slice(start, 4).ToArray();
        foreach (var row in Rows)
        {
            if ((word & row.Mask) == row.Match)
                return FormatRow(row, word, bytes, address);
        }

        return NativeDecoderSupport.FallbackWord(code, start, address);
    }

    private static NativeInstruction FormatRow(Row row, uint word, byte[] bytes, ulong address)
    {
        var rd = (int)(word & 0x1F);
        var rj = (int)((word >> 5) & 0x1F);
        var rk = (int)((word >> 10) & 0x1F);
        return row.Format switch
        {
            Format.Alias => Build(address, bytes, row.Name, []),
            Format.Alias2R => Build(address, bytes, row.Name, [Reg(rd), Reg(rj)]),
            Format.Rrr => Build(address, bytes, row.Name, [Reg(rd), Reg(rj), Reg(rk)]),
            Format.RrI12 => Build(address, bytes, row.Name, [Reg(rd), Reg(rj), Imm(I12(word))]),
            Format.LoadI12 => Build(address, bytes, row.Name, [Reg(rd), Mem(I12(word), rj)]),
            Format.StoreI12 => Build(address, bytes, row.Name, [Reg(rd), Mem(I12(word), rj)]),
            Format.LoadI14 => Build(address, bytes, row.Name, [Reg(rd), Mem(I14(word) << 2, rj)]),
            Format.StoreI14 => Build(address, bytes, row.Name, [Reg(rd), Mem(I14(word) << 2, rj)]),
            Format.RI20 => Build(address, bytes, row.Name, [Reg(rd), Imm(I20(word))]),
            Format.PcI20 => Pc(address, bytes, row.Name, rd, I20(word) << 2),
            Format.PcI20Shift12 => Pc(address, bytes, row.Name, rd, I20(word) << 12),
            Format.PcI20Shift18 => Pc(address, bytes, row.Name, rd, I20(word) << 18),
            Format.Br2 => Branch(address, bytes, row.Name, [Reg(rj), Reg(rd)], Branch16(word), NativeFlowKind.ConditionalBranch),
            Format.Br1 => Branch(address, bytes, row.Name, [Reg(rj)], Branch21(word), NativeFlowKind.ConditionalBranch),
            Format.BrCsr => Branch(address, bytes, row.Name, [Reg(((int)(word >> 5) & 0x7) + 0)], Branch21(word), NativeFlowKind.ConditionalBranch),
            Format.Br0 => Branch(address, bytes, row.Name, [], Branch26(word), NativeFlowKind.Jump),
            Format.Br0Call => Branch(address, bytes, row.Name, [], Branch26(word), NativeFlowKind.Call),
            Format.Jirl => Jirl(address, bytes, row.Name, rd, rj, Branch16(word)),
            Format.FLoadI12 => Build(address, bytes, row.Name, [FReg(rd), Mem(I12(word), rj)], NativeInstructionCategory.Float),
            Format.FStoreI12 => Build(address, bytes, row.Name, [FReg(rd), Mem(I12(word), rj)], NativeInstructionCategory.Float),
            _ => NativeDecoderSupport.Fallback(address, bytes, ".word", $"0x{word:x8}"),
        };
    }

    private static NativeInstruction Pc(ulong address, byte[] bytes, string name, int rd, long disp)
    {
        var target = (ulong)((long)address + disp);
        return Build(address, bytes, name, [Reg(rd), Imm(disp)], target: target, targetKind: NativeTargetKind.Data);
    }

    private static NativeInstruction Branch(
        ulong address, byte[] bytes, string name, IReadOnlyList<NativeOperand> prefix, long disp, NativeFlowKind flow)
    {
        var target = (ulong)((long)address + disp);
        List<NativeOperand> ops = [.. prefix, NativeDecoderSupport.Target(target)];
        return Build(address, bytes, name, ops, NativeInstructionCategory.Control, flow, target);
    }

    private static NativeInstruction Jirl(ulong address, byte[] bytes, string name, int rd, int rj, long disp)
    {
        var flow = rd == 0 && rj == 1 && disp == 0 ? NativeFlowKind.Return
            : rd == 1 ? NativeFlowKind.IndirectCall : NativeFlowKind.IndirectJump;
        return Build(address, bytes, name, [Reg(rd), Reg(rj), Imm(disp)], NativeInstructionCategory.Control, flow);
    }

    private static long I12(uint word) => NativeDecoderSupport.SignExtend((word >> 10) & 0xFFF, 12);
    private static long I14(uint word) => NativeDecoderSupport.SignExtend((word >> 10) & 0x3FFF, 14);
    private static long I20(uint word) => NativeDecoderSupport.SignExtend((word >> 5) & 0xFFFFF, 20);
    private static long Branch16(uint word) => NativeDecoderSupport.SignExtend((word >> 10) & 0xFFFF, 16) << 2;
    private static long Branch21(uint word) => NativeDecoderSupport.SignExtend(((word & 0x1F) << 16) | ((word >> 10) & 0xFFFF), 21) << 2;
    private static long Branch26(uint word) => NativeDecoderSupport.SignExtend(((word & 0x3FF) << 16) | ((word >> 10) & 0xFFFF), 26) << 2;

    private static NativeOperand Reg(int index) => NativeDecoderSupport.Reg(index switch
    {
        0 => "zero",
        1 => "ra",
        2 => "tp",
        3 => "sp",
        22 => "fp",
        _ => $"r{index}",
    });

    private static NativeOperand FReg(int index) => NativeDecoderSupport.Reg($"f{index}");

    private static NativeOperand Imm(long value) => NativeDecoderSupport.Imm(value);

    private static NativeOperand Mem(long displacement, int baseReg) =>
        NativeDecoderSupport.Mem($"{displacement}({RegName(baseReg)})", RegName(baseReg), displacement);

    private static string RegName(int index) => index switch
    {
        0 => "zero",
        1 => "ra",
        2 => "tp",
        3 => "sp",
        22 => "fp",
        _ => $"r{index}",
    };

    private static NativeInstruction Build(
        ulong address,
        byte[] bytes,
        string mnemonic,
        IReadOnlyList<NativeOperand> operands,
        NativeInstructionCategory category = NativeInstructionCategory.Integer,
        NativeFlowKind flow = NativeFlowKind.Sequential,
        ulong? target = null,
        NativeTargetKind targetKind = NativeTargetKind.Function) =>
        NativeDecoderSupport.Build(
            address, bytes, mnemonic, operands, category, flow, target,
            target is null ? NativeTargetKind.None : targetKind);

    private readonly record struct Row(string Name, uint Mask, uint Match, Format Format);

    private enum Format
    {
        Alias,
        Alias2R,
        Rrr,
        RrI12,
        LoadI12,
        StoreI12,
        LoadI14,
        StoreI14,
        RI20,
        PcI20,
        PcI20Shift12,
        PcI20Shift18,
        Br2,
        Br1,
        BrCsr,
        Br0,
        Br0Call,
        Jirl,
        FLoadI12,
        FStoreI12,
    }
}
