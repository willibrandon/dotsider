using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Parses WebAssembly modules produced by .NET browser-wasm publishes.
/// </summary>
internal static class WasmModuleReader
{
    private const uint WasmMagic = 0x6D736100;
    private const uint WasmVersion = 1;
    private const uint WebcilMagic = 0x4C496257;

    /// <summary>
    /// Returns true when the bytes start with the WebAssembly binary magic and version.
    /// </summary>
    /// <param name="bytes">The candidate file bytes.</param>
    /// <returns>True when the bytes are a WebAssembly 1.0 binary module.</returns>
    public static bool IsWasmModule(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == WasmMagic
        && BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) == WasmVersion;

    /// <summary>
    /// Reads a WebAssembly module, preserving partial results when optional sections are malformed.
    /// </summary>
    /// <param name="bytes">The WebAssembly module bytes.</param>
    /// <param name="filePath">The source path used to locate SDK symbol-map sidecars.</param>
    /// <returns>Parsed module facts, functions, data segments, and sidecar status.</returns>
    public static WasmModuleInfo Read(ReadOnlySpan<byte> bytes, string? filePath)
    {
        if (!IsWasmModule(bytes))
            throw new BadImageFormatException("The file is not a WebAssembly 1.0 module.");

        var sections = new List<WasmSectionInfo>();
        var types = new List<WasmTypeInfo>();
        var imports = new List<WasmImportInfo>();
        var functionImports = new List<WasmImportInfo>();
        var functionTypeIndices = new List<int>();
        var bodies = new List<WasmFunctionBody>();
        var exports = new List<WasmExportInfo>();
        var tables = new List<WasmTableInfo>();
        var memories = new List<WasmMemoryInfo>();
        var globals = new List<WasmGlobalInfo>();
        var elements = new List<WasmElementSegmentInfo>();
        var functionNames = new Dictionary<int, string>();
        var dataSegments = new List<WasmDataSegmentInfo>();
        var tags = new List<WasmTagInfo>();
        var targetFeatures = new List<string>();
        var producers = new List<string>();
        int? startFunctionIndex = null;
        int? dataCount = null;
        string? diagnostic = null;

        var pos = 8;
        while (pos < bytes.Length)
        {
            try
            {
                var sectionId = ReadByte(bytes, ref pos);
                var sectionSize = checked((int)ReadUleb(bytes, ref pos));
                var sectionPayloadOffset = pos;
                var sectionEnd = checked(pos + sectionSize);
                if (sectionEnd > bytes.Length)
                    throw new InvalidDataException("A WebAssembly section extends past the end of the file.");

                var sectionName = StandardSectionName(sectionId);
                if (sectionId == 0)
                {
                    var customPos = sectionPayloadOffset;
                    sectionName = ReadName(bytes, ref customPos, sectionEnd);
                    ParseCustomSection(sectionName, bytes, customPos, sectionEnd, functionNames, targetFeatures, producers);
                }
                else
                {
                    var sectionPos = sectionPayloadOffset;
                    switch (sectionId)
                    {
                        case 1:
                            types = ReadTypeSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 2:
                            ReadImportSection(bytes, ref sectionPos, sectionEnd, imports, functionImports);
                            break;
                        case 3:
                            functionTypeIndices = ReadFunctionSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 4:
                            tables = ReadTableSection(
                                bytes, ref sectionPos, sectionEnd,
                                imports.Count(static i => i.Kind == WasmExternalKind.Table));
                            break;
                        case 5:
                            memories = ReadMemorySection(
                                bytes, ref sectionPos, sectionEnd,
                                imports.Count(static i => i.Kind == WasmExternalKind.Memory));
                            break;
                        case 6:
                            globals = ReadGlobalSection(
                                bytes, ref sectionPos, sectionEnd,
                                imports.Count(static i => i.Kind == WasmExternalKind.Global));
                            break;
                        case 7:
                            exports = ReadExportSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 8:
                            startFunctionIndex = ReadStartSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 9:
                            elements = ReadElementSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 10:
                            bodies = ReadCodeSection(bytes, ref sectionPos, sectionEnd, sectionPayloadOffset);
                            break;
                        case 11:
                            dataSegments = ReadDataSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 12:
                            dataCount = ReadDataCountSection(bytes, ref sectionPos, sectionEnd);
                            break;
                        case 13:
                            tags = ReadTagSection(
                                bytes, ref sectionPos, sectionEnd,
                                imports.Count(static i => i.Kind == WasmExternalKind.Tag));
                            break;
                    }
                }

                sections.Add(new WasmSectionInfo(sectionId, sectionName, sectionPayloadOffset, sectionSize));
                pos = sectionEnd;
            }
            catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
            {
                diagnostic = ex.Message;
                break;
            }
        }

        var (symbolMapPath, symbolMapStatus, symbolMapEntries) = ReadSymbolMap(filePath);
        var functions = BuildFunctions(
            types, functionImports, functionTypeIndices, bodies, exports, functionNames, symbolMapEntries);

        return new WasmModuleInfo(
            Version: (int)WasmVersion,
            Sections: sections,
            Types: types,
            Imports: imports,
            Exports: exports,
            Functions: functions,
            Tables: tables,
            Memories: memories,
            Globals: globals,
            Elements: elements,
            DataSegments: dataSegments,
            Tags: tags,
            StartFunctionIndex: startFunctionIndex,
            DataCount: dataCount,
            TargetFeatures: targetFeatures,
            ProducerFields: producers,
            SymbolMapPath: symbolMapPath,
            SymbolMapStatus: symbolMapStatus,
            SymbolMapEntryCount: symbolMapEntries.Count,
            Diagnostic: diagnostic);
    }

    /// <summary>
    /// Finds a Webcil payload embedded as a passive Wasm data segment.
    /// </summary>
    public static bool TryExtractWebcilPayload(ReadOnlySpan<byte> bytes, out byte[] payload)
    {
        payload = [];
        if (!IsWasmModule(bytes))
            return false;

        var pos = 8;
        while (pos < bytes.Length)
        {
            var sectionId = ReadByte(bytes, ref pos);
            var sectionSize = checked((int)ReadUleb(bytes, ref pos));
            var sectionEnd = checked(pos + sectionSize);
            if (sectionEnd > bytes.Length)
                return false;

            if (sectionId != 11)
            {
                pos = sectionEnd;
                continue;
            }

            var count = checked((int)ReadUleb(bytes, ref pos));
            for (var i = 0; i < count && pos < sectionEnd; i++)
            {
                var mode = checked((int)ReadUleb(bytes, ref pos));
                if (mode == 0)
                    SkipConstExpr(bytes, ref pos, sectionEnd);
                else if (mode == 2)
                {
                    _ = ReadUleb(bytes, ref pos);
                    SkipConstExpr(bytes, ref pos, sectionEnd);
                }

                var size = checked((int)ReadUleb(bytes, ref pos));
                if (pos + size > sectionEnd)
                    return false;

                var segment = bytes[pos..(pos + size)];
                if (IsWebcilPayload(segment))
                {
                    payload = segment.ToArray();
                    return true;
                }

                pos += size;
            }

            return false;
        }

        return false;
    }

    private static List<WasmTypeInfo> ReadTypeSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var types = new List<WasmTypeInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var form = ReadByte(bytes, ref pos);
            if (form != 0x60)
                throw new InvalidDataException("Only WebAssembly function types are supported.");

            var paramCount = checked((int)ReadUleb(bytes, ref pos));
            var parameters = new byte[paramCount];
            for (var p = 0; p < paramCount; p++)
                parameters[p] = ReadByte(bytes, ref pos);

            var resultCount = checked((int)ReadUleb(bytes, ref pos));
            var results = new byte[resultCount];
            for (var r = 0; r < resultCount; r++)
                results[r] = ReadByte(bytes, ref pos);

            types.Add(new WasmTypeInfo(i, parameters, results));
        }

        RequireSectionConsumed(pos, end);
        return types;
    }

    private static void ReadImportSection(
        ReadOnlySpan<byte> bytes, ref int pos, int end,
        List<WasmImportInfo> imports, List<WasmImportInfo> functionImports)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var tableIndex = 0;
        var memoryIndex = 0;
        var globalIndex = 0;
        var tagIndex = 0;
        for (var i = 0; i < count; i++)
        {
            var module = ReadName(bytes, ref pos, end);
            var name = ReadName(bytes, ref pos, end);
            var kindByte = ReadByte(bytes, ref pos);
            var kind = ExternalKind(kindByte);
            var index = kind switch
            {
                WasmExternalKind.Function => functionImports.Count,
                WasmExternalKind.Table => tableIndex++,
                WasmExternalKind.Memory => memoryIndex++,
                WasmExternalKind.Global => globalIndex++,
                WasmExternalKind.Tag => tagIndex++,
                _ => i,
            };

            int? typeIndex = null;
            switch (kind)
            {
                case WasmExternalKind.Function:
                    typeIndex = checked((int)ReadUleb(bytes, ref pos));
                    break;
                case WasmExternalKind.Table:
                    SkipTableType(bytes, ref pos);
                    break;
                case WasmExternalKind.Memory:
                    SkipLimits(bytes, ref pos);
                    break;
                case WasmExternalKind.Global:
                    _ = ReadByte(bytes, ref pos);
                    _ = ReadByte(bytes, ref pos);
                    break;
                case WasmExternalKind.Tag:
                    _ = ReadUleb(bytes, ref pos);
                    typeIndex = checked((int)ReadUleb(bytes, ref pos));
                    break;
                default:
                    throw new InvalidDataException($"Unsupported WebAssembly import kind 0x{kindByte:X2}.");
            }

            var import = new WasmImportInfo(module, name, kind, index, typeIndex);
            imports.Add(import);
            if (kind == WasmExternalKind.Function)
                functionImports.Add(import);
        }

        RequireSectionConsumed(pos, end);
    }

    private static List<int> ReadFunctionSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<int>(count);
        for (var i = 0; i < count; i++)
            result.Add(checked((int)ReadUleb(bytes, ref pos)));

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmTableInfo> ReadTableSection(
        ReadOnlySpan<byte> bytes, ref int pos, int end, int importCount)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmTableInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var refType = ReadRefTypeName(bytes, ref pos);
            var (minimum, maximum, _, _) = ReadLimits(bytes, ref pos);
            result.Add(new WasmTableInfo(importCount + i, refType, minimum, maximum));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmMemoryInfo> ReadMemorySection(
        ReadOnlySpan<byte> bytes, ref int pos, int end, int importCount)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmMemoryInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var (minimum, maximum, isShared, isMemory64) = ReadLimits(bytes, ref pos);
            result.Add(new WasmMemoryInfo(importCount + i, minimum, maximum, isShared, isMemory64));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmGlobalInfo> ReadGlobalSection(
        ReadOnlySpan<byte> bytes, ref int pos, int end, int importCount)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmGlobalInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var valueType = ReadByte(bytes, ref pos);
            var mutable = ReadByte(bytes, ref pos);
            SkipConstExpr(bytes, ref pos, end);
            result.Add(new WasmGlobalInfo(importCount + i, valueType, ValueTypeName(valueType), mutable != 0));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmExportInfo> ReadExportSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmExportInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var name = ReadName(bytes, ref pos, end);
            var kind = ExternalKind(ReadByte(bytes, ref pos));
            var index = checked((int)ReadUleb(bytes, ref pos));
            result.Add(new WasmExportInfo(name, kind, index));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static int ReadStartSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var index = checked((int)ReadUleb(bytes, ref pos));
        RequireSectionConsumed(pos, end);
        return index;
    }

    private static List<WasmElementSegmentInfo> ReadElementSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmElementSegmentInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = checked((int)ReadUleb(bytes, ref pos));
            string mode;
            int? tableIndex = null;
            string elementType;
            int elementCount;

            switch (flags)
            {
                case 0:
                    mode = "active";
                    tableIndex = 0;
                    SkipConstExpr(bytes, ref pos, end);
                    elementType = "funcref";
                    elementCount = SkipFunctionIndexVector(bytes, ref pos);
                    break;
                case 1:
                    mode = "passive";
                    elementType = ElementKindName(ReadByte(bytes, ref pos));
                    elementCount = SkipFunctionIndexVector(bytes, ref pos);
                    break;
                case 2:
                    mode = "active-explicit-table";
                    tableIndex = checked((int)ReadUleb(bytes, ref pos));
                    SkipConstExpr(bytes, ref pos, end);
                    elementType = ElementKindName(ReadByte(bytes, ref pos));
                    elementCount = SkipFunctionIndexVector(bytes, ref pos);
                    break;
                case 3:
                    mode = "declarative";
                    elementType = ElementKindName(ReadByte(bytes, ref pos));
                    elementCount = SkipFunctionIndexVector(bytes, ref pos);
                    break;
                case 4:
                    mode = "active";
                    tableIndex = 0;
                    SkipConstExpr(bytes, ref pos, end);
                    elementType = "funcref";
                    elementCount = SkipExpressionVector(bytes, ref pos, end);
                    break;
                case 5:
                    mode = "passive";
                    elementType = ReadRefTypeName(bytes, ref pos);
                    elementCount = SkipExpressionVector(bytes, ref pos, end);
                    break;
                case 6:
                    mode = "active-explicit-table";
                    tableIndex = checked((int)ReadUleb(bytes, ref pos));
                    SkipConstExpr(bytes, ref pos, end);
                    elementType = ReadRefTypeName(bytes, ref pos);
                    elementCount = SkipExpressionVector(bytes, ref pos, end);
                    break;
                case 7:
                    mode = "declarative";
                    elementType = ReadRefTypeName(bytes, ref pos);
                    elementCount = SkipExpressionVector(bytes, ref pos, end);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported WebAssembly element segment flags {flags}.");
            }

            result.Add(new WasmElementSegmentInfo(i, mode, tableIndex, elementType, elementCount));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmFunctionBody> ReadCodeSection(
        ReadOnlySpan<byte> bytes, ref int pos, int end, int sectionPayloadOffset)
    {
        _ = sectionPayloadOffset;
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmFunctionBody>(count);
        for (var i = 0; i < count; i++)
        {
            var bodySize = checked((int)ReadUleb(bytes, ref pos));
            var bodyOffset = pos;
            var bodyEnd = checked(pos + bodySize);
            if (bodyEnd > end)
                throw new InvalidDataException("A WebAssembly function body extends past the code section.");

            var localCount = checked((int)ReadUleb(bytes, ref pos));
            var locals = new List<WasmLocalInfo>(localCount);
            for (var l = 0; l < localCount; l++)
            {
                var localRunCount = ReadUleb(bytes, ref pos);
                var valueType = ReadByte(bytes, ref pos);
                locals.Add(new WasmLocalInfo((uint)localRunCount, valueType, ValueTypeName(valueType)));
            }

            var codeOffset = pos;
            result.Add(new WasmFunctionBody(bodyOffset, bodySize, codeOffset, bodyEnd - codeOffset, locals));
            pos = bodyEnd;
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static int ReadDataCountSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        RequireSectionConsumed(pos, end);
        return count;
    }

    private static List<WasmTagInfo> ReadTagSection(
        ReadOnlySpan<byte> bytes, ref int pos, int end, int importCount)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmTagInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var attribute = checked((uint)ReadUleb(bytes, ref pos));
            var typeIndex = checked((int)ReadUleb(bytes, ref pos));
            result.Add(new WasmTagInfo(importCount + i, attribute, typeIndex));
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static List<WasmDataSegmentInfo> ReadDataSection(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        var result = new List<WasmDataSegmentInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var modeValue = checked((int)ReadUleb(bytes, ref pos));
            var mode = modeValue switch
            {
                0 => "active",
                1 => "passive",
                2 => "active-explicit-memory",
                _ => $"mode-{modeValue}",
            };

            if (modeValue == 0)
                SkipConstExpr(bytes, ref pos, end);
            else if (modeValue == 2)
            {
                _ = ReadUleb(bytes, ref pos);
                SkipConstExpr(bytes, ref pos, end);
            }
            else if (modeValue != 1)
            {
                throw new InvalidDataException($"Unsupported WebAssembly data segment mode {modeValue}.");
            }

            var size = checked((int)ReadUleb(bytes, ref pos));
            if (pos + size > end)
                throw new InvalidDataException("A WebAssembly data segment extends past the data section.");
            result.Add(new WasmDataSegmentInfo(i, mode, pos, size));
            pos += size;
        }

        RequireSectionConsumed(pos, end);
        return result;
    }

    private static void ParseCustomSection(
        string name,
        ReadOnlySpan<byte> bytes,
        int pos,
        int end,
        Dictionary<int, string> functionNames,
        List<string> targetFeatures,
        List<string> producers)
    {
        try
        {
            if (name == "name")
                ParseNameSection(bytes, pos, end, functionNames);
            else if (name == "target_features")
                ParseTargetFeatures(bytes, pos, end, targetFeatures);
            else if (name == "producers")
                ParseProducers(bytes, pos, end, producers);
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
        {
            // Custom sections are descriptive. A corrupt custom payload should not hide code bodies.
        }
    }

    private static void ParseNameSection(ReadOnlySpan<byte> bytes, int pos, int end, Dictionary<int, string> functionNames)
    {
        while (pos < end)
        {
            var subSectionId = ReadByte(bytes, ref pos);
            var size = checked((int)ReadUleb(bytes, ref pos));
            var subEnd = checked(pos + size);
            if (subEnd > end)
                throw new InvalidDataException("A WebAssembly name subsection extends past the custom section.");

            if (subSectionId == 1)
            {
                var count = checked((int)ReadUleb(bytes, ref pos));
                for (var i = 0; i < count; i++)
                {
                    var index = checked((int)ReadUleb(bytes, ref pos));
                    var name = ReadName(bytes, ref pos, subEnd);
                    functionNames[index] = name;
                }
            }

            pos = subEnd;
        }
    }

    private static void ParseTargetFeatures(ReadOnlySpan<byte> bytes, int pos, int end, List<string> targetFeatures)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        for (var i = 0; i < count && pos < end; i++)
        {
            var prefix = ReadByte(bytes, ref pos);
            var feature = ReadName(bytes, ref pos, end);
            targetFeatures.Add($"{(char)prefix}{feature}");
        }
    }

    private static void ParseProducers(ReadOnlySpan<byte> bytes, int pos, int end, List<string> producers)
    {
        var fieldCount = checked((int)ReadUleb(bytes, ref pos));
        for (var f = 0; f < fieldCount && pos < end; f++)
        {
            var fieldName = ReadName(bytes, ref pos, end);
            var valueCount = checked((int)ReadUleb(bytes, ref pos));
            for (var i = 0; i < valueCount && pos < end; i++)
            {
                var name = ReadName(bytes, ref pos, end);
                var version = ReadName(bytes, ref pos, end);
                producers.Add($"{fieldName}: {name} {version}".TrimEnd());
            }
        }
    }

    private static List<WasmFunctionInfo> BuildFunctions(
        IReadOnlyList<WasmTypeInfo> types,
        List<WasmImportInfo> functionImports,
        List<int> functionTypeIndices,
        IReadOnlyList<WasmFunctionBody> bodies,
        IReadOnlyList<WasmExportInfo> exports,
        IReadOnlyDictionary<int, string> nameSection,
        IReadOnlyDictionary<int, string> symbolMap)
    {
        var exportNames = exports
            .Where(e => e.Kind == WasmExternalKind.Function)
            .GroupBy(e => e.Index)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(e => e.Name).Order(StringComparer.Ordinal)]);

        var functions = new List<WasmFunctionInfo>(functionImports.Count + bodies.Count);
        for (var i = 0; i < functionImports.Count; i++)
        {
            var import = functionImports[i];
            var (name, source) = BestName(i, symbolMap, nameSection, exportNames, $"{import.ModuleName}!{import.Name}", "import");
            var type = TypeFor(types, import.TypeIndex);
            functions.Add(new WasmFunctionInfo(
                Index: i,
                TypeIndex: import.TypeIndex,
                Name: name,
                NameSource: source,
                IsImported: true,
                ImportModule: import.ModuleName,
                ImportName: import.Name,
                IsExported: exportNames.ContainsKey(i),
                ExportNames: exportNames.GetValueOrDefault(i, []),
                BodyOffset: null,
                BodySize: 0,
                CodeOffset: null,
                CodeSize: 0,
                Locals: [],
                ParamTypes: type.ParamTypes,
                ResultTypes: type.ResultTypes));
        }

        for (var i = 0; i < bodies.Count; i++)
        {
            var index = functionImports.Count + i;
            var typeIndex = i < functionTypeIndices.Count ? functionTypeIndices[i] : (int?)null;
            var type = TypeFor(types, typeIndex);
            var body = bodies[i];
            var (name, source) = BestName(index, symbolMap, nameSection, exportNames, $"func_{index}", "synthetic");
            functions.Add(new WasmFunctionInfo(
                Index: index,
                TypeIndex: typeIndex,
                Name: name,
                NameSource: source,
                IsImported: false,
                ImportModule: null,
                ImportName: null,
                IsExported: exportNames.ContainsKey(index),
                ExportNames: exportNames.GetValueOrDefault(index, []),
                BodyOffset: body.BodyOffset,
                BodySize: body.BodySize,
                CodeOffset: body.CodeOffset,
                CodeSize: body.CodeSize,
                Locals: body.Locals,
                ParamTypes: type.ParamTypes,
                ResultTypes: type.ResultTypes));
        }

        return functions;
    }

    private static (string Name, string Source) BestName(
        int index,
        IReadOnlyDictionary<int, string> symbolMap,
        IReadOnlyDictionary<int, string> nameSection,
        Dictionary<int, IReadOnlyList<string>> exportNames,
        string fallback,
        string fallbackSource)
    {
        if (symbolMap.TryGetValue(index, out var symbol) && !string.IsNullOrWhiteSpace(symbol))
            return (symbol, "symbol-map");
        if (nameSection.TryGetValue(index, out var name) && !string.IsNullOrWhiteSpace(name))
            return (name, "name-section");
        if (exportNames.TryGetValue(index, out var exports) && exports.Count > 0)
            return (exports[0], "export");
        return (fallback, fallbackSource);
    }

    private static WasmTypeInfo TypeFor(IReadOnlyList<WasmTypeInfo> types, int? typeIndex) =>
        typeIndex is { } index && index >= 0 && index < types.Count
            ? types[index]
            : new WasmTypeInfo(-1, [], []);

    private static (string? Path, WasmSymbolMapStatus Status, Dictionary<int, string> Entries) ReadSymbolMap(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return (null, WasmSymbolMapStatus.Missing, []);

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir))
            return (null, WasmSymbolMapStatus.Missing, []);

        var stem = Path.GetFileNameWithoutExtension(filePath);
        var candidates = new[]
        {
            Path.Combine(dir, stem + ".js.symbols"),
            filePath + ".symbols",
            Path.Combine(dir, stem + ".symbols"),
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            return (null, WasmSymbolMapStatus.Missing, []);

        var entries = new Dictionary<int, string>();
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                if (!int.TryParse(line[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                    continue;
                var name = line[(colon + 1)..].Trim();
                if (name.Length > 0)
                    entries[index] = name;
            }
        }
        catch (IOException)
        {
            return (path, WasmSymbolMapStatus.Corrupt, []);
        }

        return entries.Count > 0
            ? (path, WasmSymbolMapStatus.Loaded, entries)
            : (path, WasmSymbolMapStatus.Corrupt, entries);
    }

    private static bool IsWebcilPayload(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 28)
            return false;

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var major = BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]);
        var minor = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]);
        return magic == WebcilMagic && major is 0 or 1 && minor == 0;
    }

    private static void SkipTableType(ReadOnlySpan<byte> bytes, ref int pos)
    {
        _ = ReadRefTypeName(bytes, ref pos);
        SkipLimits(bytes, ref pos);
    }

    private static void SkipLimits(ReadOnlySpan<byte> bytes, ref int pos)
    {
        _ = ReadLimits(bytes, ref pos);
    }

    private static (ulong Minimum, ulong? Maximum, bool IsShared, bool IsMemory64) ReadLimits(
        ReadOnlySpan<byte> bytes, ref int pos)
    {
        var flags = ReadUleb(bytes, ref pos);
        var minimum = ReadUleb(bytes, ref pos);
        ulong? maximum = null;
        if ((flags & 0x01) != 0)
            maximum = ReadUleb(bytes, ref pos);

        return (minimum, maximum, (flags & 0x02) != 0, (flags & 0x04) != 0);
    }

    private static string ReadRefTypeName(ReadOnlySpan<byte> bytes, ref int pos)
    {
        var first = ReadByte(bytes, ref pos);
        if (first is 0x63 or 0x64)
        {
            var heapType = ReadSleb(bytes, ref pos);
            return first == 0x63 ? $"ref null {HeapTypeName(heapType)}" : $"ref {HeapTypeName(heapType)}";
        }

        return RefTypeName(first);
    }

    private static int SkipFunctionIndexVector(ReadOnlySpan<byte> bytes, ref int pos)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        for (var i = 0; i < count; i++)
            _ = ReadUleb(bytes, ref pos);
        return count;
    }

    private static int SkipExpressionVector(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var count = checked((int)ReadUleb(bytes, ref pos));
        for (var i = 0; i < count; i++)
            SkipConstExpr(bytes, ref pos, end);
        return count;
    }

    private static string ElementKindName(byte elementKind) => elementKind switch
    {
        0x00 => "funcref",
        _ => $"0x{elementKind:X2}",
    };

    private static string RefTypeName(byte refType) => refType switch
    {
        0x70 => "funcref",
        0x6F => "externref",
        0x6E => "anyref",
        0x6D => "eqref",
        0x6C => "i31ref",
        0x6B => "structref",
        0x6A => "arrayref",
        0x69 => "exnref",
        0x68 => "stringref",
        0x67 => "stringview_wtf8",
        0x66 => "stringview_wtf16",
        0x65 => "stringview_iter",
        0x64 => "ref",
        0x63 => "ref null",
        _ => $"0x{refType:X2}",
    };

    private static string HeapTypeName(long heapType) => heapType switch
    {
        -0x10 => "func",
        -0x11 => "extern",
        -0x12 => "any",
        -0x13 => "eq",
        -0x14 => "i31",
        -0x15 => "struct",
        -0x16 => "array",
        -0x17 => "none",
        -0x18 => "nofunc",
        -0x19 => "noextern",
        -0x1A => "exn",
        -0x1B => "noexn",
        _ => heapType.ToString(CultureInfo.InvariantCulture),
    };

    private static void SkipRefType(ReadOnlySpan<byte> bytes, ref int pos)
    {
        var first = ReadByte(bytes, ref pos);
        if (first is 0x63 or 0x64)
            _ = ReadSleb(bytes, ref pos);
    }

    private static void SkipConstExpr(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        while (pos < end)
        {
            var op = ReadByte(bytes, ref pos);
            if (op == 0x0B)
                return;

            switch (op)
            {
                case 0x41:
                case 0x42:
                    _ = ReadSleb(bytes, ref pos);
                    break;
                case 0x43:
                    pos = checked(pos + 4);
                    break;
                case 0x44:
                    pos = checked(pos + 8);
                    break;
                case 0x23:
                case 0xD0:
                case 0xD2:
                    _ = ReadUleb(bytes, ref pos);
                    break;
            }
        }

        throw new InvalidDataException("A WebAssembly constant expression is unterminated.");
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        var length = checked((int)ReadUleb(bytes, ref pos));
        if (pos + length > end)
            throw new InvalidDataException("A WebAssembly name extends past its containing section.");

        var name = Encoding.UTF8.GetString(bytes.Slice(pos, length));
        pos += length;
        return name;
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int pos)
    {
        if ((uint)pos >= (uint)bytes.Length)
            throw new InvalidDataException("Unexpected end of WebAssembly data.");

        return bytes[pos++];
    }

    private static ulong ReadUleb(ReadOnlySpan<byte> bytes, ref int pos)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var b = ReadByte(bytes, ref pos);
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return value;

            shift += 7;
            if (shift >= 64)
                throw new InvalidDataException("A WebAssembly LEB128 value is too large.");
        }
    }

    private static long ReadSleb(ReadOnlySpan<byte> bytes, ref int pos)
    {
        long value = 0;
        var shift = 0;
        byte b;
        do
        {
            b = ReadByte(bytes, ref pos);
            value |= (long)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0 && shift < 64);

        if (shift < 64 && (b & 0x40) != 0)
            value |= -1L << shift;

        return value;
    }

    private static void RequireSectionConsumed(int pos, int end)
    {
        if (pos != end)
            throw new InvalidDataException("A WebAssembly section was not consumed exactly.");
    }

    private static WasmExternalKind ExternalKind(byte kind) => kind switch
    {
        0 => WasmExternalKind.Function,
        1 => WasmExternalKind.Table,
        2 => WasmExternalKind.Memory,
        3 => WasmExternalKind.Global,
        4 => WasmExternalKind.Tag,
        _ => WasmExternalKind.Unknown,
    };

    private static string StandardSectionName(byte id) => id switch
    {
        0 => "custom",
        1 => "type",
        2 => "import",
        3 => "function",
        4 => "table",
        5 => "memory",
        6 => "global",
        7 => "export",
        8 => "start",
        9 => "element",
        10 => "code",
        11 => "data",
        12 => "data-count",
        13 => "tag",
        _ => $"section-{id}",
    };

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

    private readonly record struct WasmFunctionBody(
        int BodyOffset,
        int BodySize,
        int CodeOffset,
        int CodeSize,
        IReadOnlyList<WasmLocalInfo> Locals);
}
