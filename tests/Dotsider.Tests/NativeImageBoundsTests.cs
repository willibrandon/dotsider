using Dotsider.Core.Analysis;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Verifies untrusted PE, ELF, and Mach-O ranges are validated before arithmetic, narrowing,
/// slicing, or publication through the native-analysis facade.
/// </summary>
[TestClass]
public sealed class NativeImageBoundsTests
{
    private const ulong ElfImageBase = 0x400000;
    private const ulong MachOImageBase = 0x1_0000_0000;
    private const ulong PeImageBase = 0x1_4000_0000;

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies exact-end and empty ranges are accepted while one-byte overflow, unrepresentable,
    /// and overflowing table ranges are rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeImageRange_Boundaries_ValidateWithoutOverflow()
    {
        Assert.IsTrue(NativeImageRange.TryGet(16, 8, 8, out var offset, out var length));
        Assert.AreEqual(8, offset);
        Assert.AreEqual(8, length);
        Assert.IsTrue(NativeImageRange.TryGet(16, 16, 0, out offset, out length));
        Assert.AreEqual(16, offset);
        Assert.AreEqual(0, length);

        Assert.IsFalse(NativeImageRange.TryGet(16, 16, 1, out _, out _));
        Assert.IsFalse(NativeImageRange.TryGet(16, ulong.MaxValue, 2, out _, out _));
        Assert.IsFalse(NativeImageRange.TryGetTable(
            16, 8, ulong.MaxValue, 8, 8, out _, out _));
        Assert.IsTrue(NativeImageRange.TryAlignUp(13, 4, out var aligned));
        Assert.AreEqual(16UL, aligned);
        Assert.IsFalse(NativeImageRange.TryAlignUp(ulong.MaxValue, 4, out _));
    }

    /// <summary>
    /// Verifies valid PE32, PE32+, ELF64, and Mach-O segments map their first and last file-backed
    /// bytes with the exact number of remaining bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAddressSpace_ValidFormats_MapExactBoundaries()
    {
        var pe32 = BuildPe32();
        AssertMapping(
            NativeAddressSpace.Create(pe32),
            0x401000,
            0x400,
            0x200);

        var pe32Plus = SyntheticImageBuilders.BuildPe(
            0x8664, [], exceptionRva: 0, exceptionSize: 0, PeImageBase);
        AssertMapping(
            NativeAddressSpace.Create(pe32Plus),
            PeImageBase + 0x1000,
            0x400,
            0x200);

        var elf = BuildElfAddressSpace();
        AssertMapping(NativeAddressSpace.Create(elf), ElfImageBase, 0, elf.Length);

        var machO = BuildMachOAddressSpace();
        AssertMapping(NativeAddressSpace.Create(machO), MachOImageBase, 0, machO.Length);
    }

    /// <summary>
    /// Verifies PE header, optional-header, section-table, raw-section, and virtual-address
    /// overflows fail closed without publishing a partial address map.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAddressSpace_MalformedPeHeaders_ReturnNull()
    {
        var image = SyntheticImageBuilders.BuildPe(
            0x8664, [], exceptionRva: 0, exceptionSize: 0, PeImageBase);
        var peHeader = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
        var optional = peHeader + 24;
        var section = optional + 240;

        Assert.IsNull(NativeAddressSpace.Create(PatchUInt32(image, 0x3C, uint.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt16(image, peHeader + 20, 31)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt16(image, peHeader + 20, 111)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt16(image, optional, 0x777)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt16(image, peHeader + 6, ushort.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt32(image, section + 20, uint.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt64(image, optional + 24, ulong.MaxValue)));
    }

    /// <summary>
    /// Verifies overflowing ELF program-header tables, file extents, and virtual extents are
    /// rejected before their unsigned fields are narrowed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAddressSpace_MalformedElfProgramHeaders_ReturnNull()
    {
        var image = BuildElfAddressSpace();
        const int programHeader = 64;

        Assert.IsNull(NativeAddressSpace.Create(PatchUInt64(image, 0x20, ulong.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt16(image, 0x38, ushort.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, programHeader + 8, ulong.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, programHeader + 32, ulong.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, programHeader + 16, ulong.MaxValue)));
    }

    /// <summary>
    /// Verifies malformed Mach-O command regions and segment extents fail closed rather than
    /// producing a partial address space.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAddressSpace_MalformedMachOCommands_ReturnNull()
    {
        var image = BuildMachOAddressSpace();
        const int segmentCommand = 32;

        Assert.IsNull(NativeAddressSpace.Create(PatchUInt32(image, 16, 2)));
        Assert.IsNull(NativeAddressSpace.Create(PatchUInt32(image, 20, uint.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt32(image, segmentCommand + 4, uint.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, segmentCommand + 40, ulong.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, segmentCommand + 48, ulong.MaxValue)));
        Assert.IsNull(NativeAddressSpace.Create(
            PatchUInt64(image, segmentCommand + 24, ulong.MaxValue)));
    }

    /// <summary>
    /// Verifies ELF section tables, section-name tables, file extents, and names reject
    /// overflowing values as one malformed structural unit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ElfSections_OverflowingStructuralFields_ReturnEmpty()
    {
        var image = SyntheticImageBuilders.BuildElf(
            (".text", 0x401000, new byte[] { 0x90 }));
        var table = (int)BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(40));
        const int userSection = 64;
        var section = table + userSection;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(60));

        Assert.IsEmpty(ElfImageReader.ReadSections(PatchUInt64(image, 40, ulong.MaxValue)));
        Assert.IsEmpty(ElfImageReader.ReadSections(PatchUInt16(image, 62, count)));
        Assert.IsEmpty(ElfImageReader.ReadSections(PatchUInt32(image, section, uint.MaxValue)));
        Assert.IsEmpty(ElfImageReader.ReadSections(
            PatchUInt64(image, section + 24, ulong.MaxValue)));
        Assert.IsEmpty(ElfImageReader.ReadSections(
            PatchUInt64(image, section + 32, ulong.MaxValue)));

        var noBits = SyntheticImageBuilders.BuildElf(
            (".bss", 0x500000, Array.Empty<byte>(), 8u, 0u));
        var sections = ElfImageReader.ReadSections(noBits);
        Assert.IsNotEmpty(sections);
        Assert.IsFalse(ElfImageReader.TryMapAddress(sections, 0x500000, out _, out _));
    }

    /// <summary>
    /// Verifies GNU build-id and debug-link records reject padded-length overflow and truncated
    /// CRC data without indexing beyond their containing sections.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ElfSidecarRecords_OverflowingLengths_ReturnFalse()
    {
        var note = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(note, uint.MaxValue);
        var image = SyntheticImageBuilders.BuildElf((".note.gnu.build-id", 0, note));
        Assert.IsFalse(ElfImageReader.TryReadBuildId(image, out _));

        note = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(note, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(note.AsSpan(4), uint.MaxValue);
        image = SyntheticImageBuilders.BuildElf((".note.gnu.build-id", 0, note));
        Assert.IsFalse(ElfImageReader.TryReadBuildId(image, out _));

        image = SyntheticImageBuilders.BuildElf(
            (".gnu_debuglink", 0, new byte[] { (byte)'a', 0, 0, 0 }));
        Assert.IsFalse(ElfImageReader.TryReadDebugLink(image, out _, out _));
    }

    /// <summary>
    /// Verifies Mach-O fat slices, command regions, section extents, function-start data, and
    /// symbol tables reject overflowing ranges.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MachOReaders_OverflowingRanges_ReturnAbsent()
    {
        var thin = SyntheticImageBuilders.BuildMachO(
            [("__TEXT", MachOImageBase,
                new[] { ("__text", MachOImageBase, 0x8000_0400u, new byte[] { 0xC3 }) })],
            symbols: [("_main", 0x0F, 1, MachOImageBase)],
            functionStarts: [1, 0]);

        Assert.IsEmpty(MachOImageReader.ReadSectionList(PatchUInt32(thin, 20, uint.MaxValue)));

        var sectionCommand = FindMachOCommand(thin, 0x19);
        Assert.IsGreaterThanOrEqualTo(0, sectionCommand);
        Assert.IsEmpty(MachOImageReader.ReadSectionList(
            PatchUInt64(thin, sectionCommand + 72 + 40, ulong.MaxValue)));

        var functionStarts = FindMachOCommand(thin, 0x26);
        Assert.IsGreaterThanOrEqualTo(0, functionStarts);
        Assert.IsFalse(MachOImageReader.TryGetFunctionStarts(
            PatchUInt32(thin, functionStarts + 8, uint.MaxValue), out _, out _));

        var symbolTable = FindMachOCommand(thin, 0x2);
        Assert.IsGreaterThanOrEqualTo(0, symbolTable);
        Assert.IsFalse(MachOImageReader.TryGetSymtab(
            PatchUInt32(thin, symbolTable + 8, uint.MaxValue), out _));

        var fat = SyntheticImageBuilders.BuildFat(thin);
        Assert.IsEmpty(MachOImageReader.ReadFatSlices(PatchUInt32(fat, 16, uint.MaxValue)));
        Assert.IsEmpty(MachOImageReader.ReadFatSlices(PatchUInt32(fat, 20, uint.MaxValue)));
    }

    /// <summary>
    /// Verifies PE exception and debug-directory consumers reject ranges larger than the
    /// containing file-backed segment.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PeConsumers_OverflowingDirectorySizes_ReturnAbsent()
    {
        var pdata = SyntheticImageBuilders.BuildPe(
            0x8664,
            new byte[0x200],
            exceptionRva: 0x1000,
            exceptionSize: uint.MaxValue,
            PeImageBase);
        Assert.IsEmpty(PdataReader.ReadBoundaries(pdata));

        var codeView = SyntheticImageBuilders.BuildPe(
            0x8664, new byte[0x200], exceptionRva: 0, exceptionSize: 0, PeImageBase);
        var peHeader = BinaryPrimitives.ReadInt32LittleEndian(codeView.AsSpan(0x3C));
        var debugDirectory = peHeader + 24 + 112 + 6 * 8;
        BinaryPrimitives.WriteUInt32LittleEndian(codeView.AsSpan(debugDirectory), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(
            codeView.AsSpan(debugDirectory + 4),
            uint.MaxValue);

        Assert.IsNull(PeCodeView.TryRead(codeView));
    }

    /// <summary>
    /// Verifies a real host-native NativeAOT image with an overflowing native container field
    /// remains analyzable and exposes no guessed ReadyToRun mappings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyAnalyzer_RealNativeAotWithOverflowingHeader_DegradesGracefully()
    {
        TestSkip.When(
            Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        var patched = PatchHostNativeHeader(bytes);

        using var analyzer = new AssemblyAnalyzer(
            patched,
            Samples.NativeAotConsoleExe!);

        Assert.AreEqual(patched.Length, analyzer.FileSize);
        Assert.IsFalse(analyzer.HasMetadata);
        Assert.IsEmpty(analyzer.ReadyToRunSections);
        _ = analyzer.Imports;
        _ = analyzer.NativeSymbols;
    }

    private static void AssertMapping(
        NativeAddressSpace? addressSpace,
        ulong virtualAddress,
        int fileOffset,
        int size)
    {
        Assert.IsNotNull(addressSpace);
        Assert.IsTrue(addressSpace.TryGetFileOffset(
            virtualAddress,
            out var firstOffset,
            out var firstAvailable));
        Assert.AreEqual(fileOffset, firstOffset);
        Assert.AreEqual(size, firstAvailable);

        Assert.IsTrue(addressSpace.TryGetFileOffset(
            virtualAddress + (ulong)size - 1,
            out var lastOffset,
            out var lastAvailable));
        Assert.AreEqual(fileOffset + size - 1, lastOffset);
        Assert.AreEqual(1, lastAvailable);
        Assert.IsFalse(addressSpace.TryGetFileOffset(
            virtualAddress + (ulong)size,
            out _,
            out _));
    }

    private static byte[] BuildElfAddressSpace()
    {
        var image = new byte[0x200];
        image[0] = 0x7F;
        image[1] = (byte)'E';
        image[2] = (byte)'L';
        image[3] = (byte)'F';
        image[4] = 2;
        image[5] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x20), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x36), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x38), 1);

        const int programHeader = 64;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(programHeader), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(programHeader + 8), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(programHeader + 16),
            ElfImageBase);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(programHeader + 32),
            (ulong)image.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(programHeader + 40),
            (ulong)image.Length);
        return image;
    }

    private static byte[] BuildMachOAddressSpace()
    {
        var image = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(image, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), 72);

        const int segmentCommand = 32;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(segmentCommand), 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(segmentCommand + 4), 72);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(segmentCommand + 24),
            MachOImageBase);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(segmentCommand + 32),
            (ulong)image.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(segmentCommand + 40), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(
            image.AsSpan(segmentCommand + 48),
            (ulong)image.Length);
        return image;
    }

    private static byte[] BuildPe32()
    {
        const int peHeader = 0x80;
        const int optionalSize = 224;
        const int rawOffset = 0x400;
        const int rawSize = 0x200;
        var image = new byte[rawOffset + rawSize];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x3C), peHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(peHeader), 0x0000_4550);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(peHeader + 4), 0x14C);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(peHeader + 6), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(
            image.AsSpan(peHeader + 20),
            optionalSize);

        var optional = peHeader + 24;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optional), 0x10B);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optional + 28), 0x400000);

        var section = optional + optionalSize;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(section + 12), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(section + 16), rawSize);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(section + 20), rawOffset);
        return image;
    }

    private static int FindMachOCommand(byte[] image, uint commandType)
    {
        var count = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(16));
        var command = 32;
        for (var i = 0; i < count; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(command)) == commandType)
                return command;
            command += (int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(command + 4));
        }

        return -1;
    }

    private static byte[] PatchHostNativeHeader(byte[] source)
    {
        var image = source.ToArray();
        if (image.Length >= 0x40 && image[0] == (byte)'M' && image[1] == (byte)'Z')
        {
            var peHeader = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
            var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(peHeader + 20));
            var section = peHeader + 24 + optionalSize;
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(section + 20), uint.MaxValue);
            return image;
        }

        if (ElfImageReader.IsElf(image))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x20), ulong.MaxValue);
            return image;
        }

        if (MachOImageReader.IsMachO(image))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), uint.MaxValue);
            return image;
        }

        Assert.Fail("The NativeAOT fixture is not a recognized native image.");
        return image;
    }

    private static byte[] PatchUInt16(byte[] source, int offset, ushort value)
    {
        var patched = source.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(patched.AsSpan(offset), value);
        return patched;
    }

    private static byte[] PatchUInt32(byte[] source, int offset, uint value)
    {
        var patched = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(offset), value);
        return patched;
    }

    private static byte[] PatchUInt64(byte[] source, int offset, ulong value)
    {
        var patched = source.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(patched.AsSpan(offset), value);
        return patched;
    }
}
