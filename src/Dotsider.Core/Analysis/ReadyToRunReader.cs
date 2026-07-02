using System.Buffers.Binary;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Walks the section table that follows a Native AOT binary's ReadyToRun header. Each row
/// records a section id and its location as an absolute virtual address; the row layout
/// depends on the format version, so it is dispatched on the header's entry size rather
/// than assumed. Malformed tables yield an empty list rather than throwing.
/// </summary>
internal static class ReadyToRunReader
{
    /// <summary>SectionId of the frozen object region, which holds frozen string literals.</summary>
    internal const int FrozenObjectRegion = 206;

    /// <summary>SectionId of the dehydrated data region (ELF/Mach-O; reconstructs other regions at startup).</summary>
    internal const int DehydratedData = 207;

    /// <summary>First SectionId of the readonly blob region range (300..399).</summary>
    internal const int ReadonlyBlobRegionStart = 300;

    /// <summary>SectionId of the embedded NativeFormat metadata blob (ReadonlyBlobRegionStart + EmbeddedMetadata).</summary>
    internal const int EmbeddedMetadata = 313;

    private const int HeaderSize = 16;

    /// <summary>
    /// Reads the ReadyToRun section table.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="info">The validated header facts from <see cref="NativeAotDetector"/>.</param>
    /// <param name="addressSpace">The image's virtual-address to file-offset map.</param>
    /// <returns>The section table, or an empty list when the rows cannot be parsed.</returns>
    internal static IReadOnlyList<RtrSection> ReadSections(
        ReadOnlySpan<byte> bytes, NativeAotInfo info, NativeAddressSpace addressSpace)
    {
        var pointerSize = addressSpace.PointerSize;
        var oldRowSize = 8 + 2 * pointerSize; // .NET 8-10: SectionId, Flags, Start, End
        var newRowSize = 8 + pointerSize;     // .NET 11+:   SectionId, Length, Start
        var hasEndField = info.EntrySize == oldRowSize;
        if (!hasEndField && info.EntrySize != newRowSize) return [];

        var rowsStart = info.HeaderOffset + HeaderSize;
        var sections = new List<RtrSection>(info.SectionCount);

        for (var i = 0; i < info.SectionCount; i++)
        {
            var row = rowsStart + i * info.EntrySize;
            if (row + info.EntrySize > bytes.Length) break;

            var sectionId = BinaryPrimitives.ReadInt32LittleEndian(bytes[row..]);

            ulong start;
            long size;
            if (hasEndField)
            {
                start = ReadPointer(bytes, row + 8, pointerSize);
                var end = ReadPointer(bytes, row + 8 + pointerSize, pointerSize);
                // TypeManagerIndirection (204) records End = 0; its size is not expressed here.
                size = end > start ? (long)(end - start) : 0;
            }
            else
            {
                size = BinaryPrimitives.ReadInt32LittleEndian(bytes[(row + 4)..]);
                start = ReadPointer(bytes, row + 8, pointerSize);
            }

            int? fileOffset = addressSpace.TryGetFileOffset(start, out var offset, out _)
                ? offset
                : null;

            sections.Add(new RtrSection(sectionId, SectionName(sectionId), start, size, fileOffset));
        }

        return sections;
    }

    /// <summary>Returns the file range of a section when it is file-backed, or null.</summary>
    internal static (int Offset, int Length)? FileRange(RtrSection section)
    {
        if (section.FileOffset is not { } offset || section.Size <= 0) return null;
        return (offset, (int)section.Size);
    }

    private static ulong ReadPointer(ReadOnlySpan<byte> bytes, int offset, int pointerSize) =>
        pointerSize == 8
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..])
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    private static string SectionName(int sectionId) => sectionId switch
    {
        200 => "StringTable",
        201 => "GCStaticRegion",
        202 => "ThreadStaticRegion",
        204 => "TypeManagerIndirection",
        205 => "EagerCctor",
        206 => "FrozenObjectRegion",
        207 => "DehydratedData",
        208 => "ThreadStaticOffsetRegion",
        209 => "InterfaceDispatchCellInfoRegion",
        210 => "InterfaceDispatchCellRegion",
        212 => "ImportAddressTables",
        213 => "ModuleInitializerList",
        >= ReadonlyBlobRegionStart and <= 399 => ReadonlyBlobName(sectionId - ReadonlyBlobRegionStart),
        _ => $"Section {sectionId}",
    };

    /// <summary>
    /// Names a readonly blob region by its <c>ReflectionMapBlob</c> id (the section id minus
    /// the readonly blob region base).
    /// </summary>
    private static string ReadonlyBlobName(int blobId) => blobId switch
    {
        1 => "ReadonlyBlob (TypeMap)",
        2 => "ReadonlyBlob (ArrayMap)",
        3 => "ReadonlyBlob (PointerTypeMap)",
        4 => "ReadonlyBlob (FunctionPointerTypeMap)",
        6 => "ReadonlyBlob (InvokeMap)",
        7 => "ReadonlyBlob (VirtualInvokeMap)",
        8 => "ReadonlyBlob (CommonFixupsTable)",
        9 => "ReadonlyBlob (FieldAccessMap)",
        10 => "ReadonlyBlob (CctorContextMap)",
        11 => "ReadonlyBlob (ByRefTypeMap)",
        13 => "ReadonlyBlob (EmbeddedMetadata)",
        24 => "ReadonlyBlob (ResourceIndex)",
        25 => "ReadonlyBlob (ResourceData)",
        26 => "ReadonlyBlob (StackTraceEmbeddedMetadata)",
        27 => "ReadonlyBlob (StackTraceMethodRvaToTokenMap)",
        28 => "ReadonlyBlob (StackTraceLineNumbers)",
        29 => "ReadonlyBlob (StackTraceDocuments)",
        30 => "ReadonlyBlob (NativeLayoutInfo)",
        32 => "ReadonlyBlob (GenericsHashtable)",
        _ => $"ReadonlyBlob #{blobId}",
    };
}
