using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Builds the managed-method-to-native-code map from a ReadyToRun image's <c>MethodDefEntryPoints</c>
/// NativeArrays and the shared <c>RuntimeFunctions</c> table. One image contributes one source
/// (the image itself); a composite contributes one per component. Entry points across every source
/// are marked first so a method's owned runtime functions (its hot entry plus funclets, then any
/// cold block) group correctly before their ranges are materialized.
/// </summary>
internal static class ReadyToRunMethodMapReader
{
    /// <summary>A single assembly's <c>MethodDefEntryPoints</c> joined to its metadata. In a composite,
    /// there is one per component; the code and shared tables live in the composite.</summary>
    /// <param name="AssemblyName">The assembly's simple name.</param>
    /// <param name="Mvid">The assembly's module version id.</param>
    /// <param name="EntryPointsFileOffset">The file offset of the assembly's <c>MethodDefEntryPoints</c> section.</param>
    /// <param name="EntryPointsSize">The exact byte size of the assembly's <c>MethodDefEntryPoints</c> section.</param>
    /// <param name="MethodDefs">The assembly's method definitions, for names and signatures.</param>
    /// <param name="Metadata">The assembly's metadata reader, for resolving instantiation type names.</param>
    internal readonly record struct MethodMapSource(
        string AssemblyName,
        Guid Mvid,
        int EntryPointsFileOffset,
        int EntryPointsSize,
        IReadOnlyList<MethodDefInfo> MethodDefs,
        MetadataReader? Metadata);

    /// <summary>The single (per-image) <c>InstanceMethodEntryPoints</c> hashtable of instantiated generics.</summary>
    /// <param name="Offset">The section's file offset.</param>
    /// <param name="Size">The section's byte size.</param>
    /// <param name="Metadata">The metadata reader for resolving instantiation type names.</param>
    /// <param name="AssemblyName">The assembly to attribute a same-module entry to.</param>
    /// <param name="Mvid">That assembly's module version id.</param>
    /// <param name="MethodDefs">The same-module method definitions, for naming a same-module instantiation.</param>
    internal readonly record struct GlobalInstanceSource(
        int Offset, int Size, MetadataReader? Metadata, string AssemblyName, Guid Mvid,
        IReadOnlyList<MethodDefInfo> MethodDefs);

    private readonly record struct PendingEntry(
        string AssemblyName, Guid Mvid, MethodMapSource? Source, int Token,
        int EntryRuntimeFunctionId, bool IsGeneric, string? Instantiation);

    /// <summary>Materializes the method entries across every source.</summary>
    public static IReadOnlyList<ReadyToRunMethodEntry> Build(
        R2RNativeReader reader,
        ReadyToRunRuntimeFunctionTable runtimeFunctions,
        ReadyToRunHotColdMap hotColdMap,
        ulong imageBase,
        NativeAddressSpace addressSpace,
        IReadOnlyList<MethodMapSource> sources,
        GlobalInstanceSource? globalInstance,
        ReadyToRunModuleContext? moduleContext = null,
        Guid? targetMvid = null)
    {
        var isEntryPoint = new bool[runtimeFunctions.Count];
        var pending = new List<PendingEntry>();
        var traversalBudget = new ReadyToRunTraversalBudget();

        // Pass 1: mark the runtime functions every entry point starts — each assembly's ordinary
        // methods and the image's instantiated generics — before counting, so funclet grouping
        // never runs past the map.
        foreach (var source in sources)
            MarkMethodDefEntryPoints(
                reader, source, isEntryPoint, pending, traversalBudget, targetMvid);
        if (globalInstance is { } instance)
            MarkInstanceMethodEntryPoints(
                reader, instance, moduleContext, isEntryPoint, pending, traversalBudget, targetMvid);

        // Pass 2/3: count each method's runtime functions, then materialize its code ranges.
        var methodsByToken = new Dictionary<(string, int), MethodDefInfo>();
        var entries = new List<ReadyToRunMethodEntry>(pending.Count);
        foreach (var p in pending)
        {
            var (rfCount, coldCount) = CountRuntimeFunctions(isEntryPoint, hotColdMap, p.EntryRuntimeFunctionId);
            var ranges = BuildRanges(
                runtimeFunctions, hotColdMap, imageBase, addressSpace, p.EntryRuntimeFunctionId, rfCount, coldCount);

            var method = p.Source is { } src ? LookupMethod(methodsByToken, src, p.Token) : null;
            entries.Add(new ReadyToRunMethodEntry(
                p.AssemblyName, p.Mvid, p.Token,
                method?.DeclaringType, method?.Name, method?.Signature,
                ranges, p.EntryRuntimeFunctionId, rfCount,
                p.IsGeneric, p.Instantiation,
                TotalSize: ranges.Sum(r => r.Size)));
        }

        return entries;
    }

    private static void MarkMethodDefEntryPoints(
        R2RNativeReader reader, MethodMapSource source, bool[] isEntryPoint,
        List<PendingEntry> pending, ReadyToRunTraversalBudget traversalBudget, Guid? targetMvid)
    {
        var sectionEnd = GetSectionEnd(source.EntryPointsFileOffset, source.EntryPointsSize);
        var sectionReader = reader.Slice(source.EntryPointsFileOffset, source.EntryPointsSize);
        var array = new R2RNativeArray(sectionReader, source.EntryPointsFileOffset, sectionEnd);
        var methodDefinitionCount = source.Metadata?.MethodDefinitions.Count;
        if (methodDefinitionCount is null && source.MethodDefs.Count > 0)
        {
            methodDefinitionCount = source.MethodDefs.Count;
        }

        if (methodDefinitionCount is { } rowCount && array.Count > (uint)rowCount)
        {
            throw new BadImageFormatException(
                "ReadyToRun MethodDefEntryPoints count exceeds the module's MethodDef table.");
        }

        traversalBudget.Charge(
            (int)array.Count,
            "ReadyToRun MethodDefEntryPoints");
        for (uint index = 0; index < array.Count; index++)
        {
            if (!array.TryGetAt(index, out var elementOffset))
            {
                continue;
            }

            var entryId = DecodeRuntimeFunctionIndex(sectionReader, elementOffset);
            if (entryId < 0 || entryId >= isEntryPoint.Length)
            {
                continue;
            }

            isEntryPoint[entryId] = true;
            if (!ShouldMaterialize(targetMvid, source.Mvid))
            {
                continue;
            }

            var rid = index + 1;
            pending.Add(new PendingEntry(
                source.AssemblyName, source.Mvid, source, (int)(0x0600_0000 | rid),
                entryId, IsGeneric: false, Instantiation: null));
        }
    }

    private static void MarkInstanceMethodEntryPoints(
        R2RNativeReader reader, GlobalInstanceSource instance, ReadyToRunModuleContext? moduleContext,
        bool[] isEntryPoint, List<PendingEntry> pending,
        ReadyToRunTraversalBudget traversalBudget, Guid? targetMvid)
    {
        if (instance.Size <= 0)
        {
            return;
        }

        var sectionEnd = GetSectionEnd(instance.Offset, instance.Size);
        var sectionReader = reader.Slice(instance.Offset, instance.Size);
        var table = new R2RNativeHashtable(sectionReader, instance.Offset, sectionEnd);
        traversalBudget.Charge(
            table.BucketCount,
            "ReadyToRun InstanceMethodEntryPoints buckets");
        Func<int, MetadataReader?>? resolveMetadata =
            moduleContext is null ? null : moduleContext.ResolveMetadata;
        foreach (var entryOffset in table.AllEntryOffsets())
        {
            traversalBudget.Charge(
                1,
                "ReadyToRun InstanceMethodEntryPoints entries");
            // The payload is a method signature followed by the runtime-function index.
            var metadata = targetMvid is null ? instance.Metadata : null;
            Func<int, MetadataReader?>? metadataResolver =
                targetMvid is null ? resolveMetadata : null;
            var systemMetadata = targetMvid is null
                ? moduleContext?.ResolveSystemMetadata()
                : null;
            var sig = ReadyToRunSignatureWalker.ParseMethod(
                sectionReader, entryOffset, metadata, metadataResolver, systemMetadata);
            var entryId = DecodeRuntimeFunctionIndex(sectionReader, sig.Offset);
            if (entryId < 0 || entryId >= isEntryPoint.Length)
            {
                continue;
            }

            isEntryPoint[entryId] = true;

            // A module override attributes the instantiation to a component (composite); resolve it
            // there so its token, name, and owner identity are recovered rather than left unnamed.
            if (sig.ModuleIndex >= 0)
            {
                if (moduleContext?.Resolve(sig.ModuleIndex) is not { } module)
                {
                    if (targetMvid is not null)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!ShouldMaterialize(targetMvid, module.Mvid))
                    {
                        continue;
                    }

                    var resolvedMetadata = module.Provider?.GetMetadataReader();
                    var reparsed = resolvedMetadata is not null
                        ? ReadyToRunSignatureWalker.ParseMethod(
                            sectionReader, entryOffset, resolvedMetadata, resolveMetadata, systemMetadata)
                        : sig;
                    var crossToken = (reparsed.MethodToken & 0xFF00_0000) == 0x0600_0000 ? reparsed.MethodToken : 0;
                    var source = module.Provider is { } provider
                        ? new MethodMapSource(module.AssemblyName, module.Mvid, 0, 0, provider.MethodDefs, resolvedMetadata)
                        : (MethodMapSource?)null;
                    pending.Add(new PendingEntry(
                        module.AssemblyName, module.Mvid, source, crossToken,
                        entryId, IsGeneric: true, reparsed.InstantiationDisplay));
                    continue;
                }
            }

            if (!ShouldMaterialize(targetMvid, instance.Mvid))
            {
                continue;
            }

            // Same-module instantiation — its token is a MethodDef in the instance table's own module;
            // give it that module's source so its declaring type and name resolve (a cross-module token
            // we could not resolve stays unnamed).
            var token = sig is { CrossModule: false } && (sig.MethodToken & 0xFF00_0000) == 0x0600_0000
                ? sig.MethodToken
                : 0;
            var sameModuleSource = token != 0 && instance.MethodDefs.Count > 0
                ? new MethodMapSource(instance.AssemblyName, instance.Mvid, 0, 0, instance.MethodDefs, instance.Metadata)
                : (MethodMapSource?)null;
            pending.Add(new PendingEntry(
                instance.AssemblyName, instance.Mvid, sameModuleSource, token,
                entryId, IsGeneric: true, sig.InstantiationDisplay));
        }
    }

    private static bool ShouldMaterialize(Guid? targetMvid, Guid entryMvid) =>
        targetMvid is null || targetMvid.Value == entryMvid;

    private static int GetSectionEnd(int offset, int size)
    {
        var endOffset = (long)offset + size;
        if (offset < 0 || size < 0 || endOffset > int.MaxValue)
        {
            throw new BadImageFormatException("ReadyToRun section has an invalid file range.");
        }

        return (int)endOffset;
    }

    private static int DecodeRuntimeFunctionIndex(R2RNativeReader reader, int elementOffset)
    {
        var offset = reader.DecodeUnsigned(elementOffset, out var id);
        // Bit 0 flags a fixup slot list (inline, or at a negative delta when bit 1 is set); the
        // remaining bits are the runtime-function index. Fixup slots are consumed elsewhere.
        if ((id & 1) != 0)
        {
            if ((id & 2) != 0)
            {
                reader.DecodeUnsigned(offset, out var back);
                _ = back; // the shared-fixup back-step is only needed when resolving imports
            }

            id >>= 2;
        }
        else
        {
            id >>= 1;
        }

        return (int)id;
    }

    private static (int RuntimeFunctionCount, int ColdCount) CountRuntimeFunctions(
        bool[] isEntryPoint, ReadyToRunHotColdMap hotColdMap, int entryId)
    {
        var count = 0;
        var i = entryId;
        do
        {
            count++;
            i++;
        }
        while (i < isEntryPoint.Length && !isEntryPoint[i] && i < hotColdMap.FirstColdRuntimeFunction);

        return hotColdMap.TryGetColdRange(entryId, out _, out var coldCount)
            ? (count + coldCount, coldCount)
            : (count, 0);
    }

    private static List<ReadyToRunCodeRange> BuildRanges(
        ReadyToRunRuntimeFunctionTable runtimeFunctions, ReadyToRunHotColdMap hotColdMap,
        ulong imageBase, NativeAddressSpace addressSpace, int entryId, int rfCount, int coldCount)
    {
        var hotCount = rfCount - coldCount;
        var coldStart = coldCount > 0 && hotColdMap.TryGetColdRange(entryId, out var start, out _)
            ? start
            : 0;
        var ranges = new List<ReadyToRunCodeRange>(rfCount);

        for (var k = 0; k < rfCount; k++)
        {
            int rfIndex;
            ReadyToRunCodeRangeKind kind;
            if (k < hotCount)
            {
                rfIndex = entryId + k;
                kind = k == 0 ? ReadyToRunCodeRangeKind.HotEntry : ReadyToRunCodeRangeKind.Funclet;
            }
            else
            {
                rfIndex = coldStart + k - hotCount;
                kind = ReadyToRunCodeRangeKind.Cold;
            }

            if (rfIndex < 0 || rfIndex >= runtimeFunctions.Count)
                continue;

            var startRva = runtimeFunctions.StartRva(rfIndex);
            if (!NativeImageRange.TryAdd(imageBase, unchecked((uint)startRva), out var va))
                continue;
            int? fileOffset = addressSpace.TryGetFileOffset(va, out var off, out _) ? off : null;
            ranges.Add(new ReadyToRunCodeRange(kind, startRva, runtimeFunctions.Size(rfIndex), va, fileOffset));
        }

        return ranges;
    }

    private static MethodDefInfo? LookupMethod(
        Dictionary<(string, int), MethodDefInfo> cache, MethodMapSource source, int token)
    {
        if (cache.TryGetValue((source.AssemblyName, token), out var cached))
            return cached;

        // Populate the source's methods on first use.
        foreach (var method in source.MethodDefs)
            cache.TryAdd((source.AssemblyName, method.Token), method);

        return cache.GetValueOrDefault((source.AssemblyName, token));
    }
}
