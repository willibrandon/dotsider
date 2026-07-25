using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Orchestrates the per-image ReadyToRun view: locates the shared runtime-function and hot/cold
/// tables, then joins each metadata source's <c>MethodDefEntryPoints</c> to them. A non-composite
/// image contributes a single source (itself); a composite resolves one source per component (by
/// name + MVID) with a shared code image; a component DLL delegates to its owner composite. Never
/// throws — a malformed image-wide table produces an explicitly unavailable method map.
/// </summary>
internal static class ReadyToRunImageReader
{
    /// <summary>
    /// Builds the resolved ReadyToRun view for <paramref name="analyzer"/>: its precompiled methods,
    /// the code image, the metadata providers, and any owned sibling analyzers. Returns a model with
    /// empty methods (and a diagnostic) when the image is composite but unresolved, or when the
    /// required sections are absent.
    /// </summary>
    public static ReadyToRunModel Build(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        if (info.IsComponent)
            return BuildComponent(analyzer, info);
        if (info.IsComposite)
            return BuildComposite(analyzer, info);
        return BuildNonComposite(analyzer, info);
    }

    // Metadata and native code in the same file: one source (self), the image's own instance table.
    private static ReadyToRunModel BuildNonComposite(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        if (!TryOpenTables(analyzer, info, out var tables, out var tableDiagnostic))
        {
            return Unavailable(analyzer, tableDiagnostic);
        }

        var mvid = ReadMvid(analyzer);
        var providers = new Dictionary<Guid, AssemblyAnalyzer> { [mvid] = analyzer };
        var sources = new List<ReadyToRunMethodMapReader.MethodMapSource>();
        if (Section(info, ReadyToRunSectionType.MethodDefEntryPoints)
            is { FileOffset: { } entryOffset, Size: > 0 } entryPoints)
        {
            sources.Add(new ReadyToRunMethodMapReader.MethodMapSource(
                analyzer.AssemblyName ?? "", mvid, entryOffset, entryPoints.Size,
                analyzer.MethodDefs, analyzer.GetMetadataReader()));
        }

        var instance = Section(info, ReadyToRunSectionType.InstanceMethodEntryPoints);
        var global = instance is { FileOffset: { } io }
            ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                io, instance.Size, analyzer.GetMetadataReader(), analyzer.AssemblyName ?? "", mvid, analyzer.MethodDefs)
            : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;

        var mapUsable = TryBuild(
            tables,
            sources,
            global,
            out var methods,
            out var methodMapDiagnostic);
        return new ReadyToRunModel(
            methods,
            analyzer,
            providers,
            [],
            [],
            OwnerCompositeMissing: false,
            MapUsable: mapUsable,
            methodMapDiagnostic);
    }

    // A composite opened directly: one source per resolved component; native code is this file.
    private static ReadyToRunModel BuildComposite(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        if (!TryOpenTables(analyzer, info, out var tables, out var tableDiagnostic))
            return Unavailable(analyzer, tableDiagnostic);

        var raw = analyzer.RawBytes;
        var imageBase = analyzer.PeHeaders?.ImageBase ?? 0;
        var addressSpace = tables.AddressSpace;
        var directory = Path.GetDirectoryName(analyzer.FilePath) ?? ".";

        var components = ReadyToRunCompositeReader.ReadComponents(raw, info, imageBase, addressSpace);
        var sources = new List<ReadyToRunMethodMapReader.MethodMapSource>(components.Count);
        var providers = new Dictionary<Guid, AssemblyAnalyzer>();
        var owned = new List<AssemblyAnalyzer>();
        var listing = new List<ReadyToRunComponent>(components.Count);
        var unresolved = 0;

        foreach (var component in components)
        {
            var resolved = ReadyToRunComponentResolver.Resolve(directory, component.Name, component.Mvid);
            if (resolved is not null)
            {
                owned.Add(resolved);
                if (!providers.ContainsKey(component.Mvid))
                    providers[component.Mvid] = resolved;
            }
            else
            {
                unresolved++;
            }

            var name = component.Name ?? resolved?.AssemblyName ?? component.Mvid.ToString();
            sources.Add(new ReadyToRunMethodMapReader.MethodMapSource(
                name, component.Mvid, component.MethodDefEntryPointsFileOffset,
                component.MethodDefEntryPointsSize,
                resolved?.MethodDefs ?? [], resolved?.GetMetadataReader()));
            listing.Add(new ReadyToRunComponent(
                name, component.Mvid, 0, component.CoreHeaderRva,
                resolved?.FilePath, resolved is not null));
        }

        // The image's instantiated generics live in one global table; a module override attributes
        // each to its component, resolved through the module context (manifest AssemblyRef order).
        var moduleContext = ReadyToRunModuleContext.Create(
            info, listing, mvid => providers.GetValueOrDefault(mvid));
        var instance = Section(info, ReadyToRunSectionType.InstanceMethodEntryPoints);
        // The composite manifest contains assembly-reference routing metadata, not a MethodDef or
        // MemberRef token scope. Runtime starts global signatures without a current metadata reader;
        // MODULE_ZAPSIG and the primitive-owner system-module fallback select the real scope.
        var global = instance is { FileOffset: { } io }
            ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                io, instance.Size, null, analyzer.AssemblyName ?? "", Guid.Empty, [])
            : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;
        var mapUsable = TryBuild(
            tables,
            sources,
            global,
            out var methods,
            out var methodMapDiagnostic,
            moduleContext);

        var resolutionDiagnostic = unresolved > 0
            ? $"{unresolved} of {components.Count} component assemblies could not be resolved beside "
                + $"'{Path.GetFileName(analyzer.FilePath)}'; their methods are unnamed"
            : null;
        var diagnostic = CombineDiagnostics(methodMapDiagnostic, resolutionDiagnostic);
        return new ReadyToRunModel(
            methods, analyzer, providers, owned, listing,
            OwnerCompositeMissing: false, MapUsable: mapUsable, diagnostic);
    }

    // A component DLL: metadata is self, native code lives in the owner composite (opened sibling).
    private static ReadyToRunModel BuildComponent(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        var mvid = ReadMvid(analyzer);
        var providers = new Dictionary<Guid, AssemblyAnalyzer> { [mvid] = analyzer };
        if (info.OwnerCompositeExecutable is not { Length: > 0 } ownerName)
        {
            return new ReadyToRunModel(
                [], analyzer, providers, [], [],
                OwnerCompositeMissing: true, MapUsable: false, "owner composite is not named");
        }

        var directory = Path.GetDirectoryName(analyzer.FilePath) ?? ".";
        var owner = ReadyToRunComponentResolver.ResolveOwner(directory, ownerName);
        if (owner is null)
        {
            return new ReadyToRunModel(
                [], analyzer, providers, [], [],
                OwnerCompositeMissing: true, MapUsable: false,
                $"owner composite '{ownerName}' not found beside the component; native code unavailable");
        }

        // The owner composite holds the native code. Build a targeted component model from the
        // owner's tables instead of materializing every sibling metadata provider: all entry points
        // are still marked so funclet boundaries are correct, but only this component's metadata is
        // resolved. Disassembly routes through the owner bytes.
        string? tableDiagnostic = null;
        if (owner.ReadyToRunInfo is not { Status: ReadyToRunStatus.Valid, IsComposite: true } ownerInfo
            || !TryOpenTables(owner, ownerInfo, out var tables, out tableDiagnostic))
        {
            return new ReadyToRunModel(
                [], owner, providers, [owner], [],
                OwnerCompositeMissing: false, MapUsable: false,
                tableDiagnostic
                    ?? $"owner composite '{ownerName}' does not expose a usable ReadyToRun method map");
        }

        var ownerRaw = owner.RawBytes;
        var imageBase = owner.PeHeaders?.ImageBase ?? 0;
        var components = ReadyToRunCompositeReader.ReadComponents(
            ownerRaw, ownerInfo, imageBase, tables.AddressSpace);
        var sources = new List<ReadyToRunMethodMapReader.MethodMapSource>(components.Count);
        var listing = new List<ReadyToRunComponent>(components.Count);
        foreach (var component in components)
        {
            var isThisComponent = component.Mvid == mvid
                || string.Equals(component.Name, analyzer.AssemblyName, StringComparison.Ordinal);
            var name = component.Name ?? (isThisComponent ? analyzer.AssemblyName : null) ?? component.Mvid.ToString();
            sources.Add(new ReadyToRunMethodMapReader.MethodMapSource(
                name, component.Mvid, component.MethodDefEntryPointsFileOffset,
                component.MethodDefEntryPointsSize,
                isThisComponent ? analyzer.MethodDefs : [],
                isThisComponent ? analyzer.GetMetadataReader() : null));
            listing.Add(new ReadyToRunComponent(
                name, component.Mvid, 0, component.CoreHeaderRva,
                isThisComponent ? analyzer.FilePath : null, isThisComponent));
        }

        var moduleContext = ReadyToRunModuleContext.Create(
            ownerInfo, listing, id => id == mvid ? analyzer : null);
        var instance = Section(ownerInfo, ReadyToRunSectionType.InstanceMethodEntryPoints);
        var global = instance is { FileOffset: { } io }
            ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                io, instance.Size, null, owner.AssemblyName ?? "", Guid.Empty, [])
            : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;
        var mapUsable = TryBuild(
            tables,
            sources,
            global,
            out var allMethods,
            out var methodMapDiagnostic,
            moduleContext,
            mvid);

        var methods = allMethods
            .Where(m => m.Mvid == mvid || string.Equals(m.AssemblyName, analyzer.AssemblyName, StringComparison.Ordinal))
            .ToList();
        return new ReadyToRunModel(
            methods, owner, providers, [owner], listing,
            OwnerCompositeMissing: false, MapUsable: mapUsable, methodMapDiagnostic);
    }

    private readonly record struct Tables(
        R2RNativeReader Reader,
        ReadyToRunRuntimeFunctionTable RuntimeFunctions,
        ReadyToRunHotColdMap HotColdMap,
        ulong ImageBase,
        NativeAddressSpace AddressSpace);

    // Opens the image-wide tables (runtime functions + hot/cold). The MethodDefEntryPoints table is
    // per-source (top-level for a non-composite, per-component for a composite), so it is not required
    // here — a composite's top-level header carries no section 103.
    private static bool TryOpenTables(
        AssemblyAnalyzer analyzer,
        ReadyToRunInfo info,
        out Tables tables,
        out string? diagnostic)
    {
        tables = default;
        if (Section(info, ReadyToRunSectionType.RuntimeFunctions) is not { } runtimeFunctions)
        {
            diagnostic = "ReadyToRun RuntimeFunctions section is missing.";
            return false;
        }

        if (runtimeFunctions.FileOffset is not { } rfOffset)
        {
            diagnostic = "ReadyToRun RuntimeFunctions has no file-backed section range.";
            return false;
        }

        var addressSpace = NativeAddressSpace.Create(analyzer.RawBytes.Span);
        if (addressSpace is null)
        {
            diagnostic = "ReadyToRun RuntimeFunctions cannot be mapped through the image address space.";
            return false;
        }
        var imageBase = analyzer.PeHeaders?.ImageBase ?? 0;

        try
        {
            var reader = new R2RNativeReader(analyzer.RawBytes);
            if (!ReadyToRunRuntimeFunctionTable.TryRead(
                    reader,
                    rfOffset,
                    runtimeFunctions.Size,
                    info.Architecture,
                    imageBase,
                    addressSpace,
                    out var rfTable,
                    out diagnostic))
            {
                return false;
            }

            var hotCold = Section(info, ReadyToRunSectionType.HotColdMap);
            if (!ReadyToRunHotColdMap.TryRead(
                    reader,
                    addressSpace,
                    hotCold?.FileOffset,
                    hotCold?.Size ?? 0,
                    rfTable!.Count,
                    out var hotColdMap,
                    out diagnostic))
            {
                return false;
            }

            tables = new Tables(reader, rfTable, hotColdMap!, imageBase, addressSpace);
            return true;
        }
        catch (Exception ex) when (ex is OverflowException or OutOfMemoryException)
        {
            diagnostic = ex is OutOfMemoryException
                ? "ReadyToRun method-map tables exceeded available memory."
                : "ReadyToRun method-map table dimensions overflowed.";
            return false;
        }
    }

    private static bool TryBuild(
        Tables tables,
        IReadOnlyList<ReadyToRunMethodMapReader.MethodMapSource> sources,
        ReadyToRunMethodMapReader.GlobalInstanceSource? global,
        out List<ReadyToRunMethodEntry> methods,
        out string? diagnostic,
        ReadyToRunModuleContext? moduleContext = null,
        Guid? targetMvid = null)
    {
        try
        {
            methods = [.. ReadyToRunMethodMapReader.Build(
                tables.Reader, tables.RuntimeFunctions, tables.HotColdMap,
                tables.ImageBase, tables.AddressSpace, sources, global, moduleContext, targetMvid)];
            diagnostic = null;
            return true;
        }
        catch (Exception ex) when (
            ex is
                BadImageFormatException
                or IndexOutOfRangeException
                or ArgumentOutOfRangeException
                or OverflowException)
        {
            methods = [];
            diagnostic = $"ReadyToRun method-map tables are malformed: {ex.Message}";
            return false;
        }
    }

    private static string? CombineDiagnostics(string? first, string? second) =>
        (first, second) switch
        {
            (null, null) => null,
            (not null, null) => first,
            (null, not null) => second,
            _ => $"{first} {second}",
        };

    private static ReadyToRunModel Unavailable(AssemblyAnalyzer analyzer, string? diagnostic) =>
        new(
            [], analyzer, new Dictionary<Guid, AssemblyAnalyzer>(), [], [],
            OwnerCompositeMissing: false, MapUsable: false,
            diagnostic ?? "ReadyToRun method-map tables are unavailable.");

    private static ReadyToRunSectionEntry? Section(ReadyToRunInfo info, ReadyToRunSectionType type)
    {
        foreach (var s in info.Sections)
            if (s.Type == (int)type)
                return s;
        return null;
    }

    private static Guid ReadMvid(AssemblyAnalyzer analyzer)
    {
        var reader = analyzer.GetMetadataReader();
        return reader is not null ? reader.GetGuid(reader.GetModuleDefinition().Mvid) : Guid.Empty;
    }
}
