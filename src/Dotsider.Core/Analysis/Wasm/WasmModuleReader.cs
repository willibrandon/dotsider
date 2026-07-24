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
    internal const int MaxDecodedItems = 1 << 20;

    private const uint WasmMagic = 0x6D736100;
    private const uint WasmVersion = 1;

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
    /// Reads a WebAssembly module, preserving the valid standard-section prefix and ignoring
    /// malformed descriptive custom sections.
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

        var reader = new WasmDataReader(bytes, 8);
        var remainingItems = MaxDecodedItems;
        while (!reader.AtEnd)
        {
            try
            {
                ChargeItems(ref remainingItems, 1, "section");
                var sectionId = reader.ReadByte();
                var sectionSize = ReadLength(ref reader, "section");
                var sectionPayloadOffset = reader.Position;
                var sectionReader = reader.ReadSubReader(sectionSize);

                var sectionName = StandardSectionName(sectionId);
                if (sectionId == 0)
                {
                    sectionName = ReadName(ref sectionReader, "custom-section name");
                    ParseCustomSection(
                        sectionName, ref sectionReader, functionNames, targetFeatures, producers,
                        ref remainingItems);
                }
                else
                {
                    switch (sectionId)
                    {
                        case 1:
                            types = ReadTypeSection(ref sectionReader, ref remainingItems);
                            break;
                        case 2:
                            (imports, functionImports) = ReadImportSection(
                                ref sectionReader, ref remainingItems);
                            break;
                        case 3:
                            functionTypeIndices = ReadFunctionSection(
                                ref sectionReader, ref remainingItems);
                            break;
                        case 4:
                            tables = ReadTableSection(
                                ref sectionReader,
                                imports.Count(static i => i.Kind == WasmExternalKind.Table),
                                ref remainingItems);
                            break;
                        case 5:
                            memories = ReadMemorySection(
                                ref sectionReader,
                                imports.Count(static i => i.Kind == WasmExternalKind.Memory),
                                ref remainingItems);
                            break;
                        case 6:
                            globals = ReadGlobalSection(
                                ref sectionReader,
                                imports.Count(static i => i.Kind == WasmExternalKind.Global),
                                ref remainingItems);
                            break;
                        case 7:
                            exports = ReadExportSection(ref sectionReader, ref remainingItems);
                            break;
                        case 8:
                            startFunctionIndex = ReadStartSection(ref sectionReader);
                            break;
                        case 9:
                            elements = ReadElementSection(ref sectionReader, ref remainingItems);
                            break;
                        case 10:
                            bodies = ReadCodeSection(ref sectionReader, ref remainingItems);
                            break;
                        case 11:
                            dataSegments = ReadDataSection(ref sectionReader, ref remainingItems);
                            break;
                        case 12:
                            dataCount = ReadDataCountSection(ref sectionReader);
                            break;
                        case 13:
                            tags = ReadTagSection(
                                ref sectionReader,
                                imports.Count(static i => i.Kind == WasmExternalKind.Tag),
                                ref remainingItems);
                            break;
                    }
                }

                sections.Add(new WasmSectionInfo(sectionId, sectionName, sectionPayloadOffset, sectionSize));
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

    private static List<WasmTypeInfo> ReadTypeSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "type-section");
        var types = new List<WasmTypeInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var form = reader.ReadByte();
            if (form != 0x60)
                throw new InvalidDataException("Only WebAssembly function types are supported.");

            var paramCount = ReadVectorCount(
                ref reader, minimumElementSize: 1, ref remainingItems, "parameter");
            var parameters = new byte[paramCount];
            for (var p = 0; p < paramCount; p++)
                parameters[p] = reader.ReadByte();

            var resultCount = ReadVectorCount(
                ref reader, minimumElementSize: 1, ref remainingItems, "result");
            var results = new byte[resultCount];
            for (var r = 0; r < resultCount; r++)
                results[r] = reader.ReadByte();

            types.Add(new WasmTypeInfo(i, parameters, results));
        }

        RequireSectionConsumed(ref reader);
        return types;
    }

    private static (List<WasmImportInfo> Imports, List<WasmImportInfo> FunctionImports)
        ReadImportSection(ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 4, ref remainingItems, "import-section");
        var imports = new List<WasmImportInfo>(count);
        var functionImports = new List<WasmImportInfo>();
        var tableIndex = 0;
        var memoryIndex = 0;
        var globalIndex = 0;
        var tagIndex = 0;
        for (var i = 0; i < count; i++)
        {
            var module = ReadName(ref reader, "import module name");
            var name = ReadName(ref reader, "import name");
            var kindByte = reader.ReadByte();
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
                    typeIndex = ReadIndex(ref reader, "function type index");
                    break;
                case WasmExternalKind.Table:
                    SkipTableType(ref reader);
                    break;
                case WasmExternalKind.Memory:
                    SkipLimits(ref reader);
                    break;
                case WasmExternalKind.Global:
                    _ = reader.ReadByte();
                    _ = reader.ReadByte();
                    break;
                case WasmExternalKind.Tag:
                    _ = reader.ReadUnsignedLeb12832();
                    typeIndex = ReadIndex(ref reader, "tag type index");
                    break;
                default:
                    throw new InvalidDataException($"Unsupported WebAssembly import kind 0x{kindByte:X2}.");
            }

            var import = new WasmImportInfo(module, name, kind, index, typeIndex);
            imports.Add(import);
            if (kind == WasmExternalKind.Function)
                functionImports.Add(import);
        }

        RequireSectionConsumed(ref reader);
        return (imports, functionImports);
    }

    private static List<int> ReadFunctionSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 1, ref remainingItems, "function-section");
        var result = new List<int>(count);
        for (var i = 0; i < count; i++)
            result.Add(ReadIndex(ref reader, "function type index"));

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmTableInfo> ReadTableSection(
        ref WasmDataReader reader, int importCount, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "table-section");
        var result = new List<WasmTableInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var refType = ReadRefTypeName(ref reader);
            var (minimum, maximum, _, _) = ReadLimits(ref reader);
            result.Add(new WasmTableInfo(importCount + i, refType, minimum, maximum));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmMemoryInfo> ReadMemorySection(
        ref WasmDataReader reader, int importCount, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 2, ref remainingItems, "memory-section");
        var result = new List<WasmMemoryInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var (minimum, maximum, isShared, isMemory64) = ReadLimits(ref reader);
            result.Add(new WasmMemoryInfo(importCount + i, minimum, maximum, isShared, isMemory64));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmGlobalInfo> ReadGlobalSection(
        ref WasmDataReader reader, int importCount, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "global-section");
        var result = new List<WasmGlobalInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var valueType = reader.ReadByte();
            var mutable = reader.ReadByte();
            SkipConstExpr(ref reader);
            result.Add(new WasmGlobalInfo(importCount + i, valueType, ValueTypeName(valueType), mutable != 0));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmExportInfo> ReadExportSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "export-section");
        var result = new List<WasmExportInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var name = ReadName(ref reader, "export name");
            var kind = ExternalKind(reader.ReadByte());
            var index = ReadIndex(ref reader, "export index");
            result.Add(new WasmExportInfo(name, kind, index));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static int ReadStartSection(ref WasmDataReader reader)
    {
        var index = ReadIndex(ref reader, "start function index");
        RequireSectionConsumed(ref reader);
        return index;
    }

    private static List<WasmElementSegmentInfo> ReadElementSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "element-section");
        var result = new List<WasmElementSegmentInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var flags = ReadIndex(ref reader, "element segment flags");
            string mode;
            int? tableIndex = null;
            string elementType;
            int elementCount;

            switch (flags)
            {
                case 0:
                    mode = "active";
                    tableIndex = 0;
                    SkipConstExpr(ref reader);
                    elementType = "funcref";
                    elementCount = SkipFunctionIndexVector(
                        ref reader, ref remainingItems);
                    break;
                case 1:
                    mode = "passive";
                    elementType = ElementKindName(reader.ReadByte());
                    elementCount = SkipFunctionIndexVector(
                        ref reader, ref remainingItems);
                    break;
                case 2:
                    mode = "active-explicit-table";
                    tableIndex = ReadIndex(ref reader, "element table index");
                    SkipConstExpr(ref reader);
                    elementType = ElementKindName(reader.ReadByte());
                    elementCount = SkipFunctionIndexVector(
                        ref reader, ref remainingItems);
                    break;
                case 3:
                    mode = "declarative";
                    elementType = ElementKindName(reader.ReadByte());
                    elementCount = SkipFunctionIndexVector(
                        ref reader, ref remainingItems);
                    break;
                case 4:
                    mode = "active";
                    tableIndex = 0;
                    SkipConstExpr(ref reader);
                    elementType = "funcref";
                    elementCount = SkipExpressionVector(
                        ref reader, ref remainingItems);
                    break;
                case 5:
                    mode = "passive";
                    elementType = ReadRefTypeName(ref reader);
                    elementCount = SkipExpressionVector(
                        ref reader, ref remainingItems);
                    break;
                case 6:
                    mode = "active-explicit-table";
                    tableIndex = ReadIndex(ref reader, "element table index");
                    SkipConstExpr(ref reader);
                    elementType = ReadRefTypeName(ref reader);
                    elementCount = SkipExpressionVector(
                        ref reader, ref remainingItems);
                    break;
                case 7:
                    mode = "declarative";
                    elementType = ReadRefTypeName(ref reader);
                    elementCount = SkipExpressionVector(
                        ref reader, ref remainingItems);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported WebAssembly element segment flags {flags}.");
            }

            result.Add(new WasmElementSegmentInfo(i, mode, tableIndex, elementType, elementCount));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmFunctionBody> ReadCodeSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 3, ref remainingItems, "code-section");
        var result = new List<WasmFunctionBody>(count);
        for (var i = 0; i < count; i++)
        {
            var bodySize = ReadLength(ref reader, "function body");
            var bodyOffset = reader.Position;
            var bodyReader = reader.ReadSubReader(bodySize);

            var localCount = ReadVectorCount(
                ref bodyReader, minimumElementSize: 2, ref remainingItems,
                "function-local declaration");
            var locals = new List<WasmLocalInfo>(localCount);
            for (var l = 0; l < localCount; l++)
            {
                var localRunCount = bodyReader.ReadUnsignedLeb12832();
                ChargeItems(ref remainingItems, localRunCount, "function local");
                var valueType = bodyReader.ReadByte();
                locals.Add(new WasmLocalInfo(
                    localRunCount, valueType, ValueTypeName(valueType)));
            }

            var codeOffset = bodyReader.Position;
            result.Add(new WasmFunctionBody(
                bodyOffset, bodySize, codeOffset, bodyReader.Remaining, locals));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static int ReadDataCountSection(ref WasmDataReader reader)
    {
        var count = ReadIndex(ref reader, "data count");
        RequireSectionConsumed(ref reader);
        return count;
    }

    private static List<WasmTagInfo> ReadTagSection(
        ref WasmDataReader reader, int importCount, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 2, ref remainingItems, "tag-section");
        var result = new List<WasmTagInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var attribute = reader.ReadUnsignedLeb12832();
            var typeIndex = ReadIndex(ref reader, "tag type index");
            result.Add(new WasmTagInfo(importCount + i, attribute, typeIndex));
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static List<WasmDataSegmentInfo> ReadDataSection(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 2, ref remainingItems, "data-section");
        var result = new List<WasmDataSegmentInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var modeValue = ReadIndex(ref reader, "data segment mode");
            var mode = modeValue switch
            {
                0 => "active",
                1 => "passive",
                2 => "active-explicit-memory",
                _ => $"mode-{modeValue}",
            };

            if (modeValue == 0)
                SkipConstExpr(ref reader);
            else if (modeValue == 2)
            {
                _ = reader.ReadUnsignedLeb12832();
                SkipConstExpr(ref reader);
            }
            else if (modeValue != 1)
            {
                throw new InvalidDataException($"Unsupported WebAssembly data segment mode {modeValue}.");
            }

            var size = ReadLength(ref reader, "data segment");
            result.Add(new WasmDataSegmentInfo(i, mode, reader.Position, size));
            _ = reader.ReadBytes(size);
        }

        RequireSectionConsumed(ref reader);
        return result;
    }

    private static void ParseCustomSection(
        string name,
        ref WasmDataReader reader,
        Dictionary<int, string> functionNames,
        List<string> targetFeatures,
        List<string> producers,
        ref int remainingItems)
    {
        try
        {
            if (name == "name")
            {
                var parsedNames = new Dictionary<int, string>();
                ParseNameSection(ref reader, parsedNames, ref remainingItems);
                foreach (var (index, functionName) in parsedNames)
                    functionNames[index] = functionName;
            }
            else if (name == "target_features")
            {
                var parsedFeatures = new List<string>();
                ParseTargetFeatures(ref reader, parsedFeatures, ref remainingItems);
                targetFeatures.AddRange(parsedFeatures);
            }
            else if (name == "producers")
            {
                var parsedProducers = new List<string>();
                ParseProducers(ref reader, parsedProducers, ref remainingItems);
                producers.AddRange(parsedProducers);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
        {
            // Custom sections are descriptive. A corrupt custom payload should not hide code bodies.
        }
    }

    private static void ParseNameSection(
        ref WasmDataReader reader,
        Dictionary<int, string> functionNames,
        ref int remainingItems)
    {
        while (!reader.AtEnd)
        {
            var subSectionId = reader.ReadByte();
            var size = ReadLength(ref reader, "name subsection");
            var subReader = reader.ReadSubReader(size);

            if (subSectionId == 1)
            {
                var count = ReadVectorCount(
                    ref subReader, minimumElementSize: 2, ref remainingItems,
                    "function-name");
                for (var i = 0; i < count; i++)
                {
                    var index = ReadIndex(ref subReader, "function-name index");
                    var name = ReadName(ref subReader, "function name");
                    functionNames[index] = name;
                }

                RequireSectionConsumed(ref subReader);
            }
        }
    }

    private static void ParseTargetFeatures(
        ref WasmDataReader reader,
        List<string> targetFeatures,
        ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 2, ref remainingItems, "target-feature");
        for (var i = 0; i < count; i++)
        {
            var prefix = reader.ReadByte();
            var feature = ReadName(ref reader, "target feature name");
            targetFeatures.Add($"{(char)prefix}{feature}");
        }

        RequireSectionConsumed(ref reader);
    }

    private static void ParseProducers(
        ref WasmDataReader reader,
        List<string> producers,
        ref int remainingItems)
    {
        var fieldCount = ReadVectorCount(
            ref reader, minimumElementSize: 2, ref remainingItems, "producer field");
        for (var f = 0; f < fieldCount; f++)
        {
            var fieldName = ReadName(ref reader, "producer field name");
            var valueCount = ReadVectorCount(
                ref reader, minimumElementSize: 2, ref remainingItems, "producer value");
            for (var i = 0; i < valueCount; i++)
            {
                var name = ReadName(ref reader, "producer name");
                var version = ReadName(ref reader, "producer version");
                producers.Add($"{fieldName}: {name} {version}".TrimEnd());
            }
        }

        RequireSectionConsumed(ref reader);
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

    private static void ChargeItems(
        ref int remainingItems, uint count, string description)
    {
        if (count > (uint)remainingItems)
        {
            throw new InvalidDataException(
                $"The WebAssembly {description} count exceeds the "
                + "1,048,576-item decoding budget.");
        }

        remainingItems -= (int)count;
    }

    private static int ReadIndex(ref WasmDataReader reader, string description)
    {
        var value = reader.ReadUnsignedLeb12832();
        if (value > int.MaxValue)
        {
            throw new InvalidDataException(
                $"The WebAssembly {description} exceeds the supported range.");
        }

        return (int)value;
    }

    private static int ReadLength(ref WasmDataReader reader, string description)
    {
        var length = reader.ReadUnsignedLeb12832();
        if (length > (uint)reader.Remaining)
        {
            throw new InvalidDataException(
                $"The WebAssembly {description} extends past its containing data.");
        }

        return (int)length;
    }

    private static (ulong Minimum, ulong? Maximum, bool IsShared, bool IsMemory64) ReadLimits(
        ref WasmDataReader reader)
    {
        var flags = reader.ReadUnsignedLeb12832();
        var isMemory64 = (flags & 0x04) != 0;
        var minimum = isMemory64
            ? reader.ReadUnsignedLeb12864()
            : reader.ReadUnsignedLeb12832();
        ulong? maximum = null;
        if ((flags & 0x01) != 0)
        {
            maximum = isMemory64
                ? reader.ReadUnsignedLeb12864()
                : reader.ReadUnsignedLeb12832();
        }

        return (minimum, maximum, (flags & 0x02) != 0, isMemory64);
    }

    private static string ReadName(ref WasmDataReader reader, string description)
    {
        var length = ReadLength(ref reader, description);
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static string ReadRefTypeName(ref WasmDataReader reader)
    {
        var first = reader.ReadByte();
        if (first is 0x63 or 0x64)
        {
            var heapType = reader.ReadSignedLeb128();
            return first == 0x63
                ? $"ref null {HeapTypeName(heapType)}"
                : $"ref {HeapTypeName(heapType)}";
        }

        return RefTypeName(first);
    }

    private static int ReadVectorCount(
        ref WasmDataReader reader,
        int minimumElementSize,
        ref int remainingItems,
        string description)
    {
        var count = reader.ReadUnsignedLeb12832();
        if (count > (uint)(reader.Remaining / minimumElementSize))
        {
            throw new InvalidDataException(
                $"The WebAssembly {description} count exceeds its containing data.");
        }

        if (count > (uint)remainingItems)
        {
            throw new InvalidDataException(
                $"The WebAssembly {description} count exceeds the "
                + "1,048,576-item decoding budget.");
        }

        remainingItems -= (int)count;
        return (int)count;
    }

    private static void RequireSectionConsumed(ref WasmDataReader reader)
    {
        if (!reader.AtEnd)
            throw new InvalidDataException("A WebAssembly section was not consumed exactly.");
    }

    private static void SkipConstExpr(ref WasmDataReader reader)
    {
        while (!reader.AtEnd)
        {
            var op = reader.ReadByte();
            if (op == 0x0B)
                return;

            switch (op)
            {
                case 0x41:
                case 0x42:
                    _ = reader.ReadSignedLeb128();
                    break;
                case 0x43:
                    _ = reader.ReadBytes(4);
                    break;
                case 0x44:
                    _ = reader.ReadBytes(8);
                    break;
                case 0x23:
                case 0xD0:
                case 0xD2:
                    _ = reader.ReadUnsignedLeb12832();
                    break;
            }
        }

        throw new InvalidDataException("A WebAssembly constant expression is unterminated.");
    }

    private static int SkipExpressionVector(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 1, ref remainingItems, "element expression");
        for (var i = 0; i < count; i++)
            SkipConstExpr(ref reader);

        return count;
    }

    private static int SkipFunctionIndexVector(
        ref WasmDataReader reader, ref int remainingItems)
    {
        var count = ReadVectorCount(
            ref reader, minimumElementSize: 1, ref remainingItems, "element function-index");
        for (var i = 0; i < count; i++)
            _ = reader.ReadUnsignedLeb12832();

        return count;
    }

    private static void SkipLimits(ref WasmDataReader reader)
    {
        _ = ReadLimits(ref reader);
    }

    private static void SkipTableType(ref WasmDataReader reader)
    {
        _ = ReadRefTypeName(ref reader);
        SkipLimits(ref reader);
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
