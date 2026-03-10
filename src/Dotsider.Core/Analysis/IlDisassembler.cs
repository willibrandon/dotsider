using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences.
/// </summary>
public sealed class IlDisassembler(AssemblyAnalyzer analyzer)
{
    private readonly MetadataReader _reader = analyzer.GetMetadataReader()
        ?? throw new InvalidOperationException("Assembly has no .NET metadata.");

    /// <summary>
    /// Disassembles a method's IL body into a sequence of instructions.
    /// Returns an empty list if the method has no IL body.
    /// </summary>
    /// <param name="method">The method to disassemble.</param>
    /// <returns>The list of decoded IL instructions.</returns>
    public IReadOnlyList<IlInstruction> Disassemble(MethodDefInfo method)
    {
        var body = analyzer.GetMethodBody(method);
        if (body is null) return [];

        var instructions = new List<IlInstruction>();
        var ilBytes = body.GetILBytes();
        if (ilBytes is null) return [];

        var offset = 0;
        while (offset < ilBytes.Length)
        {
            var instructionOffset = offset;
            var opCodeByte = ilBytes[offset++];

            ILOpCode opCode;
            if (opCodeByte == 0xFE)
            {
                if (offset >= ilBytes.Length) break;
                var secondByte = ilBytes[offset++];
                opCode = (ILOpCode)(0xFE00 | secondByte);
            }
            else
            {
                opCode = (ILOpCode)opCodeByte;
            }

            var operand = DecodeOperand(ilBytes, ref offset, opCode);

            instructions.Add(new IlInstruction(
                Offset: instructionOffset,
                OpCode: FormatOpCode(opCode),
                Operand: operand));
        }

        return instructions;
    }

    /// <summary>
    /// Formats a complete disassembly listing for a method, including header information.
    /// </summary>
    /// <param name="method">The method to disassemble.</param>
    /// <returns>A multi-line string with the full disassembly listing.</returns>
    public string FormatDisassembly(MethodDefInfo method)
    {
        var body = analyzer.GetMethodBody(method);
        if (body is null) return "// No IL body (abstract, extern, or native method)";

        var lines = new List<string>();

        // Method header info
        lines.Add($"// Method: {method.DeclaringType}::{method.Name}");
        lines.Add($"// Signature: {method.Signature}");
        lines.Add($"// RVA: 0x{method.Rva:X8}");
        lines.Add($"// Code size: {body.GetILBytes()?.Length ?? 0} bytes");
        lines.Add($"// Max stack: {body.MaxStack}");
        if (body.LocalSignature.IsNil is false)
        {
            lines.Add($"// Locals init: {!body.LocalVariablesInitialized}");
        }
        lines.Add("");

        var instructions = Disassemble(method);
        foreach (var inst in instructions)
        {
            var operandPart = string.IsNullOrEmpty(inst.Operand) ? "" : $" {inst.Operand}";
            lines.Add($"IL_{inst.Offset:X4}: {inst.OpCode}{operandPart}");
        }

        return string.Join('\n', lines);
    }

    private string DecodeOperand(byte[] ilBytes, ref int offset, ILOpCode opCode)
    {
        return GetOperandType(opCode) switch
        {
            OperandKind.None => "",
            OperandKind.ShortBranchTarget => FormatBranchTarget(offset + 1 + ReadSByte(ilBytes, ref offset)),
            OperandKind.BranchTarget => FormatBranchTarget(offset + 4 + ReadInt32(ilBytes, ref offset)),
            OperandKind.ShortInlineI => ReadSByte(ilBytes, ref offset).ToString(),
            OperandKind.InlineI => ReadInt32(ilBytes, ref offset).ToString(),
            OperandKind.InlineI8 => ReadInt64(ilBytes, ref offset).ToString(),
            OperandKind.ShortInlineR => ReadSingle(ilBytes, ref offset).ToString("G"),
            OperandKind.InlineR => ReadDouble(ilBytes, ref offset).ToString("G"),
            OperandKind.ShortInlineVar => ReadByte(ilBytes, ref offset).ToString(),
            OperandKind.InlineVar => ReadUInt16(ilBytes, ref offset).ToString(),
            OperandKind.InlineString => ResolveStringToken(ReadInt32(ilBytes, ref offset)),
            OperandKind.InlineMethod or OperandKind.InlineField or OperandKind.InlineType or OperandKind.InlineTok
                => analyzer.ResolveToken(ReadInt32(ilBytes, ref offset)),
            OperandKind.InlineSig => $"StandaloneSig(0x{ReadInt32(ilBytes, ref offset):X8})",
            OperandKind.InlineSwitch => DecodeSwitch(ilBytes, ref offset),
            _ => ""
        };
    }

    private string ResolveStringToken(int token)
    {
        try
        {
            var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
            var s = _reader.GetUserString(handle);
            return s.Length > 60 ? $"\"{s[..60]}...\"" : $"\"{s}\"";
        }
        catch
        {
            return $"0x{token:X8}";
        }
    }

    private string DecodeSwitch(byte[] ilBytes, ref int offset)
    {
        var count = ReadInt32(ilBytes, ref offset);
        if (count <= 0 || count > 1000) return $"({count} targets)";

        var baseOffset = offset + count * 4;
        var targets = new List<string>();
        for (var i = 0; i < count && i < 10; i++)
        {
            var target = baseOffset + ReadInt32(ilBytes, ref offset);
            targets.Add(FormatBranchTarget(target));
        }
        // Skip remaining targets if more than 10
        if (count > 10)
        {
            offset += (count - 10) * 4;
            targets.Add($"... ({count - 10} more)");
        }
        return $"({string.Join(", ", targets)})";
    }

    private static string FormatBranchTarget(int target) => $"IL_{target:X4}";
    private static string FormatOpCode(ILOpCode opCode) => opCode.ToString().ToLowerInvariant().Replace('_', '.');

    private static sbyte ReadSByte(byte[] il, ref int offset) => (sbyte)il[offset++];
    private static byte ReadByte(byte[] il, ref int offset) => il[offset++];
    private static ushort ReadUInt16(byte[] il, ref int offset)
    {
        var v = BitConverter.ToUInt16(il, offset);
        offset += 2;
        return v;
    }
    private static int ReadInt32(byte[] il, ref int offset)
    {
        var v = BitConverter.ToInt32(il, offset);
        offset += 4;
        return v;
    }
    private static long ReadInt64(byte[] il, ref int offset)
    {
        var v = BitConverter.ToInt64(il, offset);
        offset += 8;
        return v;
    }
    private static float ReadSingle(byte[] il, ref int offset)
    {
        var v = BitConverter.ToSingle(il, offset);
        offset += 4;
        return v;
    }
    private static double ReadDouble(byte[] il, ref int offset)
    {
        var v = BitConverter.ToDouble(il, offset);
        offset += 8;
        return v;
    }

    private enum OperandKind
    {
        None,
        ShortBranchTarget,
        BranchTarget,
        ShortInlineI,
        InlineI,
        InlineI8,
        ShortInlineR,
        InlineR,
        ShortInlineVar,
        InlineVar,
        InlineString,
        InlineMethod,
        InlineField,
        InlineType,
        InlineTok,
        InlineSig,
        InlineSwitch
    }

    private static OperandKind GetOperandType(ILOpCode opCode) => opCode switch
    {
        ILOpCode.Nop or ILOpCode.Break or ILOpCode.Ldarg_0 or ILOpCode.Ldarg_1 or ILOpCode.Ldarg_2 or ILOpCode.Ldarg_3
            or ILOpCode.Ldloc_0 or ILOpCode.Ldloc_1 or ILOpCode.Ldloc_2 or ILOpCode.Ldloc_3
            or ILOpCode.Stloc_0 or ILOpCode.Stloc_1 or ILOpCode.Stloc_2 or ILOpCode.Stloc_3
            or ILOpCode.Ldnull or ILOpCode.Ldc_i4_m1
            or ILOpCode.Ldc_i4_0 or ILOpCode.Ldc_i4_1 or ILOpCode.Ldc_i4_2 or ILOpCode.Ldc_i4_3
            or ILOpCode.Ldc_i4_4 or ILOpCode.Ldc_i4_5 or ILOpCode.Ldc_i4_6 or ILOpCode.Ldc_i4_7 or ILOpCode.Ldc_i4_8
            or ILOpCode.Dup or ILOpCode.Pop or ILOpCode.Ret
            or ILOpCode.Ldind_i1 or ILOpCode.Ldind_u1 or ILOpCode.Ldind_i2 or ILOpCode.Ldind_u2
            or ILOpCode.Ldind_i4 or ILOpCode.Ldind_u4 or ILOpCode.Ldind_i8 or ILOpCode.Ldind_i
            or ILOpCode.Ldind_r4 or ILOpCode.Ldind_r8 or ILOpCode.Ldind_ref
            or ILOpCode.Stind_ref or ILOpCode.Stind_i1 or ILOpCode.Stind_i2 or ILOpCode.Stind_i4 or ILOpCode.Stind_i8
            or ILOpCode.Stind_r4 or ILOpCode.Stind_r8
            or ILOpCode.Add or ILOpCode.Sub or ILOpCode.Mul or ILOpCode.Div or ILOpCode.Div_un
            or ILOpCode.Rem or ILOpCode.Rem_un or ILOpCode.And or ILOpCode.Or or ILOpCode.Xor
            or ILOpCode.Shl or ILOpCode.Shr or ILOpCode.Shr_un or ILOpCode.Neg or ILOpCode.Not
            or ILOpCode.Conv_i1 or ILOpCode.Conv_i2 or ILOpCode.Conv_i4 or ILOpCode.Conv_i8
            or ILOpCode.Conv_r4 or ILOpCode.Conv_r8 or ILOpCode.Conv_u4 or ILOpCode.Conv_u8
            or ILOpCode.Conv_r_un
            or ILOpCode.Throw or ILOpCode.Conv_ovf_i1_un or ILOpCode.Conv_ovf_i2_un
            or ILOpCode.Conv_ovf_i4_un or ILOpCode.Conv_ovf_i8_un or ILOpCode.Conv_ovf_u1_un
            or ILOpCode.Conv_ovf_u2_un or ILOpCode.Conv_ovf_u4_un or ILOpCode.Conv_ovf_u8_un
            or ILOpCode.Conv_ovf_i_un or ILOpCode.Conv_ovf_u_un
            or ILOpCode.Ldlen or ILOpCode.Ldelem_i1 or ILOpCode.Ldelem_u1
            or ILOpCode.Ldelem_i2 or ILOpCode.Ldelem_u2 or ILOpCode.Ldelem_i4 or ILOpCode.Ldelem_u4
            or ILOpCode.Ldelem_i8 or ILOpCode.Ldelem_i or ILOpCode.Ldelem_r4 or ILOpCode.Ldelem_r8
            or ILOpCode.Ldelem_ref
            or ILOpCode.Stelem_i or ILOpCode.Stelem_i1 or ILOpCode.Stelem_i2 or ILOpCode.Stelem_i4
            or ILOpCode.Stelem_i8 or ILOpCode.Stelem_r4 or ILOpCode.Stelem_r8 or ILOpCode.Stelem_ref
            or ILOpCode.Conv_ovf_i1 or ILOpCode.Conv_ovf_u1 or ILOpCode.Conv_ovf_i2 or ILOpCode.Conv_ovf_u2
            or ILOpCode.Conv_ovf_i4 or ILOpCode.Conv_ovf_u4 or ILOpCode.Conv_ovf_i8 or ILOpCode.Conv_ovf_u8
            or ILOpCode.Conv_u2 or ILOpCode.Conv_u1 or ILOpCode.Conv_i or ILOpCode.Conv_ovf_i or ILOpCode.Conv_ovf_u
            or ILOpCode.Add_ovf or ILOpCode.Add_ovf_un or ILOpCode.Mul_ovf or ILOpCode.Mul_ovf_un
            or ILOpCode.Sub_ovf or ILOpCode.Sub_ovf_un or ILOpCode.Endfinally or ILOpCode.Stind_i
            or ILOpCode.Conv_u or ILOpCode.Arglist or ILOpCode.Ceq or ILOpCode.Cgt or ILOpCode.Cgt_un
            or ILOpCode.Clt or ILOpCode.Clt_un or ILOpCode.Localloc or ILOpCode.Endfilter
            or ILOpCode.Volatile or ILOpCode.Tail or ILOpCode.Cpblk or ILOpCode.Initblk
            or ILOpCode.Rethrow or ILOpCode.Refanytype or ILOpCode.Readonly
            => OperandKind.None,

        ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s
            or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s or ILOpCode.Ble_s or ILOpCode.Blt_s
            or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s or ILOpCode.Ble_un_s or ILOpCode.Blt_un_s
            or ILOpCode.Leave_s
            => OperandKind.ShortBranchTarget,

        ILOpCode.Br or ILOpCode.Brfalse or ILOpCode.Brtrue
            or ILOpCode.Beq or ILOpCode.Bge or ILOpCode.Bgt or ILOpCode.Ble or ILOpCode.Blt
            or ILOpCode.Bne_un or ILOpCode.Bge_un or ILOpCode.Bgt_un or ILOpCode.Ble_un or ILOpCode.Blt_un
            or ILOpCode.Leave
            => OperandKind.BranchTarget,

        ILOpCode.Ldc_i4_s or ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s
            or ILOpCode.Ldloc_s or ILOpCode.Ldloca_s or ILOpCode.Stloc_s
            or ILOpCode.Unaligned
            => OperandKind.ShortInlineI,

        ILOpCode.Ldc_i4 => OperandKind.InlineI,
        ILOpCode.Ldc_i8 => OperandKind.InlineI8,
        ILOpCode.Ldc_r4 => OperandKind.ShortInlineR,
        ILOpCode.Ldc_r8 => OperandKind.InlineR,
        ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca or ILOpCode.Stloc
            => OperandKind.InlineVar,
        ILOpCode.Ldstr => OperandKind.InlineString,
        ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Newobj or ILOpCode.Ldftn or ILOpCode.Ldvirtftn or ILOpCode.Jmp
            => OperandKind.InlineMethod,
        ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld or ILOpCode.Ldsflda or ILOpCode.Stsfld
            => OperandKind.InlineField,
        ILOpCode.Castclass or ILOpCode.Isinst or ILOpCode.Newarr or ILOpCode.Box or ILOpCode.Unbox
            or ILOpCode.Unbox_any or ILOpCode.Ldelem or ILOpCode.Stelem or ILOpCode.Ldobj or ILOpCode.Stobj
            or ILOpCode.Cpobj or ILOpCode.Initobj or ILOpCode.Constrained or ILOpCode.Sizeof
            or ILOpCode.Mkrefany or ILOpCode.Refanyval
            => OperandKind.InlineType,
        ILOpCode.Ldtoken => OperandKind.InlineTok,
        ILOpCode.Calli => OperandKind.InlineSig,
        ILOpCode.Switch => OperandKind.InlineSwitch,

        _ => OperandKind.None
    };
}
