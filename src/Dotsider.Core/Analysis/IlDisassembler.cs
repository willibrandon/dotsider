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
        var debugInfo = analyzer.GetMethodDebugInfo(method);
        var sequencePointsByOffset = debugInfo.SequencePoints
            .GroupBy(point => point.Offset)
            .ToDictionary(group => group.Key, group => group.First());

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

            var operandStart = offset;
            var operand = DecodeOperand(ilBytes, ref offset, opCode);
            var localSlot = TryGetLocalSlot(opCode, ilBytes, operandStart);
            var localName = localSlot is null
                ? null
                : ResolveLocalName(debugInfo.Locals, localSlot.Value, instructionOffset);
            if (!string.IsNullOrEmpty(localName))
                operand = string.IsNullOrEmpty(operand) ? $"// {localName}" : $"{operand} // {localName}";

            int? metadataToken = null;
            var operandKind = GetOperandType(opCode);
            if (operandKind is OperandKind.InlineMethod or OperandKind.InlineField
                or OperandKind.InlineType or OperandKind.InlineTok
                && operandStart + 4 <= ilBytes.Length)
            {
                metadataToken = BitConverter.ToInt32(ilBytes, operandStart);
            }

            sequencePointsByOffset.TryGetValue(instructionOffset, out var sequencePoint);
            instructions.Add(new IlInstruction(
                Offset: instructionOffset,
                OpCode: FormatOpCode(opCode),
                Operand: operand,
                MetadataToken: metadataToken,
                SequenceDocument: sequencePoint?.Document,
                SequenceStartLine: sequencePoint?.StartLine,
                SequenceStartColumn: sequencePoint?.StartColumn,
                SequenceEndLine: sequencePoint?.EndLine,
                SequenceEndColumn: sequencePoint?.EndColumn,
                SequenceHidden: sequencePoint?.IsHidden == true,
                SourceLinkUrl: sequencePoint?.SourceLinkUrl,
                HasEmbeddedSource: sequencePoint?.HasEmbeddedSource == true,
                LocalSlot: localSlot,
                LocalName: localName));
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
        return DisassembleWithText(method)?.Text
            ?? "// No IL body (abstract, extern, or native method)";
    }

    /// <summary>
    /// Disassembles a method and returns the text, instruction list, and header line count.
    /// </summary>
    /// <param name="method">The method to disassemble.</param>
    /// <returns>Tuple of (text, instructions, headerLineCount), or null if no IL body.</returns>
    public (string Text, IReadOnlyList<IlInstruction> Instructions, int HeaderLineCount)? DisassembleWithText(
        MethodDefInfo method)
    {
        var body = analyzer.GetMethodBody(method);
        if (body is null) return null;

        var instructions = Disassemble(method);
        var debugInfo = analyzer.GetMethodDebugInfo(method);
        var localTypes = DecodeLocalTypes(body.LocalSignature);
        var lines = BuildHeaderLines(method, body, debugInfo, localTypes);
        lines.Add("");

        var headerLineCount = lines.Count;
        var renderedInstructions = new List<IlInstruction>(instructions.Count);
        var seenSourceLinkUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in instructions)
        {
            if (inst.SequenceStartLine is not null)
            {
                var showSourceLinkMarker = ShouldShowSourceLinkMarker(inst, seenSourceLinkUrls);
                lines.Add(FormatSequencePointComment(inst, showSourceLinkMarker));
            }

            var operandPart = string.IsNullOrEmpty(inst.Operand) ? "" : $" {inst.Operand}";
            var displayLine = lines.Count + 1;
            lines.Add($"IL_{inst.Offset:X4}: {inst.OpCode}{operandPart}");
            renderedInstructions.Add(inst with { DisplayLine = displayLine });
        }
        return (string.Join('\n', lines), renderedInstructions, headerLineCount);
    }

    private static bool ShouldShowSourceLinkMarker(
        IlInstruction instruction,
        HashSet<string> seenSourceLinkUrls)
    {
        return !instruction.SequenceHidden
            && !string.IsNullOrEmpty(instruction.SourceLinkUrl)
            && seenSourceLinkUrls.Add(instruction.SourceLinkUrl);
    }

    /// <summary>
    /// Returns the number of header lines for a method's disassembly listing.
    /// </summary>
    /// <param name="method">The method to compute header lines for.</param>
    /// <returns>The number of header lines, or 0 if no IL body.</returns>
    public int GetHeaderLineCount(MethodDefInfo method)
    {
        var body = analyzer.GetMethodBody(method);
        if (body is null) return 0;
        var debugInfo = analyzer.GetMethodDebugInfo(method);
        var localTypes = DecodeLocalTypes(body.LocalSignature);
        return BuildHeaderLines(method, body, debugInfo, localTypes).Count + 1;
    }

    private List<string> BuildHeaderLines(
        MethodDefInfo method,
        MethodBodyBlock body,
        MethodDebugInfo debugInfo,
        IReadOnlyList<string> localTypes)
    {
        var lines = new List<string>
        {
            $"// Method: {method.DeclaringType}::{method.Name}",
            $"// Signature: {method.Signature}",
            $"// RVA: 0x{method.Rva:X8}",
            $"// Code size: {body.GetILBytes()?.Length ?? 0} bytes",
            $"// Max stack: {body.MaxStack}",
            $"// PDB: {analyzer.PdbProvenance}",
        };

        if (analyzer.SourceLink.IsPresent)
            lines.Add($"// Source Link: present, {analyzer.SourceLink.Mappings.Count} mappings");

        AddLocalSignatureLines(lines, body, debugInfo.Locals, localTypes);
        return lines;
    }

    private static void AddLocalSignatureLines(
        List<string> lines,
        MethodBodyBlock body,
        IReadOnlyList<LocalSlotInfo> locals,
        IReadOnlyList<string> localTypes)
    {
        var highestPdbSlot = locals.Count == 0 ? -1 : locals.Max(local => local.Slot);
        var localCount = Math.Max(localTypes.Count, highestPdbSlot + 1);
        if (body.LocalSignature.IsNil && localCount == 0)
            return;

        lines.Add(body.LocalVariablesInitialized ? ".locals init (" : ".locals (");
        for (var slot = 0; slot < localCount; slot++)
        {
            var type = slot < localTypes.Count ? localTypes[slot] : "?";
            var name = locals.FirstOrDefault(local => local.Slot == slot)?.Name ?? $"V_{slot}";
            var comma = slot == localCount - 1 ? "" : ",";
            lines.Add($"    [{slot}] {type} {name}{comma}");
        }
        lines.Add(")");
    }

    private IReadOnlyList<string> DecodeLocalTypes(StandaloneSignatureHandle localSignature)
    {
        if (localSignature.IsNil) return [];

        try
        {
            var signature = _reader.GetStandaloneSignature(localSignature);
            return [.. signature.DecodeLocalSignature(
                new AssemblyAnalyzer.SignatureTypeProvider(),
                genericContext: default)];
        }
        catch
        {
            return [];
        }
    }

    private static string FormatSequencePointComment(
        IlInstruction instruction,
        bool showSourceLinkMarker)
    {
        if (instruction.SequenceHidden)
            return "// (hidden)";

        var document = instruction.SequenceDocument is { Length: > 0 }
            ? Path.GetFileName(instruction.SequenceDocument)
            : "(unknown)";
        var line = $"// {document}({instruction.SequenceStartLine},{instruction.SequenceStartColumn})"
            + $"-({instruction.SequenceEndLine},{instruction.SequenceEndColumn})";
        if (showSourceLinkMarker)
            line += " [source link]";
        if (instruction.HasEmbeddedSource)
            line += " [embedded source]";
        return line;
    }

    private static string? ResolveLocalName(IReadOnlyList<LocalSlotInfo> locals, int slot, int offset)
    {
        return locals
            .Where(local => local.Slot == slot
                && local.StartOffset <= offset
                && offset < local.EndOffset
                && !local.IsDebuggerHidden)
            .OrderBy(local => local.EndOffset - local.StartOffset)
            .ThenByDescending(local => local.StartOffset)
            .Select(local => local.Name)
            .FirstOrDefault();
    }

    private static int? TryGetLocalSlot(ILOpCode opCode, byte[] ilBytes, int operandStart)
    {
        return opCode switch
        {
            ILOpCode.Ldloc_0 or ILOpCode.Stloc_0 => 0,
            ILOpCode.Ldloc_1 or ILOpCode.Stloc_1 => 1,
            ILOpCode.Ldloc_2 or ILOpCode.Stloc_2 => 2,
            ILOpCode.Ldloc_3 or ILOpCode.Stloc_3 => 3,
            ILOpCode.Ldloc_s or ILOpCode.Ldloca_s or ILOpCode.Stloc_s
                when operandStart < ilBytes.Length => ilBytes[operandStart],
            ILOpCode.Ldloc or ILOpCode.Ldloca or ILOpCode.Stloc
                when operandStart + 2 <= ilBytes.Length => BitConverter.ToUInt16(ilBytes, operandStart),
            _ => null
        };
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

    private static string DecodeSwitch(byte[] ilBytes, ref int offset)
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

    internal static sbyte ReadSByte(byte[] il, ref int offset) => (sbyte)il[offset++];
    internal static byte ReadByte(byte[] il, ref int offset) => il[offset++];
    internal static ushort ReadUInt16(byte[] il, ref int offset)
    {
        var v = BitConverter.ToUInt16(il, offset);
        offset += 2;
        return v;
    }
    internal static int ReadInt32(byte[] il, ref int offset)
    {
        var v = BitConverter.ToInt32(il, offset);
        offset += 4;
        return v;
    }
    internal static long ReadInt64(byte[] il, ref int offset)
    {
        var v = BitConverter.ToInt64(il, offset);
        offset += 8;
        return v;
    }
    internal static float ReadSingle(byte[] il, ref int offset)
    {
        var v = BitConverter.ToSingle(il, offset);
        offset += 4;
        return v;
    }
    internal static double ReadDouble(byte[] il, ref int offset)
    {
        var v = BitConverter.ToDouble(il, offset);
        offset += 8;
        return v;
    }

    internal enum OperandKind
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

    internal static OperandKind GetOperandType(ILOpCode opCode) => opCode switch
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

        ILOpCode.Ldc_i4_s or ILOpCode.Unaligned
            => OperandKind.ShortInlineI,

        ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s
            or ILOpCode.Ldloc_s or ILOpCode.Ldloca_s or ILOpCode.Stloc_s
            => OperandKind.ShortInlineVar,

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
            or ILOpCode.Unbox_any or ILOpCode.Ldelema or ILOpCode.Ldelem or ILOpCode.Stelem
            or ILOpCode.Ldobj or ILOpCode.Stobj
            or ILOpCode.Cpobj or ILOpCode.Initobj or ILOpCode.Constrained or ILOpCode.Sizeof
            or ILOpCode.Mkrefany or ILOpCode.Refanyval
            => OperandKind.InlineType,
        ILOpCode.Ldtoken => OperandKind.InlineTok,
        ILOpCode.Calli => OperandKind.InlineSig,
        ILOpCode.Switch => OperandKind.InlineSwitch,

        _ => OperandKind.None
    };
}
