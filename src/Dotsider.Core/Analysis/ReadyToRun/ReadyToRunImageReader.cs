using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Orchestrates the per-image ReadyToRun view: locates the shared runtime-function and hot/cold
/// tables, then joins each metadata source's <c>MethodDefEntryPoints</c> to them. A non-composite
/// image contributes a single source (itself); a composite resolves one source per component (by
/// name + MVID) with a shared code image; a component DLL delegates to its owner composite. Never
/// throws — a malformed table yields the entries that parsed.
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
        var self = Empty(analyzer);
        if (!TryOpenTables(analyzer, info, out var tables)
            || Section(info, ReadyToRunSectionType.MethodDefEntryPoints) is not { FileOffset: { } entryOffset })
        {
            return self;
        }

        var mvid = ReadMvid(analyzer);
        var source = new ReadyToRunMethodMapReader.MethodMapSource(
            analyzer.AssemblyName ?? "", mvid, entryOffset,
            analyzer.MethodDefs, analyzer.GetMetadataReader());

        var instance = Section(info, ReadyToRunSectionType.InstanceMethodEntryPoints);
        var global = instance is { FileOffset: { } io }
            ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                io, instance.Size, analyzer.GetMetadataReader(), analyzer.AssemblyName ?? "", mvid, analyzer.MethodDefs)
            : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;

        var methods = SafeBuild(tables, [source], global);
        return new ReadyToRunModel(
            methods, analyzer,
            new Dictionary<Guid, AssemblyAnalyzer> { [mvid] = analyzer },
            [], [], OwnerCompositeMissing: false, null);
    }

    // A composite opened directly: one source per resolved component; native code is this file.
    private static ReadyToRunModel BuildComposite(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        var self = Empty(analyzer);
        if (!TryOpenTables(analyzer, info, out var tables))
            return self;

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
        var manifest = Section(info, ReadyToRunSectionType.ManifestMetadata);
        MetadataReaderProvider? manifestProvider = null;
        List<ReadyToRunMethodEntry> methods;
        try
        {
            var manifestReader = OpenManifest(raw, manifest, out manifestProvider);
            var global = instance is { FileOffset: { } io }
                ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                    io, instance.Size, manifestReader, analyzer.AssemblyName ?? "", Guid.Empty, [])
                : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;
            methods = SafeBuild(tables, sources, global, moduleContext);
        }
        finally
        {
            manifestProvider?.Dispose();
        }

        var diagnostic = unresolved > 0
            ? $"{unresolved} of {components.Count} component assemblies could not be resolved beside "
                + $"'{Path.GetFileName(analyzer.FilePath)}'; their methods are unnamed"
            : null;
        return new ReadyToRunModel(
            methods, analyzer, providers, owned, listing, OwnerCompositeMissing: false, diagnostic);
    }

    // A component DLL: metadata is self, native code lives in the owner composite (opened sibling).
    private static ReadyToRunModel BuildComponent(AssemblyAnalyzer analyzer, ReadyToRunInfo info)
    {
        var mvid = ReadMvid(analyzer);
        var providers = new Dictionary<Guid, AssemblyAnalyzer> { [mvid] = analyzer };
        if (info.OwnerCompositeExecutable is not { Length: > 0 } ownerName)
        {
            return new ReadyToRunModel(
                [], analyzer, providers, [], [], OwnerCompositeMissing: true, "owner composite is not named");
        }

        var directory = Path.GetDirectoryName(analyzer.FilePath) ?? ".";
        var owner = ReadyToRunComponentResolver.ResolveOwner(directory, ownerName);
        if (owner is null)
        {
            return new ReadyToRunModel(
                [], analyzer, providers, [], [], OwnerCompositeMissing: true,
                $"owner composite '{ownerName}' not found beside the component; native code unavailable");
        }

        // The owner composite holds the native code. Build a targeted component model from the
        // owner's tables instead of materializing every sibling metadata provider: all entry points
        // are still marked so funclet boundaries are correct, but only this component's metadata is
        // resolved. Disassembly routes through the owner bytes.
        if (owner.ReadyToRunInfo is not { Status: ReadyToRunStatus.Valid, IsComposite: true } ownerInfo
            || !TryOpenTables(owner, ownerInfo, out var tables))
        {
            return new ReadyToRunModel(
                [], owner, providers, [owner], [], OwnerCompositeMissing: false,
                $"owner composite '{ownerName}' does not expose a usable ReadyToRun method map");
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
                isThisComponent ? analyzer.MethodDefs : [],
                isThisComponent ? analyzer.GetMetadataReader() : null));
            listing.Add(new ReadyToRunComponent(
                name, component.Mvid, 0, component.CoreHeaderRva,
                isThisComponent ? analyzer.FilePath : null, isThisComponent));
        }

        var moduleContext = ReadyToRunModuleContext.Create(
            ownerInfo, listing, id => id == mvid ? analyzer : null);
        var instance = Section(ownerInfo, ReadyToRunSectionType.InstanceMethodEntryPoints);
        var manifest = Section(ownerInfo, ReadyToRunSectionType.ManifestMetadata);
        MetadataReaderProvider? manifestProvider = null;
        List<ReadyToRunMethodEntry> allMethods;
        try
        {
            var manifestReader = OpenManifest(ownerRaw, manifest, out manifestProvider);
            var global = instance is { FileOffset: { } io }
                ? new ReadyToRunMethodMapReader.GlobalInstanceSource(
                    io, instance.Size, manifestReader, owner.AssemblyName ?? "", Guid.Empty, [])
                : (ReadyToRunMethodMapReader.GlobalInstanceSource?)null;
            allMethods = SafeBuild(tables, sources, global, moduleContext);
        }
        finally
        {
            manifestProvider?.Dispose();
        }

        var methods = allMethods
            .Where(m => m.Mvid == mvid || string.Equals(m.AssemblyName, analyzer.AssemblyName, StringComparison.Ordinal))
            .ToList();
        return new ReadyToRunModel(methods, owner, providers, [owner], listing, OwnerCompositeMissing: false, null);
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
    private static bool TryOpenTables(AssemblyAnalyzer analyzer, ReadyToRunInfo info, out Tables tables)
    {
        tables = default;
        if (Section(info, ReadyToRunSectionType.RuntimeFunctions) is not { FileOffset: { } rfOffset } runtimeFunctions)
            return false;

        var addressSpace = NativeAddressSpace.Create(analyzer.RawBytes.Span);
        if (addressSpace is null) return false;
        var imageBase = analyzer.PeHeaders?.ImageBase ?? 0;

        try
        {
            var reader = new R2RNativeReader(analyzer.RawBytes);
            var rfTable = new ReadyToRunRuntimeFunctionTable(
                reader, rfOffset, runtimeFunctions.Size, info.Architecture, imageBase, addressSpace);
            var hotCold = Section(info, ReadyToRunSectionType.HotColdMap);
            var hotColdMap = ReadyToRunHotColdMap.Read(
                reader, hotCold?.FileOffset, hotCold?.Size ?? 0, rfTable.Count);
            tables = new Tables(reader, rfTable, hotColdMap, imageBase, addressSpace);
            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static List<ReadyToRunMethodEntry> SafeBuild(
        Tables tables,
        IReadOnlyList<ReadyToRunMethodMapReader.MethodMapSource> sources,
        ReadyToRunMethodMapReader.GlobalInstanceSource? global,
        ReadyToRunModuleContext? moduleContext = null)
    {
        try
        {
            return [.. ReadyToRunMethodMapReader.Build(
                tables.Reader, tables.RuntimeFunctions, tables.HotColdMap,
                tables.ImageBase, tables.AddressSpace, sources, global, moduleContext)];
        }
        catch (Exception ex) when (ex is BadImageFormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static MetadataReader? OpenManifest(
        ReadOnlyMemory<byte> raw, ReadyToRunSectionEntry? manifest, out MetadataReaderProvider? provider)
    {
        provider = null;
        if (manifest is not { FileOffset: { } offset, Size: > 0 } || offset + manifest.Size > raw.Length)
            return null;
        try
        {
            var image = ImmutableCollectionsMarshal.AsImmutableArray(raw.Slice(offset, manifest.Size).ToArray());
            provider = MetadataReaderProvider.FromMetadataImage(image);
            return provider.GetMetadataReader();
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentException)
        {
            provider?.Dispose();
            provider = null;
            return null;
        }
    }

    private static ReadyToRunModel Empty(AssemblyAnalyzer analyzer) =>
        new([], analyzer, new Dictionary<Guid, AssemblyAnalyzer>(), [], [], OwnerCompositeMissing: false, null);

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
