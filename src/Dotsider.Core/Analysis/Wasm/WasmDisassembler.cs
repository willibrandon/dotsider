using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Disassembles WebAssembly function bodies with function-index-aware target naming.
/// </summary>
internal static class WasmDisassembler
{
    /// <summary>
    /// Disassembles one Wasm function symbol from its module.
    /// </summary>
    /// <param name="analyzer">The analyzer that opened the raw WebAssembly module.</param>
    /// <param name="symbol">The file-backed Wasm function symbol to disassemble.</param>
    /// <returns>A rendered listing, decoded instructions, and header line count, or null.</returns>
    public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)?
        DisassembleSymbol(AssemblyAnalyzer analyzer, NativeSymbol symbol)
    {
        if (analyzer.WasmModuleInfo is not { } module || symbol.FileOffset is not { } fileOffset || symbol.Size <= 0)
            return null;

        var raw = analyzer.RawBytes.Span;
        if (fileOffset < 0 || fileOffset + symbol.Size > raw.Length)
            return null;

        var functionByIndex = module.Functions.ToDictionary(static f => f.Index);
        var function = module.Functions.FirstOrDefault(f => f.CodeOffset == fileOffset);
        var code = raw.Slice((int)fileOffset, (int)symbol.Size);
        var decoded = NativeDisassembler.Disassemble(code, symbol.VirtualAddress, NativeArchitecture.Wasm32);
        var instructions = new List<NativeInstruction>(decoded.Count);

        foreach (var insn in decoded)
        {
            var withOffset = insn with
            {
                FileOffset = fileOffset + (long)(insn.Address - symbol.VirtualAddress),
            };

            instructions.Add(ResolveWasmTarget(withOffset, module, function, functionByIndex));
        }

        var header = function is null
            ? symbol.Name
            : $"func[{function.Index}] {function.Name}\n// type: {FormatSignature(function)}";
        return NativeDisassembler.Render(instructions, header);
    }

    private static NativeInstruction ResolveWasmTarget(
        NativeInstruction instruction,
        WasmModuleInfo module,
        WasmFunctionInfo? function,
        Dictionary<int, WasmFunctionInfo> functions)
    {
        return instruction.Mnemonic switch
        {
            "call" or "return_call" => ResolveDirectCall(instruction, functions),
            "call_indirect" or "return_call_indirect" => AnnotateIndirectCall(instruction, module),
            "call_ref" or "return_call_ref" => AnnotateTypeOperand(instruction, module, 0),
            "local.get" or "local.set" or "local.tee" => AnnotateLocalOperand(instruction, function),
            "global.get" or "global.set" => AnnotateGlobalOperand(instruction, module),
            "table.get" or "table.set" => AnnotateTableOperand(instruction, module),
            _ => instruction,
        };
    }

    private static NativeInstruction ResolveDirectCall(
        NativeInstruction instruction,
        Dictionary<int, WasmFunctionInfo> functions)
    {
        if (!TryGetOperandIndex(instruction, 0, out var rawIndex)
            || !functions.TryGetValue(rawIndex, out var target))
            return instruction;

        var operand = instruction.Operands[0];
        var namedOperand = operand with { Text = $"{operand.Text} <{target.Name}>" };
        var operands = instruction.Operands.ToArray();
        operands[0] = namedOperand;

        if (target.IsImported)
        {
            return instruction with
            {
                Operands = operands,
                OperandText = string.Join(", ", operands.Select(static op => op.Text)),
                TargetKind = NativeTargetKind.Import,
                TargetName = target.Name,
            };
        }

        return instruction with
        {
            Operands = operands,
            OperandText = string.Join(", ", operands.Select(static op => op.Text)),
            TargetAddress = target.CodeOffset is { } offset ? (ulong)offset : null,
            TargetKind = NativeTargetKind.Function,
            TargetName = target.Name,
        };
    }

    private static NativeInstruction AnnotateIndirectCall(NativeInstruction instruction, WasmModuleInfo module)
    {
        var current = AnnotateTypeOperand(instruction, module, 0);
        return AnnotateTableOperand(current, module, operandIndex: 1);
    }

    private static NativeInstruction AnnotateTypeOperand(
        NativeInstruction instruction, WasmModuleInfo module, int operandIndex)
    {
        if (!TryGetOperandIndex(instruction, operandIndex, out var typeIndex))
            return instruction;
        var type = module.Types.FirstOrDefault(t => t.Index == typeIndex);
        if (type is null)
            return instruction;

        return ReplaceOperandText(instruction, operandIndex,
            text => $"{text} <{FormatSignature(type.ParamTypes, type.ResultTypes)}>");
    }

    private static NativeInstruction AnnotateLocalOperand(
        NativeInstruction instruction, WasmFunctionInfo? function)
    {
        if (function is null || !TryGetOperandIndex(instruction, 0, out var index))
            return instruction;

        var paramCount = function.ParamTypes.Count;
        if (index < paramCount)
            return ReplaceOperandText(instruction, 0,
                text => $"{text} <param {index}: {ValueTypeName(function.ParamTypes[index])}>");

        var localIndex = index - paramCount;
        foreach (var localRun in function.Locals)
        {
            if (localIndex >= 0 && (uint)localIndex < localRun.Count)
                return ReplaceOperandText(instruction, 0,
                    text => $"{text} <local {index}: {localRun.DisplayType}>");
            localIndex -= checked((int)localRun.Count);
        }

        return instruction;
    }

    private static NativeInstruction AnnotateGlobalOperand(NativeInstruction instruction, WasmModuleInfo module)
    {
        if (!TryGetOperandIndex(instruction, 0, out var index))
            return instruction;

        var imported = module.Imports.FirstOrDefault(i => i.Kind == WasmExternalKind.Global && i.Index == index);
        if (imported is not null)
            return ReplaceOperandText(instruction, 0, text => $"{text} <{imported.ModuleName}!{imported.Name}>");

        var defined = module.Globals.FirstOrDefault(g => g.Index == index);
        if (defined is not null)
            return ReplaceOperandText(instruction, 0,
                text => $"{text} <global {index}: {defined.ValueTypeName}{(defined.IsMutable ? " mutable" : "")}>");

        return instruction;
    }

    private static NativeInstruction AnnotateTableOperand(
        NativeInstruction instruction, WasmModuleInfo module, int operandIndex = 0)
    {
        if (!TryGetOperandIndex(instruction, operandIndex, out var index))
            return instruction;

        var imported = module.Imports.FirstOrDefault(i => i.Kind == WasmExternalKind.Table && i.Index == index);
        if (imported is not null)
            return ReplaceOperandText(instruction, operandIndex, text => $"{text} <{imported.ModuleName}!{imported.Name}>");

        var table = module.Tables.FirstOrDefault(t => t.Index == index);
        if (table is not null)
            return ReplaceOperandText(instruction, operandIndex,
                text => $"{text} <table {index}: {table.RefType}>");

        return instruction;
    }

    private static NativeInstruction ReplaceOperandText(
        NativeInstruction instruction, int operandIndex, Func<string, string> replace)
    {
        if (operandIndex < 0 || operandIndex >= instruction.Operands.Count)
            return instruction;

        var operands = instruction.Operands.ToArray();
        operands[operandIndex] = operands[operandIndex] with { Text = replace(operands[operandIndex].Text) };
        return instruction with
        {
            Operands = operands,
            OperandText = string.Join(", ", operands.Select(static op => op.Text)),
        };
    }

    private static bool TryGetOperandIndex(NativeInstruction instruction, int operandIndex, out int index)
    {
        index = 0;
        if (operandIndex < 0
            || operandIndex >= instruction.Operands.Count
            || instruction.Operands[operandIndex].Immediate is not { } rawIndex
            || rawIndex < 0
            || rawIndex > int.MaxValue)
        {
            return false;
        }

        index = (int)rawIndex;
        return true;
    }

    private static string FormatSignature(WasmFunctionInfo function)
        => FormatSignature(function.ParamTypes, function.ResultTypes);

    private static string FormatSignature(IReadOnlyList<byte> paramTypes, IReadOnlyList<byte> resultTypes)
    {
        var parameters = string.Join(", ", paramTypes.Select(ValueTypeName));
        var results = string.Join(", ", resultTypes.Select(ValueTypeName));
        return $"({parameters}) -> ({results})";
    }

    private static string ValueTypeName(byte valueType) => valueType switch
    {
        0x7F => "i32",
        0x7E => "i64",
        0x7D => "f32",
        0x7C => "f64",
        0x7B => "v128",
        0x70 => "funcref",
        0x6F => "externref",
        _ => $"0x{valueType:X2}",
    };
}
