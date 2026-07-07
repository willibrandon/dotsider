using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.wasm32;

/// <summary>
/// Decodes WebAssembly instructions emitted by .NET Wasm targets.
/// The decoder models bytecode opcodes and LEB128 operands as native instruction records.
/// Unknown opcodes fall back one byte at a time so listings do not desynchronize.
/// </summary>
internal static class Wasm32Decoder
{
    /// <summary>
    /// Decodes one WebAssembly instruction beginning at the requested byte offset.
    /// Variable-length immediates are consumed with LEB128 decoding.
    /// The returned model carries structured operands for CLI, MCP, and TUI consumers.
    /// </summary>
    public static NativeInstruction Decode(ReadOnlySpan<byte> code, int start, ulong address)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, code.Length);

        var pos = start;
        var op = code[pos++];
        try
        {
            return op switch
            {
                0x00 => Simple(code, start, pos, address, "unreachable", [], NativeInstructionCategory.Control),
                0x01 => Simple(code, start, pos, address, "nop", [], NativeInstructionCategory.System),
                0x02 => Block(code, start, ref pos, address, "block"),
                0x03 => Block(code, start, ref pos, address, "loop"),
                0x04 => Block(code, start, ref pos, address, "if"),
                0x05 => Simple(code, start, pos, address, "else", [], NativeInstructionCategory.Control),
                0x08 => Index(code, start, ref pos, address, "throw", NativeFlowKind.Jump),
                0x0A => Simple(code, start, pos, address, "throw_ref", [], NativeInstructionCategory.Control, NativeFlowKind.Jump),
                0x0B => Simple(code, start, pos, address, "end", [], NativeInstructionCategory.Control),
                0x0C => Depth(code, start, ref pos, address, "br", NativeFlowKind.Jump),
                0x0D => Depth(code, start, ref pos, address, "br_if", NativeFlowKind.ConditionalBranch),
                0x0E => BrTable(code, start, ref pos, address),
                0x0F => Simple(code, start, pos, address, "return", [], NativeInstructionCategory.Control, NativeFlowKind.Return),
                0x10 => Index(code, start, ref pos, address, "call", NativeFlowKind.Call),
                0x11 => CallIndirect(code, start, ref pos, address),
                0x12 => Index(code, start, ref pos, address, "return_call", NativeFlowKind.Call),
                0x13 => CallIndirect(code, start, ref pos, address, "return_call_indirect"),
                0x14 => Index(code, start, ref pos, address, "call_ref", NativeFlowKind.IndirectCall),
                0x15 => Index(code, start, ref pos, address, "return_call_ref", NativeFlowKind.IndirectCall),
                0x1A => Simple(code, start, pos, address, "drop", []),
                0x1B => Simple(code, start, pos, address, "select", []),
                0x1C => SelectTyped(code, start, ref pos, address),
                0x20 => Index(code, start, ref pos, address, "local.get"),
                0x21 => Index(code, start, ref pos, address, "local.set"),
                0x22 => Index(code, start, ref pos, address, "local.tee"),
                0x23 => Index(code, start, ref pos, address, "global.get"),
                0x24 => Index(code, start, ref pos, address, "global.set"),
                0x25 => Index(code, start, ref pos, address, "table.get"),
                0x26 => Index(code, start, ref pos, address, "table.set"),
                >= 0x28 and <= 0x3E => Memory(code, start, ref pos, address, MemoryName(op)),
                0x3F => ReservedZero(code, start, ref pos, address, "memory.size"),
                0x40 => ReservedZero(code, start, ref pos, address, "memory.grow"),
                0x41 => ConstI32(code, start, ref pos, address),
                0x42 => ConstI64(code, start, ref pos, address),
                0x43 => ConstF32(code, start, ref pos, address),
                0x44 => ConstF64(code, start, ref pos, address),
                >= 0x45 and <= 0xC4 => Numeric(code, start, pos, address, op),
                0xFB => Gc(code, start, ref pos, address),
                0xFC => Prefixed(code, start, ref pos, address, "misc"),
                0xFD => Prefixed(code, start, ref pos, address, "simd", NativeInstructionCategory.Vector),
                _ => NativeDecoderSupport.FallbackWasm(code, start, address),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return NativeDecoderSupport.FallbackWasm(code, start, address);
        }
    }

    private static NativeInstruction Block(ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic)
    {
        var blockType = ReadBlockType(code, ref pos);
        return Simple(code, start, pos, address, mnemonic, [NativeDecoderSupport.Imm(blockType.Value, blockType.Text)],
            NativeInstructionCategory.Control);
    }

    private static NativeInstruction Depth(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic, NativeFlowKind flow)
    {
        var depth = (long)NativeDecoderSupport.ReadUleb(code, ref pos);
        return Simple(code, start, pos, address, mnemonic, [NativeDecoderSupport.Imm(depth, $"depth {depth}")],
            NativeInstructionCategory.Control, flow);
    }

    private static NativeInstruction BrTable(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var count = NativeDecoderSupport.ReadUleb(code, ref pos);
        var operands = new List<NativeOperand>();
        for (ulong i = 0; i < count; i++)
        {
            var depth = NativeDecoderSupport.ReadUleb(code, ref pos);
            operands.Add(NativeDecoderSupport.Imm((long)depth, $"depth {depth}"));
        }

        var defaultDepth = NativeDecoderSupport.ReadUleb(code, ref pos);
        operands.Add(NativeDecoderSupport.Imm((long)defaultDepth, $"default {defaultDepth}"));
        return Simple(code, start, pos, address, "br_table", operands,
            NativeInstructionCategory.Control, NativeFlowKind.ConditionalBranch);
    }

    private static NativeInstruction Index(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic,
        NativeFlowKind flow = NativeFlowKind.Sequential)
    {
        var index = NativeDecoderSupport.ReadUleb(code, ref pos);
        return Simple(code, start, pos, address, mnemonic, [NativeDecoderSupport.Imm((long)index, $"#{index}")],
            flow is NativeFlowKind.Sequential ? NativeInstructionCategory.Integer : NativeInstructionCategory.Control,
            flow);
    }

    private static NativeInstruction CallIndirect(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic = "call_indirect")
    {
        var type = NativeDecoderSupport.ReadUleb(code, ref pos);
        var table = NativeDecoderSupport.ReadUleb(code, ref pos);
        return Simple(code, start, pos, address, mnemonic,
            [NativeDecoderSupport.Imm((long)type, $"type {type}"), NativeDecoderSupport.Imm((long)table, $"table {table}")],
            NativeInstructionCategory.Control, NativeFlowKind.IndirectCall);
    }

    private static NativeInstruction SelectTyped(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var count = NativeDecoderSupport.ReadUleb(code, ref pos);
        var operands = new List<NativeOperand>();
        for (ulong i = 0; i < count; i++)
        {
            var type = ReadU8(code, ref pos);
            operands.Add(NativeDecoderSupport.Imm(type, ValueTypeName(type)));
        }

        return Simple(code, start, pos, address, "select", operands);
    }

    private static NativeInstruction Memory(ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic)
    {
        var align = NativeDecoderSupport.ReadUleb(code, ref pos);
        ulong? memoryIndex = null;
        if ((align & 0x40) != 0)
        {
            align &= ~0x40UL;
            memoryIndex = NativeDecoderSupport.ReadUleb(code, ref pos);
        }

        var offset = NativeDecoderSupport.ReadUleb(code, ref pos);
        var operands = new List<NativeOperand>
        {
            NativeDecoderSupport.Imm((long)align, $"align={1UL << (int)align}"),
            NativeDecoderSupport.Imm((long)offset, $"offset={offset}"),
        };
        if (memoryIndex is { } index)
            operands.Add(NativeDecoderSupport.Imm((long)index, $"mem={index}"));

        return Simple(code, start, pos, address, mnemonic, operands);
    }

    private static NativeInstruction ReservedZero(ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string mnemonic)
    {
        var reserved = NativeDecoderSupport.ReadUleb(code, ref pos);
        return Simple(code, start, pos, address, mnemonic, reserved == 0 ? [] : [NativeDecoderSupport.Imm((long)reserved)]);
    }

    private static NativeInstruction ConstI32(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var value = NativeDecoderSupport.ReadSleb(code, ref pos);
        return Simple(code, start, pos, address, "i32.const", [NativeDecoderSupport.Imm(value)]);
    }

    private static NativeInstruction ConstI64(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var value = NativeDecoderSupport.ReadSleb(code, ref pos, maxBytes: 10);
        return Simple(code, start, pos, address, "i64.const", [NativeDecoderSupport.Imm(value)]);
    }

    private static NativeInstruction ConstF32(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        if (pos + 4 > code.Length) throw new ArgumentOutOfRangeException(nameof(pos));
        var bytes = BitConverter.ToUInt32(code.Slice(pos, 4));
        pos += 4;
        return Simple(code, start, pos, address, "f32.const", [NativeDecoderSupport.Imm(bytes, $"0x{bytes:x8}")], NativeInstructionCategory.Float);
    }

    private static NativeInstruction ConstF64(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        if (pos + 8 > code.Length) throw new ArgumentOutOfRangeException(nameof(pos));
        var bytes = BitConverter.ToUInt64(code.Slice(pos, 8));
        pos += 8;
        return Simple(code, start, pos, address, "f64.const", [NativeDecoderSupport.Imm(unchecked((long)bytes), $"0x{bytes:x16}")],
            NativeInstructionCategory.Float);
    }

    private static NativeInstruction Numeric(ReadOnlySpan<byte> code, int start, int pos, ulong address, byte op)
    {
        var mnemonic = NumericName(op);
        var category = mnemonic[0] == 'f' ? NativeInstructionCategory.Float : NativeInstructionCategory.Integer;
        return Simple(code, start, pos, address, mnemonic, [], category);
    }

    private static NativeInstruction Prefixed(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, string family,
        NativeInstructionCategory category = NativeInstructionCategory.Integer)
    {
        var sub = NativeDecoderSupport.ReadUleb(code, ref pos);
        return family == "simd"
            ? Simd(code, start, ref pos, address, sub)
            : Misc(code, start, ref pos, address, sub, category);
    }

    private static NativeInstruction Misc(
        ReadOnlySpan<byte> code, int start, ref int pos, ulong address, ulong sub, NativeInstructionCategory category)
    {
        var operands = new List<NativeOperand>();
        switch (sub)
        {
            case <= 7:
                break;
            case 8:
                operands.Add(IndexOperand(code, ref pos, "data"));
                operands.Add(IndexOperand(code, ref pos, "memory"));
                break;
            case 9:
            case 11:
            case 13:
            case 15:
            case 16:
            case 17:
                operands.Add(IndexOperand(code, ref pos, "index"));
                break;
            case 10:
                operands.Add(IndexOperand(code, ref pos, "dst"));
                operands.Add(IndexOperand(code, ref pos, "src"));
                break;
            case 12:
                operands.Add(IndexOperand(code, ref pos, "elem"));
                operands.Add(IndexOperand(code, ref pos, "table"));
                break;
            case 14:
                operands.Add(IndexOperand(code, ref pos, "dst"));
                operands.Add(IndexOperand(code, ref pos, "src"));
                break;
        }

        return Simple(code, start, pos, address, MiscName(sub), operands, category);
    }

    private static NativeInstruction Gc(ReadOnlySpan<byte> code, int start, ref int pos, ulong address)
    {
        var sub = NativeDecoderSupport.ReadUleb(code, ref pos);
        var operands = new List<NativeOperand>();
        var flow = NativeFlowKind.Sequential;
        switch (sub)
        {
            case 0 or 1 or 6 or 7 or 11 or 12 or 13 or 14 or 16 or 20 or 21 or 22 or 23:
                operands.Add(IndexOperand(code, ref pos, "type"));
                break;
            case 2 or 3 or 4 or 5:
                operands.Add(IndexOperand(code, ref pos, "type"));
                operands.Add(IndexOperand(code, ref pos, "field"));
                break;
            case 8:
                operands.Add(IndexOperand(code, ref pos, "type"));
                operands.Add(IndexOperand(code, ref pos, "length"));
                break;
            case 9 or 18:
                operands.Add(IndexOperand(code, ref pos, "type"));
                operands.Add(IndexOperand(code, ref pos, "data"));
                break;
            case 10 or 19:
                operands.Add(IndexOperand(code, ref pos, "type"));
                operands.Add(IndexOperand(code, ref pos, "elem"));
                break;
            case 17:
                operands.Add(IndexOperand(code, ref pos, "dst"));
                operands.Add(IndexOperand(code, ref pos, "src"));
                break;
            case 24 or 25:
                operands.Add(IndexOperand(code, ref pos, "label"));
                operands.Add(IndexOperand(code, ref pos, "from"));
                operands.Add(IndexOperand(code, ref pos, "to"));
                flow = NativeFlowKind.ConditionalBranch;
                break;
        }

        return Simple(code, start, pos, address, GcName(sub), operands, NativeInstructionCategory.Control, flow);
    }

    private static NativeInstruction Simd(ReadOnlySpan<byte> code, int start, ref int pos, ulong address, ulong sub)
    {
        var operands = new List<NativeOperand>();
        if (sub is <= 11 or 92 or 93)
        {
            AddMemoryOperands(code, ref pos, operands);
        }
        else if (sub == 12)
        {
            operands.Add(NativeDecoderSupport.Imm(0, $"0x{ReadBytesHex(code, ref pos, 16)}"));
        }
        else if (sub == 13)
        {
            operands.Add(NativeDecoderSupport.Imm(0, string.Join(" ", ReadBytes(code, ref pos, 16))));
        }
        else if (sub is >= 21 and <= 34)
        {
            operands.Add(IndexOperand(code, ref pos, "lane", uleb: false));
        }
        else if (sub is >= 84 and <= 91)
        {
            AddMemoryOperands(code, ref pos, operands);
            operands.Add(IndexOperand(code, ref pos, "lane", uleb: false));
        }

        return Simple(code, start, pos, address, SimdName(sub), operands, NativeInstructionCategory.Vector);
    }

    private static void AddMemoryOperands(ReadOnlySpan<byte> code, ref int pos, List<NativeOperand> operands)
    {
        var align = NativeDecoderSupport.ReadUleb(code, ref pos);
        ulong? memoryIndex = null;
        if ((align & 0x40) != 0)
        {
            align &= ~0x40UL;
            memoryIndex = NativeDecoderSupport.ReadUleb(code, ref pos);
        }

        var offset = NativeDecoderSupport.ReadUleb(code, ref pos);
        operands.Add(NativeDecoderSupport.Imm((long)align, $"align={1UL << (int)align}"));
        operands.Add(NativeDecoderSupport.Imm((long)offset, $"offset={offset}"));
        if (memoryIndex is { } index)
            operands.Add(NativeDecoderSupport.Imm((long)index, $"mem={index}"));
    }

    private static NativeOperand IndexOperand(ReadOnlySpan<byte> code, ref int pos, string label, bool uleb = true)
    {
        var index = uleb ? NativeDecoderSupport.ReadUleb(code, ref pos) : ReadU8(code, ref pos);
        return NativeDecoderSupport.Imm((long)index, $"{label} {index}");
    }

    private static byte[] ReadBytes(ReadOnlySpan<byte> code, ref int pos, int count)
    {
        if (pos + count > code.Length)
            throw new ArgumentOutOfRangeException(nameof(pos));

        var bytes = code.Slice(pos, count).ToArray();
        pos += count;
        return bytes;
    }

    private static string ReadBytesHex(ReadOnlySpan<byte> code, ref int pos, int count) =>
        Convert.ToHexString(ReadBytes(code, ref pos, count)).ToLowerInvariant();

    private static string MemoryName(byte op) => op switch
    {
        0x28 => "i32.load", 0x29 => "i64.load", 0x2A => "f32.load", 0x2B => "f64.load",
        0x2C => "i32.load8_s", 0x2D => "i32.load8_u", 0x2E => "i32.load16_s", 0x2F => "i32.load16_u",
        0x30 => "i64.load8_s", 0x31 => "i64.load8_u", 0x32 => "i64.load16_s", 0x33 => "i64.load16_u",
        0x34 => "i64.load32_s", 0x35 => "i64.load32_u", 0x36 => "i32.store", 0x37 => "i64.store",
        0x38 => "f32.store", 0x39 => "f64.store", 0x3A => "i32.store8", 0x3B => "i32.store16",
        0x3C => "i64.store8", 0x3D => "i64.store16", _ => "i64.store32",
    };

    private static string NumericName(byte op) => op switch
    {
        0x45 => "i32.eqz", 0x46 => "i32.eq", 0x47 => "i32.ne", 0x48 => "i32.lt_s", 0x49 => "i32.lt_u",
        0x4A => "i32.gt_s", 0x4B => "i32.gt_u", 0x4C => "i32.le_s", 0x4D => "i32.le_u", 0x4E => "i32.ge_s",
        0x4F => "i32.ge_u", 0x50 => "i64.eqz", 0x51 => "i64.eq", 0x52 => "i64.ne", 0x53 => "i64.lt_s",
        0x54 => "i64.lt_u", 0x55 => "i64.gt_s", 0x56 => "i64.gt_u", 0x57 => "i64.le_s", 0x58 => "i64.le_u",
        0x59 => "i64.ge_s", 0x5A => "i64.ge_u", 0x5B => "f32.eq", 0x5C => "f32.ne", 0x5D => "f32.lt",
        0x5E => "f32.gt", 0x5F => "f32.le", 0x60 => "f32.ge", 0x61 => "f64.eq", 0x62 => "f64.ne",
        0x63 => "f64.lt", 0x64 => "f64.gt", 0x65 => "f64.le", 0x66 => "f64.ge", 0x67 => "i32.clz",
        0x68 => "i32.ctz", 0x69 => "i32.popcnt", 0x6A => "i32.add", 0x6B => "i32.sub", 0x6C => "i32.mul",
        0x6D => "i32.div_s", 0x6E => "i32.div_u", 0x6F => "i32.rem_s", 0x70 => "i32.rem_u", 0x71 => "i32.and",
        0x72 => "i32.or", 0x73 => "i32.xor", 0x74 => "i32.shl", 0x75 => "i32.shr_s", 0x76 => "i32.shr_u",
        0x77 => "i32.rotl", 0x78 => "i32.rotr", 0x79 => "i64.clz", 0x7A => "i64.ctz", 0x7B => "i64.popcnt",
        0x7C => "i64.add", 0x7D => "i64.sub", 0x7E => "i64.mul", 0x7F => "i64.div_s", 0x80 => "i64.div_u",
        0x81 => "i64.rem_s", 0x82 => "i64.rem_u", 0x83 => "i64.and", 0x84 => "i64.or", 0x85 => "i64.xor",
        0x86 => "i64.shl", 0x87 => "i64.shr_s", 0x88 => "i64.shr_u", 0x89 => "i64.rotl", 0x8A => "i64.rotr",
        0x8B => "f32.abs", 0x8C => "f32.neg", 0x8D => "f32.ceil", 0x8E => "f32.floor", 0x8F => "f32.trunc",
        0x90 => "f32.nearest", 0x91 => "f32.sqrt", 0x92 => "f32.add", 0x93 => "f32.sub", 0x94 => "f32.mul",
        0x95 => "f32.div", 0x96 => "f32.min", 0x97 => "f32.max", 0x98 => "f32.copysign", 0x99 => "f64.abs",
        0x9A => "f64.neg", 0x9B => "f64.ceil", 0x9C => "f64.floor", 0x9D => "f64.trunc", 0x9E => "f64.nearest",
        0x9F => "f64.sqrt", 0xA0 => "f64.add", 0xA1 => "f64.sub", 0xA2 => "f64.mul", 0xA3 => "f64.div",
        0xA4 => "f64.min", 0xA5 => "f64.max", 0xA6 => "f64.copysign", _ => $"op_0x{op:x2}",
    };

    private static string MiscName(ulong sub) => sub switch
    {
        0 => "i32.trunc_sat_f32_s", 1 => "i32.trunc_sat_f32_u", 2 => "i32.trunc_sat_f64_s",
        3 => "i32.trunc_sat_f64_u", 4 => "i64.trunc_sat_f32_s", 5 => "i64.trunc_sat_f32_u",
        6 => "i64.trunc_sat_f64_s", 7 => "i64.trunc_sat_f64_u", 8 => "memory.init",
        9 => "data.drop", 10 => "memory.copy", 11 => "memory.fill", 12 => "table.init",
        13 => "elem.drop", 14 => "table.copy", 15 => "table.grow", 16 => "table.size",
        17 => "table.fill", _ => $"misc.{sub}",
    };

    private static string GcName(ulong sub) => sub switch
    {
        0 => "struct.new", 1 => "struct.new_default", 2 => "struct.get",
        3 => "struct.get_s", 4 => "struct.get_u", 5 => "struct.set",
        6 => "array.new", 7 => "array.new_default", 8 => "array.new_fixed",
        9 => "array.new_data", 10 => "array.new_elem", 11 => "array.get",
        12 => "array.get_s", 13 => "array.get_u", 14 => "array.set",
        15 => "array.len", 16 => "array.fill", 17 => "array.copy",
        18 => "array.init_data", 19 => "array.init_elem", 20 => "ref.test",
        21 => "ref.test_null", 22 => "ref.cast", 23 => "ref.cast_null",
        24 => "br_on_cast", 25 => "br_on_cast_fail", 26 => "any.convert_extern",
        27 => "extern.convert_any", 28 => "ref.i31", 29 => "i31.get_s",
        30 => "i31.get_u", _ => $"gc.{sub}",
    };

    private static string SimdName(ulong sub) => sub switch
    {
        0 => "v128.load", 1 => "v128.load8x8_s", 2 => "v128.load8x8_u", 3 => "v128.load16x4_s",
        4 => "v128.load16x4_u", 5 => "v128.load32x2_s", 6 => "v128.load32x2_u", 7 => "v128.load8_splat",
        8 => "v128.load16_splat", 9 => "v128.load32_splat", 10 => "v128.load64_splat", 11 => "v128.store",
        12 => "v128.const", 13 => "i8x16.shuffle", 14 => "i8x16.swizzle", 15 => "i8x16.splat",
        16 => "i16x8.splat", 17 => "i32x4.splat", 18 => "i64x2.splat", 19 => "f32x4.splat",
        20 => "f64x2.splat", 21 => "i8x16.extract_lane_s", 22 => "i8x16.extract_lane_u",
        23 => "i8x16.replace_lane", 24 => "i16x8.extract_lane_s", 25 => "i16x8.extract_lane_u",
        26 => "i16x8.replace_lane", 27 => "i32x4.extract_lane", 28 => "i32x4.replace_lane",
        29 => "i64x2.extract_lane", 30 => "i64x2.replace_lane", 31 => "f32x4.extract_lane",
        32 => "f32x4.replace_lane", 33 => "f64x2.extract_lane", 34 => "f64x2.replace_lane",
        35 => "i8x16.eq", 36 => "i8x16.ne", 37 => "i8x16.lt_s", 38 => "i8x16.lt_u",
        39 => "i8x16.gt_s", 40 => "i8x16.gt_u", 41 => "i8x16.le_s", 42 => "i8x16.le_u",
        43 => "i8x16.ge_s", 44 => "i8x16.ge_u", 77 => "v128.not", 78 => "v128.and",
        79 => "v128.andnot", 80 => "v128.or", 81 => "v128.xor", 82 => "v128.bitselect",
        83 => "v128.any_true", 84 => "v128.load8_lane", 85 => "v128.load16_lane",
        86 => "v128.load32_lane", 87 => "v128.load64_lane", 88 => "v128.store8_lane",
        89 => "v128.store16_lane", 90 => "v128.store32_lane", 91 => "v128.store64_lane",
        92 => "v128.load32_zero", 93 => "v128.load64_zero", 96 => "i8x16.abs",
        97 => "i8x16.neg", 98 => "i8x16.popcnt", 99 => "i8x16.all_true",
        100 => "i8x16.bitmask", 107 => "i8x16.shl", 108 => "i8x16.shr_s",
        109 => "i8x16.shr_u", 110 => "i8x16.add", 111 => "i8x16.add_sat_s",
        112 => "i8x16.add_sat_u", 113 => "i8x16.sub", 114 => "i8x16.sub_sat_s",
        115 => "i8x16.sub_sat_u", 118 => "i8x16.min_s", 119 => "i8x16.min_u",
        120 => "i8x16.max_s", 121 => "i8x16.max_u", 123 => "i8x16.avgr_u",
        224 => "f32x4.abs", 225 => "f32x4.neg", 227 => "f32x4.sqrt", 228 => "f32x4.add",
        229 => "f32x4.sub", 230 => "f32x4.mul", 231 => "f32x4.div", 232 => "f32x4.min",
        233 => "f32x4.max", 236 => "f64x2.abs", 237 => "f64x2.neg", 239 => "f64x2.sqrt",
        240 => "f64x2.add", 241 => "f64x2.sub", 242 => "f64x2.mul", 243 => "f64x2.div",
        244 => "f64x2.min", 245 => "f64x2.max", _ => $"simd.{sub}",
    };

    private static (long Value, string Text) ReadBlockType(ReadOnlySpan<byte> code, ref int pos)
    {
        var type = ReadU8(code, ref pos);
        return (type, BlockType(type));
    }

    private static string BlockType(byte type) => type switch
    {
        0x40 => "empty",
        0x7F => "i32",
        0x7E => "i64",
        0x7D => "f32",
        0x7C => "f64",
        0x7B => "v128",
        _ => $"type {type}",
    };

    private static string ValueTypeName(byte type) => type switch
    {
        0x7F => "i32",
        0x7E => "i64",
        0x7D => "f32",
        0x7C => "f64",
        0x7B => "v128",
        0x70 => "funcref",
        0x6F => "externref",
        _ => $"type 0x{type:x2}",
    };

    private static byte ReadU8(ReadOnlySpan<byte> code, ref int pos)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pos, code.Length);
        return code[pos++];
    }

    private static NativeInstruction Simple(
        ReadOnlySpan<byte> code,
        int start,
        int pos,
        ulong address,
        string mnemonic,
        IReadOnlyList<NativeOperand> operands,
        NativeInstructionCategory category = NativeInstructionCategory.Integer,
        NativeFlowKind flow = NativeFlowKind.Sequential) =>
        NativeDecoderSupport.Build(
            address, code[start..pos].ToArray(), mnemonic, operands,
            category, flow);
}
