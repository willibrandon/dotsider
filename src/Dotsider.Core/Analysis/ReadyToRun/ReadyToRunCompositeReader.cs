using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Reads a composite ReadyToRun image's <c>ComponentAssemblies</c> table (each pointing at a
/// per-component core header with its own <c>MethodDefEntryPoints</c>), joined to the manifest's
/// assembly names and MVIDs. Each component's native code lives in the composite; its metadata is
/// resolved from a sibling assembly matched by MVID.
/// </summary>
internal static class ReadyToRunCompositeReader
{
    /// <summary>One component assembly of a composite: its identity and its per-component entry-points offset.</summary>
    /// <param name="Mvid">The component's module version id (the authoritative identity).</param>
    /// <param name="Name">The component's manifest name (best-effort display / resolution hint).</param>
    /// <param name="CoreHeaderRva">The RVA of the component's per-assembly ReadyToRun core header.</param>
    /// <param name="MethodDefEntryPointsFileOffset">The file offset of the component's <c>MethodDefEntryPoints</c> section.</param>
    /// <param name="MethodDefEntryPointsSize">The exact byte size of the component's <c>MethodDefEntryPoints</c> section.</param>
    internal readonly record struct Component(
        Guid Mvid,
        string? Name,
        int CoreHeaderRva,
        int MethodDefEntryPointsFileOffset,
        int MethodDefEntryPointsSize);

    private const int ComponentAssemblyRecordSize = 16;
    private const int GuidSize = 16;

    /// <summary>Reads the component assemblies of a composite image, or an empty list otherwise.</summary>
    public static List<Component> ReadComponents(
        ReadOnlyMemory<byte> raw, ReadyToRunInfo info, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var result = new List<Component>();
        var components = Section(info, ReadyToRunSectionType.ComponentAssemblies);
        if (components is not { FileOffset: { } componentsOffset }
            || components.Size < ComponentAssemblyRecordSize
            || !IsContained(componentsOffset, components.Size, raw.Length))
        {
            return result;
        }

        var names = ReadManifestNames(raw, Section(info, ReadyToRunSectionType.ManifestMetadata));
        var mvids = Section(info, ReadyToRunSectionType.ManifestAssemblyMvids);
        var span = raw.Span;

        var count = components.Size / ComponentAssemblyRecordSize;
        for (var i = 0; i < count; i++)
        {
            // Record: CorHeader {RVA, Size}, ReadyToRunCoreHeader {RVA, Size}.
            var row = componentsOffset + i * ComponentAssemblyRecordSize;
            var assemblyHeaderRva = BinaryPrimitives.ReadInt32LittleEndian(span[(row + 8)..]);
            if (!addressSpace.TryGetFileOffset(imageBase + (uint)assemblyHeaderRva, out var headerOffset, out _))
            {
                continue;
            }

            var core = ClassicReadyToRunHeaderReader.ReadCoreHeader(span, headerOffset, imageBase, addressSpace);
            if (core is not { } c)
            {
                continue;
            }

            if (ClassicReadyToRunHeaderReader.Section(c.Sections, ReadyToRunSectionType.MethodDefEntryPoints)
                    is not { FileOffset: { } mdeOffset, Size: > 0 } methodDefEntryPoints
                || !IsContained(mdeOffset, methodDefEntryPoints.Size, raw.Length))
            {
                continue;
            }

            result.Add(new Component(
                ReadMvid(span, mvids, i),
                i < names.Count ? names[i] : null,
                assemblyHeaderRva,
                mdeOffset,
                methodDefEntryPoints.Size));
        }

        return result;
    }

    private static Guid ReadMvid(ReadOnlySpan<byte> raw, ReadyToRunSectionEntry? mvids, int index)
    {
        if (mvids is not { FileOffset: { } offset, Size: >= GuidSize }
            || !IsContained(offset, mvids.Size, raw.Length)
            || index < 0)
        {
            return Guid.Empty;
        }

        var at = (long)offset + GuidSize * index;
        var sectionEnd = (long)offset + mvids.Size;
        return at + GuidSize <= sectionEnd
            ? new Guid(raw.Slice((int)at, GuidSize))
            : Guid.Empty;
    }

    private static List<string> ReadManifestNames(ReadOnlyMemory<byte> raw, ReadyToRunSectionEntry? manifest)
    {
        var names = new List<string>();
        if (manifest is not { FileOffset: { } offset, Size: > 0 }
            || !IsContained(offset, manifest.Size, raw.Length))
        {
            return names;
        }

        try
        {
            var image = ImmutableCollectionsMarshal.AsImmutableArray(raw.Slice(offset, manifest.Size).ToArray());
            using var provider = MetadataReaderProvider.FromMetadataImage(image);
            var reader = provider.GetMetadataReader();
            var count = reader.GetTableRowCount(TableIndex.AssemblyRef);
            for (var rid = 1; rid <= count; rid++)
                names.Add(reader.GetString(
                    reader.GetAssemblyReference(MetadataTokens.AssemblyReferenceHandle(rid)).Name));
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentException)
        {
            // A malformed manifest degrades to MVID-only identity.
        }

        return names;
    }

    private static ReadyToRunSectionEntry? Section(ReadyToRunInfo info, ReadyToRunSectionType type)
    {
        foreach (var s in info.Sections)
            if (s.Type == (int)type)
                return s;
        return null;
    }

    private static bool IsContained(int offset, int size, int length) =>
        size >= 0 && offset >= 0 && offset <= length - size;
}
