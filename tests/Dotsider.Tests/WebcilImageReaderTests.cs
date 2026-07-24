using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.Wasm;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Verifies that Webcil payloads, sections, and method bodies remain inside their validated bounds.
/// </summary>
[TestClass]
public sealed class WebcilImageReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Both Webcil revisions open in bare and minimally wrapped form.
    /// </summary>
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(0, true)]
    [DataRow(1, false)]
    [DataRow(1, true)]
    public void Open_ValidImage_ExposesMetadataAndIl(int version, bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(version, wrapped);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        Assert.AreEqual(version, reader.Info.VersionMajor);
        Assert.AreEqual(wrapped, reader.Info.IsWasmWrapped);
        Assert.AreEqual(image.PayloadOffset, reader.Info.PayloadOffset);
        IReadOnlyList<SectionInfo> sections = reader.ReadSections();
        Assert.HasCount(image.SectionCount, sections);
        for (int index = 0; index < sections.Count; index++)
        {
            Assert.AreEqual(
                checked(image.PayloadOffset + (int)image.GetSectionPointer(index)),
                sections[index].RawDataOffset);
        }

        using MetadataReaderProvider provider = reader.CreateMetadataReaderProvider();
        Assert.AreEqual("SyntheticWebcil", provider.GetMetadataReader().GetString(
            provider.GetMetadataReader().GetAssemblyDefinition().Name));
        MethodBodyBlock? body = reader.GetMethodBody(image.MethodRva);
        Assert.IsNotNull(body);
        byte[] expectedIl = [0x1F, 0x2A, 0x2A];
        Assert.AreSequenceEqual(expectedIl, body.GetILBytes());
    }

    /// <summary>
    /// Unsupported major and minor Webcil versions fail closed through the public facade.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_UnsupportedWebcilVersion_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage unsupportedMajor = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        unsupportedMajor.SetVersionMajor(2);
        AssertMalformedImage(unsupportedMajor);

        SyntheticWebcilImage unsupportedMinor = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        unsupportedMinor.SetVersionMinor(1);
        AssertMalformedImage(unsupportedMinor);
    }

    /// <summary>
    /// The runtime-supported section-count boundaries are accepted and values outside them fail closed.
    /// </summary>
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(16, true)]
    [DataRow(17, false)]
    public void Open_SectionCountBoundary_MatchesRuntimeLimit(int sectionCount, bool valid)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: Math.Max(sectionCount, 1));
        image.SetSectionCount(checked((ushort)sectionCount));

        if (valid)
        {
            Assert.IsNotNull(WebcilImageReader.Open(image.Bytes));
            return;
        }

        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(image.Bytes));
    }

    /// <summary>
    /// Recognized bare and wrapped payloads fail closed when either revision's section table is truncated.
    /// </summary>
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(0, true)]
    [DataRow(1, false)]
    [DataRow(1, true)]
    public void Open_TruncatedSectionTable_ThrowsBadImageFormatException(int version, bool wrapped)
    {
        byte[] bytes = SyntheticWebcilBuilder.CreateWithTruncatedSectionTable(version, wrapped);

        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(bytes));
    }

    /// <summary>
    /// A zero-length raw range may begin at the payload end, but never beyond it.
    /// </summary>
    [TestMethod]
    public void Open_ZeroLengthRawRange_RequiresPointerInsideOrAtPayloadEnd()
    {
        SyntheticWebcilImage exactEnd = SyntheticWebcilBuilder.Create(sectionCount: 2);
        exactEnd.SetSectionPointer(1, checked((uint)exactEnd.PayloadLength));
        exactEnd.SetSectionRawSize(1, 0);
        Assert.IsNotNull(WebcilImageReader.Open(exactEnd.Bytes));

        SyntheticWebcilImage pastEnd = SyntheticWebcilBuilder.Create(sectionCount: 2);
        pastEnd.SetSectionPointer(1, checked((uint)pastEnd.PayloadLength + 1));
        pastEnd.SetSectionRawSize(1, 0);
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(pastEnd.Bytes));
    }

    /// <summary>
    /// Raw ranges ending after the payload or overflowing unsigned arithmetic fail closed.
    /// </summary>
    [TestMethod]
    public void Open_InvalidRawRange_ThrowsBadImageFormatException()
    {
        SyntheticWebcilImage crossingEnd = SyntheticWebcilBuilder.Create(sectionCount: 2);
        crossingEnd.SetSectionPointer(1, checked((uint)crossingEnd.PayloadLength));
        crossingEnd.SetSectionRawSize(1, 1);
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(crossingEnd.Bytes));

        SyntheticWebcilImage overflowing = SyntheticWebcilBuilder.Create(sectionCount: 2);
        overflowing.SetSectionPointer(1, uint.MaxValue);
        overflowing.SetSectionRawSize(1, 2);
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(overflowing.Bytes));
    }

    /// <summary>
    /// A section cannot overflow the 32-bit virtual address space or overlap another virtual section.
    /// </summary>
    [TestMethod]
    public void Open_InvalidVirtualRange_ThrowsBadImageFormatException()
    {
        SyntheticWebcilImage overflowing = SyntheticWebcilBuilder.Create(sectionCount: 2);
        overflowing.SetSectionVirtualAddress(1, uint.MaxValue);
        overflowing.SetSectionVirtualSize(1, 2);
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(overflowing.Bytes));

        SyntheticWebcilImage overlapping = SyntheticWebcilBuilder.Create(sectionCount: 2);
        overlapping.SetSectionVirtualAddress(1, overlapping.GetSectionVirtualAddress(0));
        overlapping.SetSectionVirtualSize(1, 1);
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(overlapping.Bytes));
    }

    /// <summary>
    /// Adjacent virtual ranges are legal and do not require an artificial gap.
    /// </summary>
    [TestMethod]
    public void Open_AdjacentVirtualRanges_AreAccepted()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 2);
        uint expectedStart = checked(image.GetSectionVirtualAddress(0) + image.GetSectionVirtualSize(0));

        Assert.AreEqual(expectedStart, image.GetSectionVirtualAddress(1));
        Assert.IsNotNull(WebcilImageReader.Open(image.Bytes));
    }

    /// <summary>
    /// Webcil raw ranges do not acquire a file-alignment rule that the runtime does not impose.
    /// </summary>
    [TestMethod]
    public void Open_UnalignedRawRange_IsAccepted()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 2);
        image.SetSectionPointer(1, checked(image.GetSectionPointer(1) + 1));
        image.SetSectionRawSize(1, 1);

        Assert.IsNotNull(WebcilImageReader.Open(image.Bytes));
    }

    /// <summary>
    /// Wrapped Webcil raw ranges are limited to the data-segment payload, not the outer Wasm file.
    /// </summary>
    [TestMethod]
    public void Open_WrappedRawRangeEscapesPayloadButNotOuterFile_ThrowsBadImageFormatException()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(
            wrapped: true,
            sectionCount: 2,
            wrapperSuffixLength: 32);
        image.SetSectionPointer(1, checked((uint)image.PayloadLength));
        image.SetSectionRawSize(1, 1);

        int payloadEnd = image.PayloadOffset + image.PayloadLength;
        Assert.IsGreaterThan(
            payloadEnd,
            image.Bytes.Length,
            "The test needs outer Wasm bytes after the Webcil data-segment payload.");
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(image.Bytes));
    }

    /// <summary>
    /// A recognizable payload inside a truncated Wasm data section cannot downgrade to raw Wasm.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void Open_WebcilInTruncatedWasmDataSection_ThrowsBadImageFormatException(int version)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithOversizedWasmDataSection(version);

        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(image.Bytes));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new AssemblyAnalyzer(image.Bytes, "truncated-data-section.wasm"));
    }

    /// <summary>
    /// Webcil accepts non-overflowing unsigned virtual ranges and projects their bit patterns to int models.
    /// </summary>
    [TestMethod]
    public void Open_HighBitVirtualAddress_IsAccepted()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 2);
        image.SetSectionVirtualAddress(1, 0x80000000);
        image.SetSectionVirtualSize(1, 4);
        image.SetClrResourcesRva(0x80000000);
        image.SetClrResourcesSize(4);
        int resourceOffset = checked(image.PayloadOffset + (int)image.GetSectionPointer(1));
        BinaryPrimitives.WriteInt32LittleEndian(image.Bytes.AsSpan(resourceOffset), 0x12345678);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        SectionInfo section = reader.ReadSections()[1];
        Assert.AreEqual(int.MinValue, section.VirtualAddress);
        Assert.AreEqual(4, section.VirtualSize);
        Assert.AreEqual(int.MinValue, reader.ClrHeader.ResourcesRva);
        Assert.AreEqual(4, reader.ClrHeader.ResourcesSize);
        Assert.IsTrue(reader.TryReadInt32AtRva(reader.ClrHeader.ResourcesRva, 0, out int value));
        Assert.AreEqual(0x12345678, value);

        using AssemblyAnalyzer analyzer = new(image.Bytes, "high-bit-resource.webcil");
        ResourceInfo resource = Assert.ContainsSingle(analyzer.Resources);
        Assert.AreEqual("SyntheticResource", resource.Name);
        Assert.AreEqual(0x12345678, resource.Size);
    }

    /// <summary>
    /// CLR runtime versions outside the range supported by the runtime fail closed.
    /// </summary>
    [TestMethod]
    [DataRow((ushort)1, false)]
    [DataRow((ushort)1, true)]
    [DataRow((ushort)3, false)]
    [DataRow((ushort)3, true)]
    public void Open_UnsupportedClrRuntimeVersion_ThrowsBadImageFormatException(
        ushort majorRuntimeVersion,
        bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        image.SetClrMajorRuntimeVersion(majorRuntimeVersion);

        AssertMalformedImage(image);
    }

    /// <summary>
    /// CLR features forbidden by the runtime's Webcil decoder fail closed through the public facade.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_UnsupportedClrHeaderFeatures_ThrowBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage nativeEntryPoint = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        nativeEntryPoint.SetClrFlags(CorFlags.ILOnly | CorFlags.NativeEntryPoint);
        AssertMalformedImage(nativeEntryPoint);

        SyntheticWebcilImage vtableFixups = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        vtableFixups.SetClrVTableFixupsSize(1);
        AssertMalformedImage(vtableFixups);

        SyntheticWebcilImage exportAddressTableJumps = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        exportAddressTableJumps.SetClrExportAddressTableJumpsSize(1);
        AssertMalformedImage(exportAddressTableJumps);

        SyntheticWebcilImage missingStrongNameSignature = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        missingStrongNameSignature.SetClrStrongNameSignatureRva(0);
        missingStrongNameSignature.SetClrFlags(CorFlags.ILOnly | CorFlags.StrongNameSigned);
        AssertMalformedImage(missingStrongNameSignature);
    }

    /// <summary>
    /// The runtime-required CLR header bytes must map, and both size declarations must meet its minimum.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_InvalidClrHeaderRange_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage unmappedRva = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        unmappedRva.SetPeCliHeaderRva(uint.MaxValue);
        AssertMalformedImage(unmappedRva);

        SyntheticWebcilImage shortDirectory = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        shortDirectory.SetPeCliHeaderSize(71);
        AssertMalformedImage(shortDirectory);

        SyntheticWebcilImage shortInternalSize = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        shortInternalSize.SetClrHeaderSize(71);
        AssertMalformedImage(shortInternalSize);
    }

    /// <summary>
    /// The runtime-required 72-byte CLR header may end at the final mapped byte of a section.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_ClrHeaderEndingAtSectionBoundary_IsAccepted(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2,
            additionalSectionSize: 72);
        byte[] clrHeader = image.Bytes.AsSpan(
            image.PayloadOffset + image.ClrHeaderOffset,
            72).ToArray();
        clrHeader.CopyTo(image.Bytes.AsSpan(image.GetSectionDataOffset(1), 72));
        image.SetPeCliHeaderRva(image.GetSectionVirtualAddress(1));
        image.SetPeCliHeaderSize(72);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        using AssemblyAnalyzer analyzer = new(
            image.Bytes,
            wrapped ? "clr-boundary.wasm" : "clr-boundary.webcil");
        Assert.AreEqual("SyntheticWebcil", analyzer.AssemblyName);
    }

    /// <summary>
    /// Future-extended CLR header declarations remain compatible when the runtime-required bytes map.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_ExtendedClrHeaderDeclarations_AreAccepted(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        image.SetPeCliHeaderSize(uint.MaxValue);
        image.SetClrHeaderSize(uint.MaxValue);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        using AssemblyAnalyzer analyzer = new(
            image.Bytes,
            wrapped ? "extended-header.wasm" : "extended-header.webcil");
        Assert.IsTrue(analyzer.HasMetadata);
    }

    /// <summary>
    /// CLR directories ending exactly at a mapped section boundary are accepted.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_OptionalClrDirectoriesEndingAtSectionBoundary_AreAccepted(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: wrapped, sectionCount: 2);
        uint rva = image.GetSectionVirtualAddress(1);
        uint size = image.GetSectionVirtualSize(1);
        image.SetClrResourcesRva(rva);
        image.SetClrResourcesSize(size);
        image.SetClrStrongNameSignatureRva(rva);
        image.SetClrStrongNameSignatureSize(size);
        image.SetClrManagedNativeHeaderRva(rva);
        image.SetClrManagedNativeHeaderSize(size);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        Assert.AreEqual(unchecked((int)rva), reader.ClrHeader.ResourcesRva);
        Assert.AreEqual(unchecked((int)rva), reader.ClrHeader.ManagedNativeHeader.RelativeVirtualAddress);
        using AssemblyAnalyzer analyzer = new(
            image.Bytes,
            wrapped ? "directories.wasm" : "directories.webcil");
        Assert.IsTrue(analyzer.HasMetadata);
    }

    /// <summary>
    /// Required metadata may end at the final mapped byte of its section.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_MetadataEndingAtSectionBoundary_IsAccepted(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2,
            additionalSectionSize: 4_096);
        uint metadataRva = image.GetClrMetadataRva();
        uint metadataSize = image.GetClrMetadataSize();
        Assert.IsLessThanOrEqualTo(image.GetSectionVirtualSize(1), metadataSize);
        int sourceOffset = checked(
            image.GetSectionDataOffset(0)
            + (int)(metadataRva - image.GetSectionVirtualAddress(0)));
        byte[] metadata = image.Bytes.AsSpan(sourceOffset, checked((int)metadataSize)).ToArray();
        metadata.CopyTo(image.Bytes.AsSpan(image.GetSectionDataOffset(1), metadata.Length));
        image.SetSectionVirtualSize(1, metadataSize);
        image.SetSectionRawSize(1, metadataSize);
        image.SetClrMetadataRva(image.GetSectionVirtualAddress(1));

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        using AssemblyAnalyzer analyzer = new(
            image.Bytes,
            wrapped ? "metadata-boundary.wasm" : "metadata-boundary.webcil");
        Assert.AreEqual("SyntheticWebcil", analyzer.AssemblyName);
    }

    /// <summary>
    /// Required metadata cannot be empty, and nonempty CLR directory extents cannot cross mapped bytes.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_InvalidClrDirectoryRange_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage metadataZeroSize = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        metadataZeroSize.SetClrMetadataSize(0);
        AssertMalformedImage(metadataZeroSize);

        SyntheticWebcilImage metadataNegativeSize = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        metadataNegativeSize.SetClrMetadataSize(uint.MaxValue);
        AssertMalformedImage(metadataNegativeSize);

        SyntheticWebcilImage metadataPastBoundary = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2);
        metadataPastBoundary.SetClrMetadataRva(metadataPastBoundary.GetSectionVirtualAddress(1));
        metadataPastBoundary.SetClrMetadataSize(
            checked(metadataPastBoundary.GetSectionVirtualSize(1) + 1));
        AssertMalformedImage(metadataPastBoundary);

        SyntheticWebcilImage resourcesPastBoundary = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2);
        resourcesPastBoundary.SetClrResourcesRva(resourcesPastBoundary.GetSectionVirtualAddress(1));
        resourcesPastBoundary.SetClrResourcesSize(
            checked(resourcesPastBoundary.GetSectionVirtualSize(1) + 1));
        AssertMalformedImage(resourcesPastBoundary);

        SyntheticWebcilImage strongNamePastBoundary = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2);
        strongNamePastBoundary.SetClrStrongNameSignatureRva(
            strongNamePastBoundary.GetSectionVirtualAddress(1));
        strongNamePastBoundary.SetClrStrongNameSignatureSize(
            checked(strongNamePastBoundary.GetSectionVirtualSize(1) + 1));
        AssertMalformedImage(strongNamePastBoundary);

        SyntheticWebcilImage managedNativePastBoundary = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2);
        managedNativePastBoundary.SetClrManagedNativeHeaderRva(
            managedNativePastBoundary.GetSectionVirtualAddress(1));
        managedNativePastBoundary.SetClrManagedNativeHeaderSize(
            checked(managedNativePastBoundary.GetSectionVirtualSize(1) + 1));
        AssertMalformedImage(managedNativePastBoundary);
    }

    /// <summary>
    /// Optional CLR directories preserve the runtime's zero-RVA and mapped-zero-size compatibility.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_OptionalClrDirectoryZeroEncodings_AreAccepted(bool wrapped)
    {
        SyntheticWebcilImage zeroRvas = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        zeroRvas.SetClrResourcesRva(0);
        zeroRvas.SetClrResourcesSize(1);
        zeroRvas.SetClrStrongNameSignatureRva(0);
        zeroRvas.SetClrStrongNameSignatureSize(1);
        zeroRvas.SetClrManagedNativeHeaderRva(0);
        zeroRvas.SetClrManagedNativeHeaderSize(1);
        Assert.IsNotNull(WebcilImageReader.Open(zeroRvas.Bytes));

        SyntheticWebcilImage zeroSizes = SyntheticWebcilBuilder.Create(
            wrapped: wrapped,
            sectionCount: 2);
        uint mappedRva = zeroSizes.GetSectionVirtualAddress(1);
        zeroSizes.SetClrResourcesRva(mappedRva);
        zeroSizes.SetClrResourcesSize(0);
        zeroSizes.SetClrStrongNameSignatureRva(mappedRva);
        zeroSizes.SetClrStrongNameSignatureSize(0);
        zeroSizes.SetClrManagedNativeHeaderRva(mappedRva);
        zeroSizes.SetClrManagedNativeHeaderSize(0);
        Assert.IsNotNull(WebcilImageReader.Open(zeroSizes.Bytes));
    }

    /// <summary>
    /// Wrapped section and debug-entry offsets retain their exact outer-file provenance.
    /// </summary>
    [TestMethod]
    public void Open_WrappedDebugDirectory_ReportsExactOuterOffsets()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: true);

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        Assert.AreEqual(image.PayloadOffset, reader.Info.PayloadOffset);
        Assert.AreEqual(
            checked(image.PayloadOffset + (int)image.GetSectionPointer(1)),
            reader.ReadSections()[1].RawDataOffset);
        DebugDirectoryInfo entry = Assert.ContainsSingle(reader.ReadDebugDirectory());
        Assert.AreEqual(DebugDirectoryEntryType.Reproducible, entry.Type);
        Assert.AreEqual(0x12345678u, entry.Stamp);
        Assert.AreEqual(1, entry.MajorVersion);
        Assert.AreEqual(2, entry.MinorVersion);
        Assert.AreEqual(4, entry.DataSize);
        Assert.AreEqual(
            checked((int)image.GetSectionVirtualAddress(1) + 28),
            entry.AddressOfRawData);
        Assert.AreEqual(
            checked(image.PayloadOffset + (int)image.GetSectionPointer(1) + 28),
            entry.PointerToRawData);

        using AssemblyAnalyzer analyzer = new(image.Bytes, "debug-provenance.wasm");
        Assert.AreEqual(
            checked(image.PayloadOffset + (int)image.GetSectionPointer(1)),
            analyzer.Sections[1].RawDataOffset);
        DebugDirectoryInfo publicEntry = Assert.ContainsSingle(analyzer.DebugDirectory);
        Assert.AreEqual(entry.Type, publicEntry.Type);
        Assert.AreEqual(entry.Stamp, publicEntry.Stamp);
        Assert.AreEqual(entry.MajorVersion, publicEntry.MajorVersion);
        Assert.AreEqual(entry.MinorVersion, publicEntry.MinorVersion);
        Assert.AreEqual(entry.DataSize, publicEntry.DataSize);
        Assert.AreEqual(entry.AddressOfRawData, publicEntry.AddressOfRawData);
        Assert.AreEqual(entry.PointerToRawData, publicEntry.PointerToRawData);
    }

    /// <summary>
    /// A compiler-produced embedded portable PDB remains readable from bare and wrapped Webcil.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadEmbeddedPortablePdb_CompilerProducedPayload_Decodes(bool wrapped)
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        int expectedSize = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int)));
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(
            payload,
            wrapped);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        WebcilDebugEntry? embeddedEntry = reader.EmbeddedPortablePdbEntry();
        Assert.IsTrue(embeddedEntry.HasValue);
        WebcilDebugEntry entry = embeddedEntry.Value;

        using MetadataReaderProvider provider = reader.ReadEmbeddedPortablePdb(entry);

        MetadataReader metadata = provider.GetMetadataReader();
        Assert.IsGreaterThan(0, metadata.Documents.Count);
        DebugDirectoryInfo publicEntry = Assert.ContainsSingle(
            static candidate => candidate.Type == DebugDirectoryEntryType.EmbeddedPortablePdb,
            reader.ReadDebugDirectory());
        Assert.AreEqual(
            $"present; uncompressed size: {expectedSize} bytes",
            publicEntry.Payload);
    }

    /// <summary>
    /// Invalid or oversized embedded-PDB declarations fail closed without hiding managed metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow((256 * 1024 * 1024) + 1)]
    [DataRow(int.MaxValue)]
    public void ReadEmbeddedPortablePdb_InvalidDeclaredSize_IsRejected(int declaredSize)
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), declaredSize);
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(payload);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        WebcilDebugEntry? embeddedEntry = reader.EmbeddedPortablePdbEntry();
        Assert.IsTrue(embeddedEntry.HasValue);
        WebcilDebugEntry entry = embeddedEntry.Value;

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            reader.ReadEmbeddedPortablePdb(entry));

        using AssemblyAnalyzer analyzer = new(image.Bytes, "oversized-pdb.webcil");
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.IsFalse(analyzer.HasPortablePdb);
        Assert.AreEqual(PdbProvenanceKind.InvalidEmbeddedPdb, analyzer.PdbProvenance.Kind);
    }

    /// <summary>
    /// Embedded-PDB deflate output must match the declared size exactly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(-1)]
    [DataRow(1)]
    public void ReadEmbeddedPortablePdb_OutputLengthMismatch_IsRejected(int sizeAdjustment)
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        int declaredSize = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(
            payload.AsSpan(sizeof(int)),
            checked(declaredSize + sizeAdjustment));
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(payload);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        WebcilDebugEntry? embeddedEntry = reader.EmbeddedPortablePdbEntry();
        Assert.IsTrue(embeddedEntry.HasValue);
        WebcilDebugEntry entry = embeddedEntry.Value;

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            reader.ReadEmbeddedPortablePdb(entry));
    }

    /// <summary>
    /// The maximum supported embedded-PDB declaration is accepted by the header guard.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadEmbeddedPortablePdb_MaximumDeclaredSize_PassesHeaderGuard()
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        BinaryPrimitives.WriteInt32LittleEndian(
            payload.AsSpan(sizeof(int)),
            256 * 1024 * 1024);
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(payload);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        DebugDirectoryInfo entry = Assert.ContainsSingle(
            static candidate => candidate.Type == DebugDirectoryEntryType.EmbeddedPortablePdb,
            reader.ReadDebugDirectory());

        Assert.AreEqual(
            "present; uncompressed size: 268435456 bytes",
            entry.Payload);
    }

    /// <summary>
    /// Unsupported embedded-PDB directory versions fail closed and remain diagnostic.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(8, 0x00FF)]
    [DataRow(10, 0x0101)]
    public void ReadEmbeddedPortablePdb_UnsupportedVersion_IsRejected(
        int fieldOffset,
        int version)
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(payload);
        BinaryPrimitives.WriteUInt16LittleEndian(
            image.Bytes.AsSpan(image.GetSectionDataOffset(1) + fieldOffset),
            checked((ushort)version));
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        WebcilDebugEntry? embeddedEntry = reader.EmbeddedPortablePdbEntry();
        Assert.IsTrue(embeddedEntry.HasValue);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            reader.ReadEmbeddedPortablePdb(embeddedEntry.Value));
        DebugDirectoryInfo publicEntry = Assert.ContainsSingle(reader.ReadDebugDirectory());
        Assert.Contains("unreadable:", publicEntry.Payload);
        Assert.Contains("version", publicEntry.Payload);
    }

    /// <summary>
    /// Invalid signatures and deflate streams are rejected without invalidating Webcil metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadEmbeddedPortablePdb_MalformedPayload_IsRejected(bool corruptSignature)
    {
        byte[] payload = EmbeddedPortablePdbTestImage.ExtractPayload(Samples.EmbeddedSourceLibDll);
        if (corruptSignature)
        {
            payload[0] ^= 0xFF;
        }
        else
        {
            payload.AsSpan(2 * sizeof(int)).Fill(0xFF);
        }

        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithEmbeddedPortablePdb(payload);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        WebcilDebugEntry? embeddedEntry = reader.EmbeddedPortablePdbEntry();
        Assert.IsTrue(embeddedEntry.HasValue);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            reader.ReadEmbeddedPortablePdb(embeddedEntry.Value));
        using AssemblyAnalyzer analyzer = new(image.Bytes, "malformed-pdb.webcil");
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual(PdbProvenanceKind.InvalidEmbeddedPdb, analyzer.PdbProvenance.Kind);
    }

    /// <summary>
    /// Reserved debug characteristics are malformed rather than an entry that can be silently ignored.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_NonzeroDebugCharacteristics_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(image, fieldOffset: 0, value: 1);

        AssertMalformedImage(image);
    }

    /// <summary>
    /// A zero-length debug payload may begin exactly at the Webcil payload end, but not beyond it.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_ZeroLengthDebugPayload_RequiresPointerInsideOrAtPayloadEnd(bool wrapped)
    {
        SyntheticWebcilImage exactEnd = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(exactEnd, fieldOffset: 16, value: 0);
        SetDebugEntryUInt32(
            exactEnd,
            fieldOffset: 24,
            value: checked((uint)exactEnd.PayloadLength));
        Assert.IsNotNull(WebcilImageReader.Open(exactEnd.Bytes));

        SyntheticWebcilImage pastEnd = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(pastEnd, fieldOffset: 16, value: 0);
        SetDebugEntryUInt32(
            pastEnd,
            fieldOffset: 24,
            value: checked((uint)pastEnd.PayloadLength + 1));
        AssertMalformedImage(pastEnd);
    }

    /// <summary>
    /// A complete debug-directory row may end at the final mapped byte of its section.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_DebugDirectoryEndingAtSectionBoundary_IsAccepted(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        image.SetSectionVirtualSize(1, 28);
        image.SetSectionRawSize(1, 28);
        SetDebugEntryUInt32(image, fieldOffset: 16, value: 0);
        SetDebugEntryUInt32(
            image,
            fieldOffset: 24,
            value: checked((uint)image.PayloadLength));

        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);

        Assert.IsNotNull(reader);
        DebugDirectoryInfo entry = Assert.ContainsSingle(reader.ReadDebugDirectory());
        Assert.AreEqual(DebugDirectoryEntryType.Reproducible, entry.Type);
        Assert.AreEqual(0, entry.DataSize);
        Assert.AreEqual(checked(image.PayloadOffset + image.PayloadLength), entry.PointerToRawData);
    }

    /// <summary>
    /// Either zero debug-directory field represents an absent directory, matching the runtime.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_AbsentDebugDirectoryEncoding_ReportsNoEntries(bool wrapped)
    {
        SyntheticWebcilImage zeroRva = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        zeroRva.SetPeDebugRva(0);
        zeroRva.SetPeDebugSize(uint.MaxValue);
        WebcilImageReader? zeroRvaReader = WebcilImageReader.Open(zeroRva.Bytes);
        Assert.IsNotNull(zeroRvaReader);
        Assert.AreEqual(0, zeroRvaReader.Info.DebugDirectorySize);
        Assert.IsEmpty(zeroRvaReader.ReadDebugDirectory());
        using AssemblyAnalyzer zeroRvaAnalyzer = new(
            zeroRva.Bytes,
            wrapped ? "zero-debug-rva.wasm" : "zero-debug-rva.webcil");
        Assert.IsNotNull(zeroRvaAnalyzer.WebcilInfo);
        Assert.AreEqual(0, zeroRvaAnalyzer.WebcilInfo.DebugDirectorySize);
        Assert.IsEmpty(zeroRvaAnalyzer.DebugDirectory);

        SyntheticWebcilImage zeroSize = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        zeroSize.SetPeDebugSize(0);
        WebcilImageReader? zeroSizeReader = WebcilImageReader.Open(zeroSize.Bytes);
        Assert.IsNotNull(zeroSizeReader);
        Assert.AreEqual(0, zeroSizeReader.Info.DebugDirectorySize);
        Assert.IsEmpty(zeroSizeReader.ReadDebugDirectory());
        using AssemblyAnalyzer zeroSizeAnalyzer = new(
            zeroSize.Bytes,
            wrapped ? "zero-debug-size.wasm" : "zero-debug-size.webcil");
        Assert.IsNotNull(zeroSizeAnalyzer.WebcilInfo);
        Assert.AreEqual(0, zeroSizeAnalyzer.WebcilInfo.DebugDirectorySize);
        Assert.IsEmpty(zeroSizeAnalyzer.DebugDirectory);
    }

    /// <summary>
    /// Debug directory rows and their payloads cannot cross the Webcil payload or mapped section bytes.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Open_InvalidDebugDirectoryRange_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage partialEntry = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        partialEntry.SetPeDebugSize(27);
        AssertMalformedImage(partialEntry);

        SyntheticWebcilImage unmappedDirectory = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        unmappedDirectory.SetPeDebugRva(checked(
            unmappedDirectory.GetSectionVirtualAddress(1)
            + unmappedDirectory.GetSectionVirtualSize(1)));
        AssertMalformedImage(unmappedDirectory);

        SyntheticWebcilImage oversizedDirectory = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        oversizedDirectory.SetPeDebugSize(84);
        AssertMalformedImage(oversizedDirectory);

        SyntheticWebcilImage payloadPastEnd = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(payloadPastEnd, fieldOffset: 16, value: 1);
        SetDebugEntryUInt32(
            payloadPastEnd,
            fieldOffset: 24,
            checked((uint)payloadPastEnd.PayloadLength));
        AssertMalformedImage(payloadPastEnd);

        SyntheticWebcilImage payloadOverflow = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(payloadOverflow, fieldOffset: 16, value: 2);
        SetDebugEntryUInt32(payloadOverflow, fieldOffset: 24, uint.MaxValue);
        AssertMalformedImage(payloadOverflow);

        SyntheticWebcilImage negativePayloadSize = SyntheticWebcilBuilder.CreateWithDebugDirectory(wrapped: wrapped);
        SetDebugEntryUInt32(negativePayloadSize, fieldOffset: 16, uint.MaxValue);
        AssertMalformedImage(negativePayloadSize);
    }

    /// <summary>
    /// Zero, negative, unmapped, virtual-tail, and raw-padding RVAs do not produce method bodies.
    /// </summary>
    [TestMethod]
    public void GetMethodBody_UnmappedRva_ReturnsNull()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 3);
        image.SetSectionVirtualSize(1, 8);
        image.SetSectionRawSize(1, 4);
        image.SetSectionVirtualAddress(2, checked(image.GetSectionVirtualAddress(1) + 8));
        image.SetSectionVirtualSize(2, 4);
        image.SetSectionPointer(2, image.GetSectionPointer(1));
        image.SetSectionRawSize(2, 8);
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        Assert.IsNull(reader.GetMethodBody(0));
        Assert.IsNull(reader.GetMethodBody(-1));
        Assert.IsNull(reader.GetMethodBody(int.MaxValue));
        Assert.IsNull(reader.GetMethodBody(checked((int)image.GetSectionVirtualAddress(1) + 4)));
        Assert.IsNull(reader.GetMethodBody(checked((int)image.GetSectionVirtualAddress(2) + 4)));
    }

    /// <summary>
    /// Zero and negative signed method RVAs remain invalid even when their unsigned bit patterns map.
    /// </summary>
    [TestMethod]
    public void GetMethodBody_MappedZeroAndNegativeRvas_ReturnNull()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 3);
        image.SetSectionVirtualAddress(1, 0);
        image.SetSectionVirtualSize(1, 2);
        image.SetSectionRawSize(1, 2);
        image.SetSectionVirtualAddress(2, 0x80000000);
        image.SetSectionVirtualSize(2, 2);
        image.SetSectionRawSize(2, 2);
        image.Bytes[image.GetSectionDataOffset(1)] = 0x06;
        image.Bytes[image.GetSectionDataOffset(1) + 1] = 0x2A;
        image.Bytes[image.GetSectionDataOffset(2)] = 0x06;
        image.Bytes[image.GetSectionDataOffset(2) + 1] = 0x2A;
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        Assert.IsNull(reader.GetMethodBody(0));
        Assert.IsNull(reader.GetMethodBody(int.MinValue));
    }

    /// <summary>
    /// A complete tiny body ending at the final mapped byte remains byte-exact across compacting collections.
    /// </summary>
    [TestMethod]
    public void AssemblyAnalyzer_TinyBodyEndingAtFinalMappedByte_SurvivesCompactingCollections()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 2);
        image.SetSectionVirtualSize(1, 2);
        image.SetSectionRawSize(1, 2);
        int bodyOffset = checked(image.PayloadOffset + (int)image.GetSectionPointer(1));
        image.Bytes[bodyOffset] = 0x06;
        image.Bytes[bodyOffset + 1] = 0x2A;
        image.SetMethodRva(checked((int)image.GetSectionVirtualAddress(1)));
        using AssemblyAnalyzer analyzer = new(image.Bytes, "boundary.webcil");
        MethodDefInfo method = Assert.ContainsSingle(
            static method => method.Name == "Answer",
            analyzer.MethodDefs);

        MethodBodyBlock? body = analyzer.GetMethodBody(method);
        Assert.IsNotNull(body);
        byte[] expected = [0x2A];
        Assert.AreSequenceEqual(expected, body.GetILBytes());

        CompactManagedHeap();

        Assert.AreSequenceEqual(expected, body.GetILBytes());
        GC.KeepAlive(analyzer);
    }

    /// <summary>
    /// A method body cannot consume bytes from a following virtual section even when raw ranges overlap.
    /// </summary>
    [TestMethod]
    public void GetMethodBody_TruncatedAtVirtualSectionBoundary_ThrowsBadImageFormatException()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 3);
        image.SetSectionVirtualSize(1, 1);
        image.SetSectionRawSize(1, 5);
        image.SetSectionVirtualAddress(2, checked(image.GetSectionVirtualAddress(1) + 1));
        image.SetSectionPointer(2, checked(image.GetSectionPointer(1) + 1));
        image.Bytes[checked(image.PayloadOffset + (int)image.GetSectionPointer(1))] = 0x06;
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            reader.GetMethodBody(checked((int)image.GetSectionVirtualAddress(1))));
    }

    /// <summary>
    /// An RVA at one section's exclusive end resolves to an adjacent section beginning at that RVA.
    /// </summary>
    [TestMethod]
    public void GetMethodBody_AdjacentSectionStart_ReadsSecondSection()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 3);
        image.SetSectionVirtualSize(1, 1);
        image.SetSectionRawSize(1, 5);
        image.SetSectionVirtualAddress(2, checked(image.GetSectionVirtualAddress(1) + 1));
        image.SetSectionVirtualSize(2, 2);
        image.SetSectionPointer(2, checked(image.GetSectionPointer(1) + 1));
        image.SetSectionRawSize(2, 2);
        image.Bytes[image.GetSectionDataOffset(2)] = 0x06;
        image.Bytes[image.GetSectionDataOffset(2) + 1] = 0x2A;
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        MethodBodyBlock? body = reader.GetMethodBody(
            checked((int)image.GetSectionVirtualAddress(2)));

        Assert.IsNotNull(body);
        byte[] expected = [0x2A];
        Assert.AreSequenceEqual(expected, body.GetILBytes());
    }

    /// <summary>
    /// Large relative offsets are rejected without signed overflow.
    /// </summary>
    [TestMethod]
    public void TryReadInt32AtRva_IntMaxRelativeOffset_ReturnsFalse()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create();
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);

        Assert.IsFalse(reader.TryReadInt32AtRva(image.MethodRva, int.MaxValue, out _));
    }

    /// <summary>
    /// Method-body storage remains stable across compacting collections for the reader lifetime.
    /// </summary>
    [TestMethod]
    public void GetMethodBody_CompactingCollections_PreservesIlBytes()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create();
        WebcilImageReader? reader = WebcilImageReader.Open(image.Bytes);
        Assert.IsNotNull(reader);
        MethodBodyBlock? body = reader.GetMethodBody(image.MethodRva);
        Assert.IsNotNull(body);
        byte[]? ilBytes = body.GetILBytes();
        Assert.IsNotNull(ilBytes);
        byte[] expected = [.. ilBytes];

        CompactManagedHeap();

        Assert.AreSequenceEqual(expected, body.GetILBytes());
        GC.KeepAlive(reader);
    }

    /// <summary>
    /// The public analyzer and disassembler expose valid synthetic Webcil metadata and IL.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AssemblyAnalyzer_ValidWebcil_DisassemblesMethod(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        using AssemblyAnalyzer analyzer = new(
            image.Bytes,
            wrapped ? "synthetic.wasm" : "synthetic.webcil");

        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual(BinaryKind.Managed, analyzer.BinaryKind);
        Assert.IsNotNull(analyzer.WebcilInfo);
        MethodDefInfo method = Assert.ContainsSingle(
            static method => method.Name == "Answer",
            analyzer.MethodDefs);
        (string Text, IReadOnlyList<IlInstruction> Instructions, int HeaderLineCount)? disassembly =
            new IlDisassembler(analyzer).DisassembleWithText(method);
        Assert.IsNotNull(disassembly);
        Assert.Contains("ldc.i4.s 42", disassembly.Value.Text);
        Assert.Contains("ret", disassembly.Value.Text);
    }

    /// <summary>
    /// Public IL disassembly reports a mapped body truncated at its section boundary as malformed.
    /// </summary>
    [TestMethod]
    public void IlDisassembler_MethodBodyCrossesSectionBoundary_ThrowsBadImageFormatException()
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(sectionCount: 3);
        image.SetSectionVirtualSize(1, 1);
        image.SetSectionRawSize(1, 5);
        image.SetSectionVirtualAddress(2, checked(image.GetSectionVirtualAddress(1) + 1));
        image.SetSectionPointer(2, checked(image.GetSectionPointer(1) + 1));
        image.Bytes[checked(image.PayloadOffset + (int)image.GetSectionPointer(1))] = 0x06;
        image.SetMethodRva(checked((int)image.GetSectionVirtualAddress(1)));
        using AssemblyAnalyzer analyzer = new(image.Bytes, "truncated.webcil");
        MethodDefInfo method = Assert.ContainsSingle(
            static method => method.Name == "Answer",
            analyzer.MethodDefs);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new IlDisassembler(analyzer).Disassemble(method));
    }

    /// <summary>
    /// Recognized corrupt bare and wrapped Webcil images fail closed at the public analyzer boundary.
    /// </summary>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AssemblyAnalyzer_RecognizedCorruptWebcil_ThrowsBadImageFormatException(bool wrapped)
    {
        SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: wrapped);
        image.SetSectionCount(0);

        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new AssemblyAnalyzer(image.Bytes, wrapped ? "corrupt.wasm" : "corrupt.webcil"));
    }

    /// <summary>
    /// A failed file-path construction releases its stream so the corrupt file is immediately deletable.
    /// </summary>
    [TestMethod]
    public void AssemblyAnalyzer_CorruptWrappedFile_ReleasesFileHandle()
    {
        TestSkip.Unless(
            OperatingSystem.IsWindows(),
            "Exclusive file-handle release is observable through deletion only on Windows.");
        string path = Path.Combine(Path.GetTempPath(), $"dotsider-webcil-{Guid.NewGuid():N}.wasm");
        try
        {
            SyntheticWebcilImage image = SyntheticWebcilBuilder.Create(wrapped: true);
            image.SetSectionCount(0);
            File.WriteAllBytes(path, image.Bytes);

            Assert.ThrowsExactly<BadImageFormatException>(() => new AssemblyAnalyzer(path));
            File.Delete(path);

            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Genuine non-Webcil Wasm remains on the raw WebAssembly analysis path.
    /// </summary>
    [TestMethod]
    public void AssemblyAnalyzer_NonWebcilWasm_RemainsRawWasm()
    {
        byte[] wasm = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];

        using AssemblyAnalyzer analyzer = new(wasm, "empty.wasm");

        Assert.IsFalse(analyzer.HasMetadata);
        Assert.AreEqual(BinaryKind.Wasm, analyzer.BinaryKind);
        Assert.IsNull(analyzer.WebcilInfo);
        Assert.IsNotNull(analyzer.WasmModuleInfo);
    }

    /// <summary>
    /// A sub-four-byte data segment cannot borrow following bytes to resemble a Webcil signature.
    /// </summary>
    [TestMethod]
    public void Open_ShortDataSegmentFollowedByWebcilMagicBytes_ReturnsNull()
    {
        byte[] wasm =
        [
            0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00,
            0x0B, 0x07,
            0x01,
            0x01, 0x01, (byte)'W', (byte)'b', (byte)'I', (byte)'L',
        ];

        Assert.IsNull(WebcilImageReader.Open(wasm));
    }

    private static void CompactManagedHeap()
    {
        for (int iteration = 0; iteration < 5; iteration++)
        {
            _ = Enumerable.Range(0, 2_048).Select(static _ => new byte[256]).ToArray();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
    }

    private static void AssertMalformedImage(SyntheticWebcilImage image)
    {
        Assert.ThrowsExactly<BadImageFormatException>(() => WebcilImageReader.Open(image.Bytes));
        Assert.ThrowsExactly<BadImageFormatException>(() =>
            new AssemblyAnalyzer(image.Bytes, image.PayloadOffset == 0 ? "invalid.webcil" : "invalid.wasm"));
    }

    private static void SetDebugEntryUInt32(
        SyntheticWebcilImage image,
        int fieldOffset,
        uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.Bytes.AsSpan(image.GetSectionDataOffset(1) + fieldOffset, sizeof(uint)),
            value);
}
