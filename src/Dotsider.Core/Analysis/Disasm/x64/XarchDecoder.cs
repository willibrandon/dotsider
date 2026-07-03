using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// The table-driven x86-64 instruction decoder: legacy prefixes → REX → opcode map escape →
/// table lookup (refined by ModRM.reg for groups) → ModRM/SIB/displacement → immediate. Length is
/// the cursor delta and is exact for every recognized encoding because ModRM presence and
/// immediate size are properties of the matched row. A byte that maps to no defined row decodes as
/// a one-byte <c>.byte</c> safety net that resynchronizes at the next byte.
/// </summary>
internal static class XarchDecoder
{
    /// <summary>Decodes one instruction beginning at <paramref name="start"/>.</summary>
    /// <param name="code">The code window.</param>
    /// <param name="start">The byte offset of the instruction within <paramref name="code"/>.</param>
    /// <param name="address">The virtual address of the instruction's first byte.</param>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        var r = new NativeCodeReader(code) { Position = start };
        var p = default(Prefixes);

        // 1. Legacy prefixes, then an optional REX (which must be last before the opcode).
        while (r.HasMore && TryConsumeLegacyPrefix(ref r, ref p)) { }
        if (r.HasMore && r.Peek() is >= 0x40 and <= 0x4F)
        {
            var rex = r.ReadU8();
            p.HasRex = true;
            p.RexW = (rex & 8) != 0;
            p.RexR = (rex & 4) != 0;
            p.RexX = (rex & 2) != 0;
            p.RexB = (rex & 1) != 0;
        }

        if (!r.HasMore) return ByteFallback(code, start, address);

        // 2. Opcode + 0F / 0F 38 / 0F 3A escape.
        var map = XarchTables.MapOneByte;
        int opcode = r.ReadU8();
        if (opcode == 0x0F)
        {
            var op2 = r.ReadU8();
            (map, opcode) = op2 switch
            {
                0x38 => (XarchTables.Map0F38, (int)r.ReadU8()),
                0x3A => (XarchTables.Map0F3A, (int)r.ReadU8()),
                _ => (XarchTables.Map0F, op2),
            };
        }

        var pp = p.Rep ? XarchTables.PpF3 : p.Repne ? XarchTables.PpF2 : p.OpSize ? XarchTables.Pp66 : XarchTables.PpNone;
        var entry = XarchTables.Lookup(map, pp, opcode);
        if (entry.IsEmpty) return ByteFallback(code, start, address);

        // 3. ModRM (+ group re-index), then operands.
        var modrm = (byte)0;
        var hasModRm = entry.HasModRm;
        if (hasModRm) modrm = r.Peek();

        if ((entry.Flags & OpFlags.Group) != 0)
        {
            var group = XarchTables.GroupEntry(entry.GroupOrTuple, (modrm >> 3) & 7);
            if (group.Mnemonic is null) return ByteFallback(code, start, address);
            entry = MergeGroup(entry, group);
        }

        var mnemonic = entry.Mnemonic ?? ".byte";
        var effOp = EffectiveOperandSize(entry, p);

        // Endbr and the x87 escapes need the ModRM/opcode before naming.
        if (map == XarchTables.Map0F && opcode == 0x1E && p.Rep && hasModRm)
        {
            var b = r.Peek();
            if (b is 0xFA or 0xFB)
            {
                r.ReadU8();
                return Build(code, start, address, b == 0xFA ? "endbr64" : "endbr32", [],
                    NativeInstructionCategory.System, NativeFlowKind.Sequential, r.Position);
            }
        }

        if (map == XarchTables.MapOneByte && opcode is >= 0xD8 and <= 0xDF)
            mnemonic = XarchOperandFormatter.X87Mnemonic(opcode, modrm);

        // The 64-bit-immediate mov (B8+r, REX.W) is spelled movabs in this syntax.
        if (map == XarchTables.MapOneByte && opcode is >= 0xB8 and <= 0xBF && effOp == 8)
            mnemonic = "movabs";

        var ctx = new OperandContext(effOp, p, address, start);
        var operands = new List<NativeOperand>(4);
        ulong? target = null;

        // Consume the ModRM byte now (before operands read SIB/disp/imm).
        if (hasModRm) r.ReadU8();

        // x87: a memory form consumes SIB/disp (so length is exact); a register form is st(i).
        if (map == XarchTables.MapOneByte && opcode is >= 0xD8 and <= 0xDF)
        {
            if ((modrm >> 6) == 3)
            {
                var st = XarchRegisters.St(modrm & 7);
                operands.Add(new NativeOperand(NativeOperandKind.Register, st, Register: st));
            }
            else
            {
                int[] sizes = [4, 4, 4, 4, 8, 8, 2, 2];
                operands.Add(Rm(ref r, modrm, ctx, sizes[opcode - 0xD8]));
            }

            return Build(code, start, address, mnemonic, operands, NativeInstructionCategory.Float,
                NativeFlowKind.Sequential, r.Position);
        }

        DecodeOperand(entry.Op1, ref r, modrm, ctx, operands, ref target, opcode);
        DecodeOperand(entry.Op2, ref r, modrm, ctx, operands, ref target, opcode);
        DecodeOperand(entry.Op3, ref r, modrm, ctx, operands, ref target, opcode);
        DecodeOperand(entry.Op4, ref r, modrm, ctx, operands, ref target, opcode);

        mnemonic = ApplyRepPrefix(mnemonic, opcode, p);
        var flow = ClassifyFlow(mnemonic, operands);
        var category = ClassifyCategory(mnemonic, opcode, map);
        return Build(code, start, address, mnemonic, operands, category, flow, r.Position, target);
    }

    private static bool TryConsumeLegacyPrefix(ref NativeCodeReader r, ref Prefixes p)
    {
        switch (r.Peek())
        {
            case 0x66: p.OpSize = true; break;
            case 0x67: p.AddrSize = true; break;
            case 0xF0: p.Lock = true; break;
            case 0xF2: p.Repne = true; break;
            case 0xF3: p.Rep = true; break;
            case 0x2E: case 0x36: case 0x3E: case 0x26: break; // ignored segment overrides in 64-bit
            case 0x64: p.Segment = 4; break;
            case 0x65: p.Segment = 5; break;
            default: return false;
        }

        r.ReadU8();
        return true;
    }

    private static OpEntry MergeGroup(OpEntry primary, OpEntry group)
    {
        var hasOperands = group.Op1 != OperandKind.None;
        return hasOperands
            ? group with { Flags = group.Flags | (primary.Flags & (OpFlags.Default64 | OpFlags.Force64)) }
            : primary with { Mnemonic = group.Mnemonic, Flags = primary.Flags | group.Flags };
    }

    private static int EffectiveOperandSize(OpEntry entry, Prefixes p)
    {
        if ((entry.Flags & OpFlags.Force64) != 0) return 8;
        if ((entry.Flags & OpFlags.Default64) != 0) return p.OpSize ? 2 : 8;
        return p.RexW ? 8 : p.OpSize ? 2 : 4;
    }

    private static void DecodeOperand(
        OperandKind kind, ref NativeCodeReader r, byte modrm, OperandContext ctx,
        List<NativeOperand> operands, ref ulong? target, int opcode)
    {
        switch (kind)
        {
            case OperandKind.None:
                return;

            case OperandKind.Eb: operands.Add(Rm(ref r, modrm, ctx, 1)); break;
            case OperandKind.Ew: operands.Add(Rm(ref r, modrm, ctx, 2)); break;
            case OperandKind.Ed: operands.Add(Rm(ref r, modrm, ctx, 4)); break;
            case OperandKind.Eq: operands.Add(Rm(ref r, modrm, ctx, 8)); break;
            case OperandKind.Ev: operands.Add(Rm(ref r, modrm, ctx, ctx.OpSize)); break;
            case OperandKind.Ey: operands.Add(Rm(ref r, modrm, ctx, 8)); break;
            case OperandKind.M: case OperandKind.Mv: operands.Add(Rm(ref r, modrm, ctx, ctx.OpSize)); break;
            case OperandKind.Wx: operands.Add(Rm(ref r, modrm, ctx, 16, vector: true)); break;
            case OperandKind.Qq: operands.Add(Rm(ref r, modrm, ctx, 8, mmx: true)); break;

            case OperandKind.Gb: operands.Add(Reg(modrm, ctx, 1)); break;
            case OperandKind.Gw: operands.Add(Reg(modrm, ctx, 2)); break;
            case OperandKind.Gd: operands.Add(Reg(modrm, ctx, 4)); break;
            case OperandKind.Gv: operands.Add(Reg(modrm, ctx, ctx.OpSize)); break;
            case OperandKind.Gy: operands.Add(Reg(modrm, ctx, 8)); break;
            case OperandKind.Vx: operands.Add(RegVector(modrm, ctx, 16)); break;
            case OperandKind.Kr: operands.Add(new NativeOperand(NativeOperandKind.Register, XarchRegisters.Mask(RegIndex(modrm, ctx)), Register: XarchRegisters.Mask(RegIndex(modrm, ctx)))); break;
            case OperandKind.Pq: operands.Add(new NativeOperand(NativeOperandKind.Register, XarchRegisters.Mmx((modrm >> 3) & 7), Register: XarchRegisters.Mmx((modrm >> 3) & 7))); break;
            case OperandKind.Sw: operands.Add(new NativeOperand(NativeOperandKind.Register, XarchRegisters.Segment((modrm >> 3) & 7), Register: XarchRegisters.Segment((modrm >> 3) & 7))); break;

            case OperandKind.Ib: operands.Add(ImmSx(ref r, 1, ctx.OpSize)); break;
            case OperandKind.Iw: operands.Add(Imm(ref r, 2)); break;
            case OperandKind.Id: operands.Add(Imm(ref r, 4)); break;
            case OperandKind.Iz: operands.Add(ImmSx(ref r, ctx.OpSize == 2 ? 2 : 4, ctx.OpSize)); break;
            case OperandKind.Iv: operands.Add(Imm(ref r, ctx.OpSize)); break;

            case OperandKind.Jb: operands.Add(Rel(ref r, 1, ctx, ref target)); break;
            case OperandKind.Jz: operands.Add(Rel(ref r, ctx.OpSize == 2 ? 2 : 4, ctx, ref target)); break;

            case OperandKind.Ob: case OperandKind.Ov: operands.Add(Moffs(ref r, ctx, kind == OperandKind.Ob ? 1 : ctx.OpSize)); break;

            case OperandKind.Zb: operands.Add(RegDirect((opcode & 7) + (ctx.Prefixes.RexB ? 8 : 0), ctx, 1)); break;
            case OperandKind.Zv: operands.Add(RegDirect((opcode & 7) + (ctx.Prefixes.RexB ? 8 : 0), ctx, ctx.OpSize)); break;
            case OperandKind.RAX: operands.Add(RegDirect(0, ctx, ctx.OpSize)); break;
            case OperandKind.AL: operands.Add(RegDirect(0, ctx, 1)); break;
            case OperandKind.DX: operands.Add(RegDirect(2, ctx, 2)); break;
            case OperandKind.CL: operands.Add(RegDirect(1, ctx, 1)); break;
            case OperandKind.One: operands.Add(new NativeOperand(NativeOperandKind.Immediate, "1", Immediate: 1)); break;

            default:
                operands.Add(new NativeOperand(NativeOperandKind.Register, "?"));
                break;
        }
    }

    private static int RegIndex(byte modrm, OperandContext ctx) => ((modrm >> 3) & 7) + (ctx.Prefixes.RexR ? 8 : 0);

    private static NativeOperand Reg(byte modrm, OperandContext ctx, int size)
    {
        var name = XarchRegisters.Gpr(RegIndex(modrm, ctx), size, ctx.Prefixes.HasRex);
        return new NativeOperand(NativeOperandKind.Register, name, Register: name);
    }

    private static NativeOperand RegVector(byte modrm, OperandContext ctx, int len)
    {
        var name = XarchRegisters.Vector(RegIndex(modrm, ctx), len);
        return new NativeOperand(NativeOperandKind.Register, name, Register: name);
    }

    private static NativeOperand RegDirect(int index, OperandContext ctx, int size)
    {
        var name = XarchRegisters.Gpr(index, size, ctx.Prefixes.HasRex);
        return new NativeOperand(NativeOperandKind.Register, name, Register: name);
    }

    private static NativeOperand Rm(
        ref NativeCodeReader r, byte modrm, OperandContext ctx, int size, bool vector = false, bool mmx = false)
    {
        var mod = modrm >> 6;
        var rmLow = modrm & 7;
        var rmFull = rmLow + (ctx.Prefixes.RexB ? 8 : 0);

        if (mod == 3)
        {
            var name = vector ? XarchRegisters.Vector(rmFull, size)
                : mmx ? XarchRegisters.Mmx(rmLow)
                : XarchRegisters.Gpr(rmFull, size, ctx.Prefixes.HasRex);
            return new NativeOperand(NativeOperandKind.Register, name, Register: name);
        }

        string? baseReg = null, indexReg = null;
        var scale = 0;
        long disp = 0;
        var ripRel = false;
        var addrSize = ctx.Prefixes.AddrSize ? 4 : 8;

        if (rmLow == 4)
        {
            var sib = r.ReadU8();
            var ss = 1 << (sib >> 6);
            var idx = ((sib >> 3) & 7) + (ctx.Prefixes.RexX ? 8 : 0);
            var bse = (sib & 7) + (ctx.Prefixes.RexB ? 8 : 0);

            if (idx != 4) { indexReg = XarchRegisters.Gpr(idx, addrSize, ctx.Prefixes.HasRex); scale = ss; }
            if ((sib & 7) == 5 && mod == 0)
                disp = r.ReadI32();
            else
                baseReg = XarchRegisters.Gpr(bse, addrSize, ctx.Prefixes.HasRex);
        }
        else if (rmLow == 5 && mod == 0)
        {
            ripRel = true;
            disp = r.ReadI32();
        }
        else
        {
            baseReg = XarchRegisters.Gpr(rmFull, addrSize, ctx.Prefixes.HasRex);
        }

        disp += mod switch { 1 => r.ReadI8(), 2 => r.ReadI32(), _ => 0 };
        var hintSize = vector ? 16 : mmx ? 8 : size;
        return XarchOperandFormatter.Memory(hintSize, baseReg, indexReg, scale, disp, ripRel);
    }

    private static NativeOperand Imm(ref NativeCodeReader r, int size)
    {
        // Unsigned immediate of an exact width (Iw/Id/Iv): shown as read.
        ulong value = size switch { 1 => r.ReadU8(), 2 => r.ReadU16(), 8 => r.ReadU64(), _ => r.ReadU32() };
        return new NativeOperand(NativeOperandKind.Immediate, $"0x{value:x}", Immediate: (long)value);
    }

    /// <summary>
    /// A sign-extending immediate (Ib/Iz): the encoded value is sign-extended to the operand size
    /// and shown as the resulting unsigned width, matching disassembler convention (e.g. an imm8 of
    /// 0xFF on a 64-bit op renders as <c>0xffffffffffffffff</c>).
    /// </summary>
    private static NativeOperand ImmSx(ref NativeCodeReader r, int size, int opSize)
    {
        long signed = size == 1 ? r.ReadI8() : size == 2 ? r.ReadI16() : r.ReadI32();
        var masked = opSize >= 8 ? (ulong)signed : (ulong)signed & ((1UL << (opSize * 8)) - 1);
        return new NativeOperand(NativeOperandKind.Immediate, $"0x{masked:x}", Immediate: signed);
    }

    private static NativeOperand Rel(ref NativeCodeReader r, int size, OperandContext ctx, ref ulong? target)
    {
        long rel = size == 1 ? r.ReadI8() : (size == 2 ? r.ReadI16() : r.ReadI32());
        // Relative to the next instruction's VA: instruction start VA + its length so far.
        var nextIp = unchecked(ctx.Address + (ulong)(r.Position - ctx.CodeStart));
        var abs = unchecked(nextIp + (ulong)rel);
        target = abs;
        return new NativeOperand(NativeOperandKind.RelativeTarget, $"0x{abs:x}", Immediate: rel);
    }

    private static NativeOperand Moffs(ref NativeCodeReader r, OperandContext ctx, int size)
    {
        var addr = ctx.Prefixes.AddrSize ? r.ReadU32() : r.ReadU64();
        return XarchOperandFormatter.Memory(size, null, null, 0, (long)addr, ripRelative: false);
    }

    private static string ApplyRepPrefix(string mnemonic, int opcode, Prefixes p)
    {
        var isString = opcode is (>= 0xA4 and <= 0xA7) or (>= 0xAA and <= 0xAF) or (>= 0x6C and <= 0x6F);
        if (!isString) return mnemonic;
        var cmpOrScas = opcode is 0xA6 or 0xA7 or 0xAE or 0xAF;
        if (p.Rep) return (cmpOrScas ? "repe " : "rep ") + mnemonic;
        if (p.Repne) return "repne " + mnemonic;
        return mnemonic;
    }

    private static NativeFlowKind ClassifyFlow(string mnemonic, List<NativeOperand> operands)
    {
        if (mnemonic is "ret" or "retf" or "iret" or "sysret") return NativeFlowKind.Return;
        var hasRel = operands.Any(o => o.Kind == NativeOperandKind.RelativeTarget);
        if (mnemonic == "call") return hasRel ? NativeFlowKind.Call : NativeFlowKind.IndirectCall;
        if (mnemonic == "jmp") return hasRel ? NativeFlowKind.Jump : NativeFlowKind.IndirectJump;
        if (mnemonic.Length > 1 && mnemonic[0] == 'j') return NativeFlowKind.ConditionalBranch;
        if (mnemonic is "loop" or "loope" or "loopne" or "jrcxz") return NativeFlowKind.ConditionalBranch;
        return NativeFlowKind.Sequential;
    }

    private static NativeInstructionCategory ClassifyCategory(string mnemonic, int opcode, int map)
    {
        if (map == XarchTables.MapOneByte && opcode is >= 0xD8 and <= 0xDF) return NativeInstructionCategory.Float;
        if (mnemonic is "nop" or "int3" or "int1" or "ud2" or "pause" or "cpuid" or "rdtsc" or "hlt"
            or "mfence" or "lfence" or "sfence" or "xgetbv" or "xsetbv" or "endbr64" or "endbr32"
            or "cli" or "sti" or "cld" or "std" or "clc" or "stc" or "cmc" or "syscall")
        {
            return NativeInstructionCategory.System;
        }

        return mnemonic is "call" or "jmp" or "ret" or "retf" or "iret" || (mnemonic.Length > 1 && mnemonic[0] == 'j')
            || mnemonic is "loop" or "loope" or "loopne" or "jrcxz"
            ? NativeInstructionCategory.Control
            : NativeInstructionCategory.Integer;
    }

    private static NativeInstruction Build(
        ReadOnlySpan<byte> code, int start, ulong address, string mnemonic, List<NativeOperand> operands,
        NativeInstructionCategory category, NativeFlowKind flow, int endPosition, ulong? target = null)
    {
        var bytes = code[start..endPosition].ToArray();
        var operandText = string.Join(", ", operands.Select(o => o.Text));
        var targetKind = target is null
            ? NativeTargetKind.None
            : flow is NativeFlowKind.Call or NativeFlowKind.Jump or NativeFlowKind.ConditionalBranch
                ? NativeTargetKind.Function
                : NativeTargetKind.None;
        return new NativeInstruction(
            Address: address, Rva: null, FileOffset: null, Bytes: bytes, Length: bytes.Length,
            Mnemonic: mnemonic, Operands: operands, OperandText: operandText,
            Category: category, Flow: flow, TargetAddress: target, TargetKind: targetKind);
    }

    private static NativeInstruction ByteFallback(ReadOnlySpan<byte> code, int start, ulong address)
    {
        var b = code[start];
        return new NativeInstruction(
            Address: address, Rva: null, FileOffset: null, Bytes: [b], Length: 1,
            Mnemonic: ".byte",
            Operands: [new NativeOperand(NativeOperandKind.Immediate, $"0x{b:x2}", Immediate: b)],
            OperandText: $"0x{b:x2}",
            Category: NativeInstructionCategory.Unknown, Flow: NativeFlowKind.Sequential, IsFallback: true);
    }

    private ref struct Prefixes
    {
        public bool OpSize;
        public bool AddrSize;
        public bool Lock;
        public bool Rep;
        public bool Repne;
        public int Segment;
        public bool HasRex;
        public bool RexW;
        public bool RexR;
        public bool RexX;
        public bool RexB;
    }

    private readonly ref struct OperandContext(int opSize, Prefixes prefixes, ulong address, int codeStart)
    {
        public int OpSize { get; } = opSize;
        public Prefixes Prefixes { get; } = prefixes;
        public ulong Address { get; } = address;
        public int CodeStart { get; } = codeStart;
    }
}
