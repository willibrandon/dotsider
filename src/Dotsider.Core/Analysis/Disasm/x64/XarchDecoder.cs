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
        p.VectorLen = 16;

        // 1. Legacy prefixes.
        while (r.HasMore && TryConsumeLegacyPrefix(ref r, ref p)) { }
        if (!r.HasMore) return ByteFallback(code, start, address);

        int map, opcode, pp;
        var next = r.Peek();
        if (next is 0xC4 or 0xC5)
        {
            // 2a. VEX prefix carries the map, mandatory prefix, W, vvvv, and vector length.
            (map, pp) = ParseVex(ref r, ref p);
            if (!r.HasMore) return ByteFallback(code, start, address);
            opcode = r.ReadU8();
        }
        else if (next == 0x62)
        {
            // 2a'. EVEX prefix: like VEX plus R'/V'/X extensions, opmask, zeroing, and broadcast.
            (map, pp) = ParseEvex(ref r, ref p);
            if (map < 0 || !r.HasMore) return ByteFallback(code, start, address);
            opcode = r.ReadU8();
        }
        else
        {
            // 2b. Optional REX, then opcode + 0F / 0F 38 / 0F 3A escape.
            if (next is >= 0x40 and <= 0x4F)
            {
                var rex = r.ReadU8();
                p.HasRex = true;
                p.RexW = (rex & 8) != 0;
                p.RexR = (rex & 4) != 0;
                p.RexX = (rex & 2) != 0;
                p.RexB = (rex & 1) != 0;
                if (!r.HasMore) return ByteFallback(code, start, address);
            }

            map = XarchTables.MapOneByte;
            opcode = r.ReadU8();
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

            pp = p.Rep ? XarchTables.PpF3 : p.Repne ? XarchTables.PpF2 : p.OpSize ? XarchTables.Pp66 : XarchTables.PpNone;
        }

        // Under VEX the 0F setcc/cmovcc slots are the opmask ops (kmov/kand/…) instead.
        var entry = p.HasVex && map == XarchTables.Map0F && XarchTables.TryKmask(pp, opcode, out var km)
            ? km
            : XarchTables.Lookup(map, pp, opcode);
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
        // 66 is an operand-size override only when it is NOT the mandatory prefix that selected an
        // SSE/vector entry; F2/F3 are never operand-size.
        var op66Mandatory = p.OpSize && map != XarchTables.MapOneByte
            && XarchTables.HasEntry(map, XarchTables.Pp66, opcode);
        var effOp = EffectiveOperandSize(entry, p, p.OpSize && !op66Mandatory);

        // movd becomes movq under REX.W (the GPR operand is 64-bit).
        if (mnemonic == "movd" && p.RexW) mnemonic = "movq";

        // FMA: VEX.W selects the pd/sd (W=1) vs ps/ss (W=0) suffix registered on the row.
        if (map == XarchTables.Map0F38 && p.HasVex && p.RexW && XarchTables.IsFmaOpcode(opcode)
            && mnemonic.Length > 2)
        {
            mnemonic = mnemonic.EndsWith("ps") ? mnemonic[..^2] + "pd"
                : mnemonic.EndsWith("ss") ? mnemonic[..^2] + "sd"
                : mnemonic;
        }

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

        // VEX 0F 77 is vzeroupper (128) / vzeroall (256); the legacy form is emms.
        if (map == XarchTables.Map0F && opcode == 0x77 && p.HasVex)
        {
            return Build(code, start, address, p.VectorLen == 32 ? "vzeroall" : "vzeroupper", [],
                NativeInstructionCategory.Vector, NativeFlowKind.Sequential, r.Position);
        }

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

        mnemonic = ApplyRepPrefix(mnemonic, map, opcode, p);
        if (p.HasEvex) mnemonic = EvexSuffix(mnemonic, map, opcode, entry.Flags, p);
        if (p.IsVector && (entry.Flags & OpFlags.NoVexPrefix) == 0 && mnemonic != ".byte")
            mnemonic = "v" + mnemonic;
        if (p.HasEvex) ApplyEvexDecoration(operands, p);
        var flow = ClassifyFlow(mnemonic, operands);
        var category = ClassifyCategory(mnemonic, opcode, map, operands);
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

    /// <summary>
    /// Parses a 2-byte (<c>C5</c>) or 3-byte (<c>C4</c>) VEX prefix, filling the vector-encoding
    /// state (inverted R/X/B/vvvv, W, vector length, mandatory prefix) and returning the opcode map
    /// and mandatory-prefix index the VEX fields select.
    /// </summary>
    private static (int Map, int Pp) ParseVex(ref NativeCodeReader r, ref Prefixes p)
    {
        p.HasVex = true;
        var lead = r.ReadU8();
        if (lead == 0xC5)
        {
            var b1 = r.ReadU8();
            p.RexR = (b1 & 0x80) == 0;
            p.Vvvv = ~(b1 >> 3) & 0xF;
            p.VectorLen = (b1 & 4) != 0 ? 32 : 16;
            return (XarchTables.Map0F, VexPp(b1 & 3));
        }

        var c1 = r.ReadU8();
        var c2 = r.ReadU8();
        p.RexR = (c1 & 0x80) == 0;
        p.RexX = (c1 & 0x40) == 0;
        p.RexB = (c1 & 0x20) == 0;
        p.RexW = (c2 & 0x80) != 0;
        p.Vvvv = ~(c2 >> 3) & 0xF;
        p.VectorLen = (c2 & 4) != 0 ? 32 : 16;
        var map = (c1 & 0x1F) switch
        {
            2 => XarchTables.Map0F38,
            3 => XarchTables.Map0F3A,
            _ => XarchTables.Map0F,
        };
        return (map, VexPp(c2 & 3));
    }

    private static int VexPp(int pp) => pp switch
    {
        1 => XarchTables.Pp66,
        2 => XarchTables.PpF3,
        3 => XarchTables.PpF2,
        _ => XarchTables.PpNone,
    };

    /// <summary>
    /// Parses a 4-byte EVEX prefix (<c>62</c> + three payload bytes), filling the R/X/B plus the R'/
    /// V'/X extension bits (for xmm/zmm 16-31), W, vvvv, vector length from L'L, opmask (aaa),
    /// zeroing (z), and broadcast (b). Returns (-1, -1) if the payload is truncated.
    /// </summary>
    private static (int Map, int Pp) ParseEvex(ref NativeCodeReader r, ref Prefixes p)
    {
        r.ReadU8(); // 0x62
        if (r.Remaining < 3) return (-1, -1);
        var p0 = r.ReadU8();
        var p1 = r.ReadU8();
        var p2 = r.ReadU8();

        p.HasEvex = true;
        p.RexR = (p0 & 0x80) == 0;
        p.RexX = (p0 & 0x40) == 0;
        p.EvexX = (p0 & 0x40) == 0;
        p.RexB = (p0 & 0x20) == 0;
        p.EvexR2 = (p0 & 0x10) == 0;

        p.RexW = (p1 & 0x80) != 0;
        var v2 = (p2 & 0x08) == 0;
        p.Vvvv = (~(p1 >> 3) & 0xF) | (v2 ? 16 : 0);

        var ll = ((p2 >> 6) & 1) << 1 | ((p2 >> 5) & 1);
        p.VectorLen = ll == 0 ? 16 : ll == 1 ? 32 : 64;
        p.Zeroing = (p2 & 0x80) != 0;
        p.Broadcast = (p2 & 0x10) != 0;
        p.MaskReg = p2 & 7;

        var map = (p0 & 0x7) switch
        {
            2 => XarchTables.Map0F38,
            3 => XarchTables.Map0F3A,
            1 => XarchTables.Map0F,
            _ => -1,
        };
        return (map, VexPp(p1 & 3));
    }

    private static OpEntry MergeGroup(OpEntry primary, OpEntry group)
    {
        var hasOperands = group.Op1 != OperandKind.None;
        return hasOperands
            ? group with { Flags = group.Flags | (primary.Flags & (OpFlags.Default64 | OpFlags.Force64)) }
            : primary with { Mnemonic = group.Mnemonic, Flags = primary.Flags | group.Flags };
    }

    private static int EffectiveOperandSize(OpEntry entry, Prefixes p, bool opSize16)
    {
        if ((entry.Flags & OpFlags.Force64) != 0) return 8;
        if ((entry.Flags & OpFlags.Default64) != 0) return opSize16 ? 2 : 8;
        return p.RexW ? 8 : opSize16 ? 2 : 4;
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
            case OperandKind.Ey: operands.Add(Rm(ref r, modrm, ctx, ctx.OpSize == 2 ? 4 : ctx.OpSize)); break;
            case OperandKind.M: case OperandKind.Mv: operands.Add(Rm(ref r, modrm, ctx, ctx.OpSize)); break;
            case OperandKind.Wx: operands.Add(Rm(ref r, modrm, ctx, ctx.Prefixes.VectorLen, vector: true)); break;
            case OperandKind.Wss: operands.Add(Rm(ref r, modrm, ctx, 4, vector: true)); break;
            case OperandKind.Wsd: operands.Add(Rm(ref r, modrm, ctx, 8, vector: true)); break;
            case OperandKind.Wxmm: operands.Add(Rm(ref r, modrm, ctx, 16, vector: true)); break;
            case OperandKind.Qq: operands.Add(Rm(ref r, modrm, ctx, 8, mmx: true)); break;

            case OperandKind.Gb: operands.Add(Reg(modrm, ctx, 1)); break;
            case OperandKind.Gw: operands.Add(Reg(modrm, ctx, 2)); break;
            case OperandKind.Gd: operands.Add(Reg(modrm, ctx, 4)); break;
            case OperandKind.Gv: operands.Add(Reg(modrm, ctx, ctx.OpSize)); break;
            case OperandKind.Gy: operands.Add(Reg(modrm, ctx, ctx.OpSize == 2 ? 4 : ctx.OpSize)); break;
            case OperandKind.Vx: operands.Add(RegVector(modrm, ctx, ctx.Prefixes.VectorLen)); break;
            case OperandKind.Hx:
                // The vvvv source exists only under VEX/EVEX; legacy SSE decode drops it.
                if (ctx.Prefixes.IsVector)
                {
                    var h = XarchRegisters.Vector(ctx.Prefixes.Vvvv, ctx.Prefixes.VectorLen);
                    operands.Add(new NativeOperand(NativeOperandKind.Register, h, Register: h));
                }

                break;
            case OperandKind.Lx:
            {
                // is4: the high nibble of a trailing imm8 selects a vector register.
                var is4 = r.ReadU8();
                var l = XarchRegisters.Vector((is4 >> 4) & (ctx.Prefixes.HasEvex ? 31 : 15), ctx.Prefixes.VectorLen);
                operands.Add(new NativeOperand(NativeOperandKind.Register, l, Register: l));
                break;
            }
            case OperandKind.Kr: { var k = XarchRegisters.Mask(((modrm >> 3) & 7)); operands.Add(new NativeOperand(NativeOperandKind.Register, k, Register: k)); break; }
            case OperandKind.Km: { var k = XarchRegisters.Mask(modrm & 7); operands.Add(new NativeOperand(NativeOperandKind.Register, k, Register: k)); break; }
            case OperandKind.Kv: { var k = XarchRegisters.Mask(ctx.Prefixes.Vvvv & 7); operands.Add(new NativeOperand(NativeOperandKind.Register, k, Register: k)); break; }
            case OperandKind.Pq: operands.Add(new NativeOperand(NativeOperandKind.Register, XarchRegisters.Mmx((modrm >> 3) & 7), Register: XarchRegisters.Mmx((modrm >> 3) & 7))); break;
            case OperandKind.Sw: operands.Add(new NativeOperand(NativeOperandKind.Register, XarchRegisters.Segment((modrm >> 3) & 7), Register: XarchRegisters.Segment((modrm >> 3) & 7))); break;

            // A vector/mask control imm8 is a raw byte; a GPR ALU imm8 sign-extends to operand size.
            case OperandKind.Ib when IsVectorContext(operands): operands.Add(Imm(ref r, 1)); break;
            case OperandKind.Ib: operands.Add(ImmSx(ref r, 1, ctx.OpSize)); break;
            case OperandKind.Iw: operands.Add(Imm(ref r, 2)); break;
            case OperandKind.Id: operands.Add(Imm(ref r, 4)); break;
            case OperandKind.Iz: operands.Add(ImmSx(ref r, ctx.OpSize == 2 ? 2 : 4, ctx.OpSize)); break;
            case OperandKind.Iv: operands.Add(Imm(ref r, ctx.OpSize)); break;

            case OperandKind.Jb: operands.Add(Rel(ref r, 1, ctx, ref target)); break;
            case OperandKind.Jz: operands.Add(Rel(ref r, ctx.OpSize == 2 ? 2 : 4, ctx, ref target)); break;

            case OperandKind.Ob: case OperandKind.Ov: operands.Add(Moffs(ref r, ctx, kind == OperandKind.Ob ? 1 : ctx.OpSize)); break;

            case OperandKind.By: operands.Add(RegDirect(ctx.Prefixes.Vvvv, ctx, ctx.OpSize == 2 ? 4 : ctx.OpSize)); break;
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

    /// <summary>Whether the operands so far name a vector or mask register (so an imm8 is a raw control byte).</summary>
    private static bool IsVectorContext(List<NativeOperand> operands) =>
        operands.Any(o => o.Register is { } n
            && (n.StartsWith("xmm") || n.StartsWith("ymm") || n.StartsWith("zmm") || n[0] == 'k'));

    private static int RegIndex(byte modrm, OperandContext ctx) =>
        ((modrm >> 3) & 7) + (ctx.Prefixes.RexR ? 8 : 0) + (ctx.Prefixes.EvexR2 ? 16 : 0);

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
        var rmFull = rmLow + (ctx.Prefixes.RexB ? 8 : 0) + (ctx.Prefixes.EvexX && vector ? 16 : 0);

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
        var hintSize = mmx ? 8 : size;
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

    private static string ApplyRepPrefix(string mnemonic, int map, int opcode, Prefixes p)
    {
        // Only the one-byte string opcodes take a rep prefix; on the 0F map F3/F2 are mandatory.
        var isString = map == XarchTables.MapOneByte
            && opcode is (>= 0xA4 and <= 0xA7) or (>= 0xAA and <= 0xAF) or (>= 0x6C and <= 0x6F);
        if (!isString) return mnemonic;
        var cmpOrScas = opcode is 0xA6 or 0xA7 or 0xAE or 0xAF;
        if (p.Rep) return (cmpOrScas ? "repe " : "rep ") + mnemonic;
        if (p.Repne) return "repne " + mnemonic;
        return mnemonic;
    }

    /// <summary>
    /// Applies the element-width suffix that EVEX adds to the SSE/AVX logical and packed-move
    /// opcodes (e.g. <c>pxor</c> → <c>pxord</c>/<c>pxorq</c>, <c>movdqa</c> → <c>movdqa32/64</c> by
    /// EVEX.W). Ops that already encode their width in the base name pass through unchanged.
    /// </summary>
    private static string EvexSuffix(string m, int map, int opcode, OpFlags flags, Prefixes p)
    {
        if ((flags & OpFlags.EvexDQ) != 0) return m + (p.RexW ? "q" : "d");

        // scalef/getexp carry ps (W=0) / pd (W=1) like the FMA rows.
        if (map == XarchTables.Map0F38 && opcode is 0x2C or 0x42 && p.RexW && m.EndsWith("ps"))
            return m[..^2] + "pd";

        // round* (0F3A 08-0B) is spelled rndscale* under EVEX.
        if (map == XarchTables.Map0F3A && opcode is >= 0x08 and <= 0x0B && m.StartsWith("round"))
            return "rndscale" + m[5..];

        if (map != XarchTables.Map0F) return m;
        var q = p.RexW;
        return opcode switch
        {
            0xDB => q ? "pandq" : "pandd",
            0xDF => q ? "pandnq" : "pandnd",
            0xEB => q ? "porq" : "pord",
            0xEF => q ? "pxorq" : "pxord",
            0x6F or 0x7F when m == "movdqa" => q ? "movdqa64" : "movdqa32",
            0x6F or 0x7F when m == "movdqu" => q ? "movdqu64" : "movdqu32",
            _ => m,
        };
    }

    /// <summary>
    /// Decorates the operands with EVEX masking and broadcast: <c>{k1}</c>/<c>{z}</c> on the
    /// destination when an opmask is selected, and <c>{1toN}</c> on a memory source under broadcast.
    /// </summary>
    private static void ApplyEvexDecoration(List<NativeOperand> operands, Prefixes p)
    {
        if (operands.Count == 0) return;

        if (p.MaskReg != 0)
        {
            var dst = operands[0];
            var text = $"{dst.Text}{{k{p.MaskReg}}}" + (p.Zeroing ? "{z}" : "");
            operands[0] = dst with { Text = text };
        }

        if (p.Broadcast)
        {
            for (var i = 0; i < operands.Count; i++)
            {
                if (operands[i].Kind != NativeOperandKind.Memory) continue;
                var n = p.VectorLen / (p.RexW ? 8 : 4);
                operands[i] = operands[i] with { Text = $"{operands[i].Text} {{1to{n}}}" };
                break;
            }
        }
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

    private static NativeInstructionCategory ClassifyCategory(
        string mnemonic, int opcode, int map, List<NativeOperand> operands)
    {
        if (map == XarchTables.MapOneByte && opcode is >= 0xD8 and <= 0xDF) return NativeInstructionCategory.Float;
        if (mnemonic is "nop" or "int3" or "int1" or "ud2" or "pause" or "cpuid" or "rdtsc" or "hlt"
            or "mfence" or "lfence" or "sfence" or "xgetbv" or "xsetbv" or "endbr64" or "endbr32"
            or "cli" or "sti" or "cld" or "std" or "clc" or "stc" or "cmc" or "syscall")
        {
            return NativeInstructionCategory.System;
        }

        if (mnemonic is "call" or "jmp" or "ret" or "retf" or "iret" || (mnemonic.Length > 1 && mnemonic[0] == 'j')
            || mnemonic is "loop" or "loope" or "loopne" or "jrcxz")
        {
            return NativeInstructionCategory.Control;
        }

        // Vector/float: an operand names a vector register. Scalar single/double forms are float.
        var usesVector = operands.Any(o => o.Register is { } n && (n.StartsWith("xmm") || n.StartsWith("ymm") || n.StartsWith("zmm")));
        if (usesVector)
        {
            var scalar = mnemonic.EndsWith("ss") || mnemonic.EndsWith("sd") || mnemonic.StartsWith("cvtsi")
                || mnemonic is "movd" or "movq";
            return scalar ? NativeInstructionCategory.Float : NativeInstructionCategory.Vector;
        }

        return NativeInstructionCategory.Integer;
    }

    private static NativeInstruction Build(
        ReadOnlySpan<byte> code, int start, ulong address, string mnemonic, List<NativeOperand> operands,
        NativeInstructionCategory category, NativeFlowKind flow, int endPosition, ulong? target = null)
    {
        var bytes = code[start..endPosition].ToArray();
        var operandText = string.Join(", ", operands.Select(o => o.Text));

        var targetKind = NativeTargetKind.None;
        if (target is not null)
        {
            targetKind = flow is NativeFlowKind.Call or NativeFlowKind.Jump or NativeFlowKind.ConditionalBranch
                ? NativeTargetKind.Function
                : NativeTargetKind.None;
        }
        else if (operands.FirstOrDefault(o => o.IsRipRelative) is { } rip)
        {
            // RIP-relative data target: absolute VA = next-instruction VA + disp = address + length + disp.
            target = unchecked(address + (ulong)bytes.Length + (ulong)rip.MemoryDisplacement);
            targetKind = NativeTargetKind.Data;
        }

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

        // VEX/EVEX vector-encoding state.
        public bool HasVex;
        public bool HasEvex;
        public int Vvvv;        // the vvvv source register (0-31)
        public int VectorLen;   // packed vector length in bytes (16/32/64)
        public bool EvexR2;     // R' — reg bit 4 (xmm/zmm 16-31)
        public bool EvexX;      // X — rm bit 4 for a register operand
        public int MaskReg;     // EVEX aaa opmask (0 = none)
        public bool Zeroing;    // EVEX z (merging vs zeroing)
        public bool Broadcast;  // EVEX b (memory broadcast)

        public readonly bool IsVector => HasVex || HasEvex;
    }

    private readonly ref struct OperandContext(int opSize, Prefixes prefixes, ulong address, int codeStart)
    {
        public int OpSize { get; } = opSize;
        public Prefixes Prefixes { get; } = prefixes;
        public ulong Address { get; } = address;
        public int CodeStart { get; } = codeStart;
    }
}
