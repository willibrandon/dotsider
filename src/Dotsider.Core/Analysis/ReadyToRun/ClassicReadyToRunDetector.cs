using System.Buffers.Binary;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Detects a classic crossgen2 ReadyToRun image and reads its header-level facts. Distinct from
/// <see cref="NativeAotDetector"/> (which handles the Native AOT flavour of the RTR header): a
/// classic R2R image keeps full ECMA-335 metadata and locates its header via the COR
/// <c>ManagedNativeHeader</c> directory (managed PE) or an <c>RTR_HEADER</c> export (composite /
/// native-style). Never throws — a claimed-but-broken header yields a diagnostic status.
/// </summary>
internal static class ClassicReadyToRunDetector
{
    /// <summary>
    /// The current major version, from <c>readytorun.h</c> (<c>READYTORUN_MAJOR_VERSION</c>). A major
    /// newer than this is an unknown future format and is surfaced as unsupported.
    /// </summary>
    internal const int CurrentMajorVersion = 24;

    /// <summary>
    /// The inspection floor — deliberately <em>not</em> the runtime's <c>MINIMUM_READYTORUN_MAJOR_VERSION</c>.
    /// Runtime <em>load</em> compatibility is not the same as dotsider's inspectable-format window: the
    /// runtime constant tracks the newest runtime and rejects the older majors an installed SDK still
    /// emits (today's SDK produces v16, while the checked-in runtime header is at 24). This floor is the
    /// oldest major proven to parse by a real fixture in this repo; a major below it is a historical
    /// layout this reader does not claim to support, surfaced as unsupported rather than trusted.
    /// </summary>
    internal const int MinimumInspectableMajorVersion = 16;

    private const uint FlagPartial = 0x0000_0004;
    private const uint FlagComponent = 0x0000_0020;
    private const string HeaderExportName = "RTR_HEADER";

    /// <summary>
    /// Probes <paramref name="analyzer"/> for a ReadyToRun header. Returns null when the image does
    /// not claim R2R at all (no managed-native-header directory and no <c>RTR_HEADER</c> export);
    /// otherwise a <see cref="ReadyToRunInfo"/> whose <see cref="ReadyToRunInfo.Status"/> reflects
    /// whether it is valid, corrupt, an unsupported version, or an unrecognized native header.
    /// </summary>
    internal static ReadyToRunInfo? Detect(AssemblyAnalyzer analyzer)
    {
        var raw = analyzer.RawBytes.Span;
        var imageBase = analyzer.PeHeaders?.ImageBase ?? 0;
        var arch = ReadMachine(raw);

        // Locate the header: a managed PE points its COR ManagedNativeHeader directory at it; a
        // composite (metadata-less native PE) exports it as RTR_HEADER. A plain managed image has
        // neither — return before touching the export table so BinaryKind stays cheap.
        var fromManagedNativeHeader = analyzer.ClrHeader?.ManagedNativeHeader is { Size: > 0 } dir
            ? dir.RelativeVirtualAddress
            : (int?)null;
        var fromExport = fromManagedNativeHeader is null && !analyzer.HasMetadata
            ? FindExportRva(analyzer, HeaderExportName)
            : null;
        if (fromManagedNativeHeader is null && fromExport is null)
            return null; // does not claim ReadyToRun

        var headerRva = fromManagedNativeHeader ?? fromExport!.Value;
        var addressSpace = NativeAddressSpace.Create(raw);
        if (addressSpace is null
            || !addressSpace.TryGetFileOffset(imageBase + (uint)headerRva, out var headerOffset, out _))
        {
            return Unreadable(headerRva, arch, "the ReadyToRun header RVA does not map to a file offset");
        }

        var header = ClassicReadyToRunHeaderReader.ReadFullHeader(raw, headerOffset, imageBase, addressSpace);
        if (header is not { } h)
            return Unreadable(headerRva, arch, "the ReadyToRun header could not be read");

        // A managed-native-header directory that is not an RTR signature is a legacy/unknown native
        // header — surfaced, but the binary stays classified as managed.
        if (h.Signature != ClassicReadyToRunHeaderReader.Signature)
        {
            var status = fromManagedNativeHeader is not null
                ? ReadyToRunStatus.UnrecognizedNativeHeader
                : ReadyToRunStatus.Corrupt;
            return new ReadyToRunInfo(
                h.Signature, h.MajorVersion, h.MinorVersion, h.Flags,
                IsComposite: false, IsComponent: false, IsPartialImage: false,
                headerRva, h.SectionCount, status,
                $"managed native header present but not a ReadyToRun signature (0x{h.Signature:X8})",
                arch, OwnerCompositeExecutable: null, h.Sections, [], 0, 0);
        }

        var isComponent = (h.Flags & FlagComponent) != 0;
        // A composite global image carries the ComponentAssemblies table (the authoritative signal —
        // it may still have a COR header, present only for PDB generation).
        var isComposite = ClassicReadyToRunHeaderReader.Section(
            h.Sections, ReadyToRunSectionType.ComponentAssemblies) is not null;

        // A section table claiming more rows than the file holds is corrupt — surface it rather than
        // parse a valid-looking image with critical sections silently missing.
        if (h.Sections.Count < h.SectionCount)
        {
            return new ReadyToRunInfo(
                h.Signature, h.MajorVersion, h.MinorVersion, h.Flags,
                isComposite, isComponent, (h.Flags & FlagPartial) != 0,
                headerRva, h.SectionCount, ReadyToRunStatus.Corrupt,
                $"the ReadyToRun section table is truncated ({h.Sections.Count} of {h.SectionCount} rows fit the file)",
                arch, isComponent ? ReadOwnerCompositeExecutable(raw, h.Sections, imageBase, addressSpace) : null,
                h.Sections, [], 0, 0);
        }

        var versionStatus = h.MajorVersion is >= MinimumInspectableMajorVersion and <= CurrentMajorVersion
            ? ReadyToRunStatus.Valid
            : ReadyToRunStatus.UnsupportedVersion;
        var diagnostic = versionStatus == ReadyToRunStatus.UnsupportedVersion
            ? h.MajorVersion > CurrentMajorVersion
                ? $"ReadyToRun major version {h.MajorVersion} is newer than the supported version "
                    + $"{CurrentMajorVersion}; results may be incomplete"
                : $"ReadyToRun major version {h.MajorVersion} predates the supported crossgen2 range "
                    + $"({MinimumInspectableMajorVersion}–{CurrentMajorVersion}); its layout is not trusted"
            : null;

        var owner = isComponent
            ? ReadOwnerCompositeExecutable(raw, h.Sections, imageBase, addressSpace)
            : null;

        return new ReadyToRunInfo(
            h.Signature, h.MajorVersion, h.MinorVersion, h.Flags,
            isComposite, isComponent, (h.Flags & FlagPartial) != 0,
            headerRva, h.SectionCount, versionStatus, diagnostic, arch, owner,
            h.Sections, Components: [], MethodCount: 0, InstanceMethodCount: 0);
    }

    private static ReadyToRunInfo Unreadable(int headerRva, NativeArchitecture arch, string diagnostic) =>
        new(0, 0, 0, 0, false, false, false, headerRva, 0, ReadyToRunStatus.Corrupt, diagnostic,
            arch, null, [], [], 0, 0);

    private static int? FindExportRva(AssemblyAnalyzer analyzer, string name)
    {
        foreach (var export in analyzer.Exports)
            if (string.Equals(export.Name, name, StringComparison.Ordinal))
                return export.Rva;
        return null;
    }

    private static string? ReadOwnerCompositeExecutable(
        ReadOnlySpan<byte> raw, IReadOnlyList<ReadyToRunSectionEntry> sections,
        ulong imageBase, NativeAddressSpace addressSpace)
    {
        var section = ClassicReadyToRunHeaderReader.Section(
            sections, ReadyToRunSectionType.OwnerCompositeExecutable);
        if (section is not { Size: > 0 } s
            || !addressSpace.TryGetFileOffset(imageBase + (uint)s.Rva, out var offset, out var available))
        {
            return null;
        }

        var length = Math.Min(s.Size, available);
        var span = raw.Slice(offset, length);
        var nul = span.IndexOf((byte)0);
        if (nul >= 0) span = span[..nul];
        return span.Length == 0 ? null : System.Text.Encoding.UTF8.GetString(span);
    }

    private static NativeArchitecture ReadMachine(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 0x40) return NativeArchitecture.Unknown;
        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(raw[0x3C..]);
        if (peOffset < 0 || peOffset + 6 > raw.Length) return NativeArchitecture.Unknown;
        if (BinaryPrimitives.ReadUInt32LittleEndian(raw[peOffset..]) != 0x0000_4550) // "PE\0\0"
            return NativeArchitecture.Unknown;
        return BinaryPrimitives.ReadUInt16LittleEndian(raw[(peOffset + 4)..]) switch
        {
            0x8664 => NativeArchitecture.X64,
            0xAA64 => NativeArchitecture.Arm64,
            0x014C => NativeArchitecture.X86,
            0x01C0 or 0x01C2 or 0x01C4 => NativeArchitecture.Arm32,
            0x5064 => NativeArchitecture.RiscV64,
            0x6264 => NativeArchitecture.LoongArch64,
            _ => NativeArchitecture.Unknown,
        };
    }
}
