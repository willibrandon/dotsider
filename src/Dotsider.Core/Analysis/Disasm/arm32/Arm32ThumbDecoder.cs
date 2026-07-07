using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm.arm32;

/// <summary>
/// Decodes ARM32 Thumb and Thumb-2 instructions used by .NET images.
/// ReadyToRun and Native AOT code on ARM32 is expected to enter through this Thumb decoder.
/// Unknown halfwords or words fall back with exact length so listings do not desynchronize.
/// </summary>
internal static class Arm32ThumbDecoder
{
    private static readonly string[] LowRegs = ["r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7"];
    private static readonly string[] Cond =
    [
        "eq", "ne", "cs", "cc", "mi", "pl", "vs", "vc",
        "hi", "ls", "ge", "lt", "gt", "le", "", ""
    ];

    /// <summary>
    /// Decodes one Thumb or Thumb-2 instruction beginning at the requested byte offset.
    /// The method chooses the 16-bit or 32-bit decoder from the first halfword.
    /// The returned model carries structured operands for CLI, MCP, and TUI consumers.
    /// </summary>
    /// <param name="code">The code window.</param>
    /// <param name="start">The byte offset within <paramref name="code"/>.</param>
    /// <param name="address">The virtual address of the instruction's first byte.</param>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        if (start + 2 > code.Length)
            return NativeDecoderSupport.FallbackByte(code, start, address);

        var h = BinaryPrimitives.ReadUInt16LittleEndian(code[start..]);
        if (IsThumb32(h))
            return Decode32(code, start, address, h);

        return Decode16(code, start, address, h);
    }

    private static NativeInstruction Decode16(ReadOnlySpan<byte> code, int start, ulong address, ushort h)
    {
        var bytes = code.Slice(start, 2).ToArray();
        var op5 = h >> 11;

        if (h == 0xBF00)
            return Build(address, bytes, "nop", [], NativeInstructionCategory.System);
        if ((h & 0xFF00) == 0xDE00)
            return Build(address, bytes, "udf", [Imm(h & 0xFF)], NativeInstructionCategory.System);
        if ((h & 0xFF00) == 0xDF00)
            return Build(address, bytes, "svc", [Imm(h & 0xFF)], NativeInstructionCategory.System);
        if ((h & 0xFF87) == 0x4700)
        {
            var rm = ((h >> 3) & 0xF);
            var flow = rm == 14 ? NativeFlowKind.Return : NativeFlowKind.IndirectJump;
            return Build(address, bytes, "bx", [Reg(rm)], NativeInstructionCategory.Control, flow);
        }
        if ((h & 0xFF87) == 0x4780)
            return Build(address, bytes, "blx", [Reg((h >> 3) & 0xF)], NativeInstructionCategory.Control, NativeFlowKind.IndirectCall);
        if ((h & 0xFE00) == 0xBC00)
        {
            var hasPc = (h & 0x0100) != 0;
            return Build(address, bytes, "pop", [RegList(h & 0xFF, hasPc ? "pc" : null)],
                NativeInstructionCategory.Control, hasPc ? NativeFlowKind.Return : NativeFlowKind.Sequential);
        }
        if ((h & 0xFE00) == 0xB400)
            return Build(address, bytes, "push", [RegList(h & 0xFF, (h & 0x0100) != 0 ? "lr" : null)]);
        if ((h & 0xF800) == 0x9000)
            return Build(address, bytes, "str", [Reg(h >> 8 & 7), Mem($"[sp, #0x{(h & 0xFF) << 2:x}]")]);
        if ((h & 0xF800) == 0x9800)
            return Build(address, bytes, "ldr", [Reg(h >> 8 & 7), Mem($"[sp, #0x{(h & 0xFF) << 2:x}]")]);
        if ((h & 0xF800) == 0xA800)
            return Build(address, bytes, "add", [Reg(h >> 8 & 7), Reg("sp"), Imm((h & 0xFF) << 2)]);
        if ((h & 0xF800) == 0xC000)
            return Build(address, bytes, "stmia", [Reg($"{RegName((h >> 8) & 7)}!"), RegList(h & 0xFF, null)]);
        if ((h & 0xFF00) == 0x4400)
        {
            var rd = (h & 7) | ((h >> 4) & 8);
            var rm = (h >> 3) & 0xF;
            return Build(address, bytes, "add", [Reg(rd), Reg(rm)]);
        }
        if ((h & 0xF800) == 0x2000)
            return Build(address, bytes, "movs", [Reg(h >> 8 & 7), Imm(h & 0xFF)]);
        if ((h & 0xF800) == 0x2800)
            return Build(address, bytes, "cmp", [Reg(h >> 8 & 7), Imm(h & 0xFF)]);
        if ((h & 0xF800) == 0x3000)
            return Build(address, bytes, "adds", [Reg(h >> 8 & 7), Imm(h & 0xFF)]);
        if ((h & 0xF800) == 0x3800)
            return Build(address, bytes, "subs", [Reg(h >> 8 & 7), Imm(h & 0xFF)]);
        if ((h & 0xF800) == 0x4800)
        {
            var rt = (h >> 8) & 7;
            var target = ((address + 4) & ~3UL) + (ulong)((h & 0xFF) << 2);
            return Build(address, bytes, "ldr", [Reg(rt), Mem($"[pc, #0x{(h & 0xFF) << 2:x}]")],
                target: target, targetKind: NativeTargetKind.Data);
        }
        if ((h & 0xF800) == 0x6000)
            return LoadStoreImm(bytes, address, h, "str", 2);
        if ((h & 0xF800) == 0x6800)
            return LoadStoreImm(bytes, address, h, "ldr", 2);
        if ((h & 0xF800) == 0x7000)
            return LoadStoreImm(bytes, address, h, "strb", 0);
        if ((h & 0xF800) == 0x7800)
            return LoadStoreImm(bytes, address, h, "ldrb", 0);
        if ((h & 0xFF00) is >= 0xD000 and <= 0xDD00)
        {
            var cond = (h >> 8) & 0xF;
            var disp = NativeDecoderSupport.SignExtend((ulong)(h & 0xFF), 8) << 1;
            var target = (ulong)((long)address + 4 + disp);
            return Build(address, bytes, $"b{Cond[cond]}", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.ConditionalBranch, target);
        }
        if ((h & 0xF800) == 0xE000)
        {
            var disp = NativeDecoderSupport.SignExtend((ulong)(h & 0x7FF), 11) << 1;
            var target = (ulong)((long)address + 4 + disp);
            return Build(address, bytes, "b", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.Jump, target);
        }
        if ((h & 0xFF00) == 0xB000)
        {
            var imm = (h & 0x7F) << 2;
            var mnemonic = (h & 0x80) == 0 ? "add" : "sub";
            return Build(address, bytes, mnemonic, [Reg("sp"), Imm(imm)]);
        }
        if ((h & 0xFFC0) == 0x4600)
        {
            var rd = (h & 7) | ((h >> 4) & 8);
            var rm = (h >> 3) & 0xF;
            return Build(address, bytes, "mov", [Reg(rd), Reg(rm)]);
        }
        if (op5 is 0 or 1 or 2 or 3)
        {
            var imm = (h >> 6) & 0x1F;
            var rm = (h >> 3) & 7;
            var rd = h & 7;
            var mnem = op5 switch { 0 => "lsls", 1 => "lsrs", 2 => "asrs", _ => "adds" };
            return Build(address, bytes, mnem, [Reg(rd), Reg(rm), Imm(imm)]);
        }

        return NativeDecoderSupport.FallbackHalf(code, start, address);
    }

    private static NativeInstruction Decode32(ReadOnlySpan<byte> code, int start, ulong address, ushort hi)
    {
        if (start + 4 > code.Length)
            return NativeDecoderSupport.FallbackHalf(code, start, address);

        var lo = BinaryPrimitives.ReadUInt16LittleEndian(code[(start + 2)..]);
        var bytes = code.Slice(start, 4).ToArray();

        if (hi == 0xE92D)
            return Build(address, bytes, "push.w", [RegList(lo, null)]);

        if (hi == 0xE8BD)
        {
            var hasPc = (lo & (1 << 15)) != 0;
            return Build(address, bytes, "pop.w", [RegList(lo, null)],
                NativeInstructionCategory.Control, hasPc ? NativeFlowKind.Return : NativeFlowKind.Sequential);
        }

        if ((hi & 0xF800) == 0xF000 && (lo & 0xD000) == 0xD000)
        {
            var s = (hi >> 10) & 1;
            var j1 = (lo >> 13) & 1;
            var j2 = (lo >> 11) & 1;
            var i1 = (~(j1 ^ s)) & 1;
            var i2 = (~(j2 ^ s)) & 1;
            var imm10 = hi & 0x03FF;
            var imm11 = lo & 0x07FF;
            var raw = (s << 24) | (i1 << 23) | (i2 << 22) | (imm10 << 12) | (imm11 << 1);
            var disp = NativeDecoderSupport.SignExtend((ulong)raw, 25);
            var target = (ulong)((long)address + 4 + disp);
            return Build(address, bytes, "bl", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.Call, target);
        }

        if ((hi & 0xF800) == 0xF000 && (lo & 0xD000) == 0x8000)
        {
            var cond = (hi >> 6) & 0xF;
            var s = (hi >> 10) & 1;
            var j1 = (lo >> 13) & 1;
            var j2 = (lo >> 11) & 1;
            var i1 = (~(j1 ^ s)) & 1;
            var i2 = (~(j2 ^ s)) & 1;
            var imm6 = hi & 0x003F;
            var imm11 = lo & 0x07FF;
            var raw = (s << 20) | (i1 << 19) | (i2 << 18) | (imm6 << 12) | (imm11 << 1);
            var disp = NativeDecoderSupport.SignExtend((ulong)raw, 21);
            var target = (ulong)((long)address + 4 + disp);
            return Build(address, bytes, $"b{Cond[cond]}.w", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.ConditionalBranch, target);
        }

        if ((hi & 0xF800) == 0xF000 && (lo & 0x9000) == 0x9000)
        {
            var s = (hi >> 10) & 1;
            var j1 = (lo >> 13) & 1;
            var j2 = (lo >> 11) & 1;
            var imm10 = hi & 0x03FF;
            var imm11 = lo & 0x07FF;
            var raw = (s << 20) | (j2 << 19) | (j1 << 18) | (imm10 << 8) | (imm11 << 1);
            var disp = NativeDecoderSupport.SignExtend((ulong)raw, 21);
            var target = (ulong)((long)address + 4 + disp);
            return Build(address, bytes, "b.w", [NativeDecoderSupport.Target(target)],
                NativeInstructionCategory.Control, NativeFlowKind.Jump, target);
        }

        if ((hi & 0xFBF0) == 0xF240)
        {
            var rd = (lo >> 8) & 0xF;
            var imm = ((hi & 0x0400) << 1) | ((hi & 0x000F) << 12) | ((lo & 0x7000) >> 4) | (lo & 0x00FF);
            return Build(address, bytes, "movw", [Reg(rd), Imm(imm)]);
        }

        if ((hi & 0xFBF0) == 0xF2C0)
        {
            var rd = (lo >> 8) & 0xF;
            var imm = ((hi & 0x0400) << 1) | ((hi & 0x000F) << 12) | ((lo & 0x7000) >> 4) | (lo & 0x00FF);
            return Build(address, bytes, "movt", [Reg(rd), Imm(imm)]);
        }

        if ((hi & 0xFBFF) == 0xF04F)
        {
            var rd = (lo >> 8) & 0xF;
            var imm = DecodeModifiedImmediate(hi, lo);
            return Build(address, bytes, "mov.w", [Reg(rd), Imm(imm)]);
        }

        if ((hi & 0xFBFF) == 0xF06F)
        {
            var rd = (lo >> 8) & 0xF;
            var imm = DecodeModifiedImmediate(hi, lo);
            return Build(address, bytes, "mvn", [Reg(rd), Imm(imm)]);
        }

        if ((hi & 0xFBF0) == 0xF100)
        {
            var rn = hi & 0xF;
            var rd = (lo >> 8) & 0xF;
            var imm = ((hi & 0x0400) << 1) | ((lo & 0x7000) >> 4) | (lo & 0x00FF);
            return Build(address, bytes, "add.w", [Reg(rd), Reg(rn), Imm(imm)]);
        }

        if ((hi & 0xFBF0) == 0xF1A0)
        {
            var rn = hi & 0xF;
            var rd = (lo >> 8) & 0xF;
            var imm = ((hi & 0x0400) << 1) | ((lo & 0x7000) >> 4) | (lo & 0x00FF);
            return Build(address, bytes, "sub.w", [Reg(rd), Reg(rn), Imm(imm)]);
        }

        if ((hi & 0xFFF0) == 0xF8D0)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x0FFF;
            return Build(address, bytes, "ldr.w", [Reg(rt), Mem($"[{RegName(rn)}{(imm == 0 ? "" : $", #0x{imm:x}")}]")]);
        }

        if ((hi & 0xFFF0) == 0xF890)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x0FFF;
            return Build(address, bytes, "ldrb.w", [Reg(rt), Mem($"[{RegName(rn)}{(imm == 0 ? "" : $", #0x{imm:x}")}]")]);
        }

        if ((hi & 0xFFF0) == 0xF990)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x0FFF;
            return Build(address, bytes, "ldrsb.w", [Reg(rt), Mem($"[{RegName(rn)}{(imm == 0 ? "" : $", #0x{imm:x}")}]")]);
        }

        if ((hi & 0xFFF0) == 0xF880)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x0FFF;
            return Build(address, bytes, "strb.w", [Reg(rt), Mem($"[{RegName(rn)}{(imm == 0 ? "" : $", #0x{imm:x}")}]")]);
        }

        if ((hi & 0xFFF0) == 0xF840 && (lo & 0x0F00) is 0x0C00 or 0x0D00)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x00FF;
            var writeback = (lo & 0x0100) != 0 ? "!" : "";
            return Build(address, bytes, "str.w", [Reg(rt), Mem($"[{RegName(rn)}, #-0x{imm:x}]{writeback}")]);
        }

        if ((hi & 0xFFF0) == 0xF850 && (lo & 0x0F00) == 0x0C00)
        {
            var rn = hi & 0xF;
            var rt = (lo >> 12) & 0xF;
            var imm = lo & 0x00FF;
            return Build(address, bytes, "ldr.w", [Reg(rt), Mem($"[{RegName(rn)}, #-0x{imm:x}]")]);
        }

        return NativeDecoderSupport.FallbackWord(code, start, address);
    }

    private static bool IsThumb32(ushort firstHalfword) =>
        (firstHalfword & 0xF800) is 0xE800 or 0xF000 or 0xF800;

    private static NativeInstruction LoadStoreImm(byte[] bytes, ulong address, ushort h, string mnemonic, int scale)
    {
        var imm = ((h >> 6) & 0x1F) << scale;
        var rn = (h >> 3) & 7;
        var rt = h & 7;
        return Build(address, bytes, mnemonic, [Reg(rt), Mem($"[{LowRegs[rn]}, #0x{imm:x}]")]);
    }

    private static NativeOperand Reg(int index) => NativeDecoderSupport.Reg(index switch
    {
        13 => "sp",
        14 => "lr",
        15 => "pc",
        _ => $"r{index}",
    });

    private static string RegName(int index) => index switch
    {
        13 => "sp",
        14 => "lr",
        15 => "pc",
        _ => $"r{index}",
    };

    private static NativeOperand Reg(string name) => NativeDecoderSupport.Reg(name);

    private static NativeOperand Imm(long value) => NativeDecoderSupport.Imm(value, $"#0x{value:x}");

    private static NativeOperand Mem(string text) => NativeDecoderSupport.Mem(text);

    private static NativeOperand RegList(int mask, string? extra)
    {
        var names = new List<string>();
        for (var i = 0; i < 16; i++)
            if ((mask & (1 << i)) != 0)
                names.Add(RegName(i));
        if (extra is not null) names.Add(extra);
        return new NativeOperand(NativeOperandKind.Register, "{" + string.Join(", ", names) + "}");
    }

    private static int DecodeModifiedImmediate(ushort hi, ushort lo)
    {
        var i = (hi >> 10) & 1;
        var imm3 = (lo >> 12) & 7;
        var imm8 = lo & 0xFF;
        var imm12 = (i << 11) | (imm3 << 8) | imm8;
        if ((imm12 & 0xC00) == 0)
        {
            return ((imm12 >> 8) & 3) switch
            {
                0 => imm8,
                1 => (imm8 << 16) | imm8,
                2 => (imm8 << 24) | (imm8 << 8),
                _ => (imm8 << 24) | (imm8 << 16) | (imm8 << 8) | imm8,
            };
        }

        var unrotated = 0x80 | (imm12 & 0x7F);
        var rotation = (imm12 >> 7) & 0x1F;
        return (int)(((uint)unrotated >> rotation) | ((uint)unrotated << (32 - rotation)));
    }

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
}
