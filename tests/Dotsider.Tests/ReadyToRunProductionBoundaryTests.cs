using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Exercises the ReadyToRun signature-depth boundary through both production callers over a real
/// crossgen2 image whose section routing is patched in memory.
/// </summary>
[TestClass]
public sealed class ReadyToRunProductionBoundaryTests
{
    private const string CompositeSkipReason = "ReadyToRun composite publish did not run on this leg.";
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies a one-element <c>MethodDefEntryPoints</c> NativeArray remains valid when its runtime
    /// function encoding is the final byte of the declared section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodDefEntryPoints_ExactSectionBoundary_ResolvesMethod()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.MethodDefEntryPoints,
            [0x08, 0x01, 0x00, 0x00],
            declaredSize: 4);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        var method = Assert.ContainsSingle(
            entry => !entry.IsGenericInstantiation && entry.Token == 0x0600_0001,
            analyzer.ReadyToRunMethods);
        Assert.AreEqual(0, method.EntryPointRuntimeFunctionId);
    }

    /// <summary>
    /// Verifies a structurally contained sparse-array count above the module's MethodDef rows fails
    /// closed before it can amplify a small index into millions of lookups.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodDefEntryPoints_CountAboveMethodDefRows_ProducesEmptyMethodModel()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        uint forgedCount;
        using (var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!))
        {
            var metadata = baseline.GetMetadataReader();
            Assert.IsNotNull(metadata);
            forgedCount = checked((uint)metadata.MethodDefinitions.Count + 1);
            Assert.Contains(entry => entry.IsGenericInstantiation, baseline.ReadyToRunMethods);
        }

        var table = BuildAbsentNativeArray(forgedCount);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.MethodDefEntryPoints,
            table,
            table.Length);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies a NativeArray root cannot consume a plausible leaf and runtime-function index just
    /// beyond the declared <c>MethodDefEntryPoints</c> section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodDefEntryPoints_TreeRootBeyondSection_ProducesEmptyMethodModel()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.MethodDefEntryPoints,
            [0x08, 0x02, 0xCC, 0x00, 0x00],
            declaredSize: 2);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies a contained leaf cannot decode its runtime-function integer from bytes immediately
    /// beyond the declared <c>MethodDefEntryPoints</c> section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodDefEntryPoints_ElementEncodingBeyondSection_ProducesEmptyMethodModel()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.MethodDefEntryPoints,
            [0x08, 0x01, 0x00, 0x01, 0x00],
            declaredSize: 4);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies overflowing NativeFormat section coordinates degrade through <c>SafeBuild</c> as a
    /// malformed image rather than escaping as an arithmetic exception.
    /// </summary>
    /// <param name="sectionType">The image-level NativeFormat section whose size is forged.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(ReadyToRunSectionType.MethodDefEntryPoints)]
    [DataRow(ReadyToRunSectionType.InstanceMethodEntryPoints)]
    public void NativeFormatSection_OverflowingDeclaredRange_ProducesEmptyMethodModel(
        ReadyToRunSectionType sectionType)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            sectionType,
            [0x00],
            int.MaxValue);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies a composite component propagates the exact section size and accepts an element
    /// ending at that component core header's <c>MethodDefEntryPoints</c> boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CompositeMethodDefEntryPoints_ExactSectionBoundary_ResolvesComponentMethod()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, CompositeSkipReason);
        var patched = ReadyToRunImagePatcher.PatchComponentMethodDefEntryPoints(
            Samples.ReadyToRunCompositeImage!,
            Samples.ReadyToRunComponentLibMvid,
            [0x08, 0x01, 0x00, 0x00],
            declaredSize: 4);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunCompositeImage!);

        _ = Assert.ContainsSingle(
            entry => !entry.IsGenericInstantiation
                && entry.Mvid == Samples.ReadyToRunComponentLibMvid
                && entry.Token == 0x0600_0001,
            analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies a component NativeArray root cannot consume plausible bytes beyond that component's
    /// declared <c>MethodDefEntryPoints</c> section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CompositeMethodDefEntryPoints_TreeRootBeyondSection_ProducesEmptyMethodModel()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, CompositeSkipReason);
        var patched = ReadyToRunImagePatcher.PatchComponentMethodDefEntryPoints(
            Samples.ReadyToRunCompositeImage!,
            Samples.ReadyToRunComponentLibMvid,
            [0x08, 0x02, 0xCC, 0x00, 0x00],
            declaredSize: 2);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunCompositeImage!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies an empty <c>InstanceMethodEntryPoints</c> table accepts a final bucket boundary that
    /// is exactly equal to the declared section end.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InstanceMethodEntryPoints_ExactEmptyBoundary_RetainsOrdinaryMethods()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.InstanceMethodEntryPoints,
            [0x00, 0x02, 0x02],
            declaredSize: 3);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.DoesNotContain(entry => entry.IsGenericInstantiation, analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies bucket indexes, ranges, and signed payload deltas cannot escape the declared
    /// <c>InstanceMethodEntryPoints</c> section.
    /// </summary>
    /// <param name="malformation">The NativeHashtable cursor to forge.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("TruncatedIndex")]
    [DataRow("TruncatedEntry")]
    [DataRow("TruncatedPayload")]
    [DataRow("EscapedRange")]
    [DataRow("EscapedPayload")]
    public void InstanceMethodEntryPoints_EscapedContainerCursor_ProducesEmptyMethodModel(
        string malformation)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        (byte[] Table, int DeclaredSize) container = malformation switch
        {
            "TruncatedIndex" => ([0x00, 0x02, 0x02], 2),
            "TruncatedEntry" => ([0x00, 0x02, 0x03, 0x00, 0x02, 0x00, 0x01, 0x00], 4),
            "TruncatedPayload" => ([0x00, 0x02, 0x04, 0x00, 0x02, 0x00, 0x01, 0x00], 6),
            "EscapedRange" => ([0x00, 0x02, 0x07, 0x00, 0x02, 0x00, 0x01, 0x00], 3),
            "EscapedPayload" => ([0x00, 0x02, 0x04, 0x00, 0x02, 0x00, 0x01, 0x00], 5),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };
        var (table, declaredSize) = container;
        var patched = ReadyToRunImagePatcher.PatchNativeFormatSection(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.InstanceMethodEntryPoints,
            table,
            declaredSize);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
    }

    /// <summary>
    /// Verifies <c>ReadyToRunImageReader.SafeBuild</c> degrades to an exact empty method model when
    /// its real <c>InstanceMethodEntryPoints</c> table reaches a depth-129 signature.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InstanceMethodEntryPoints_Depth129_ProducesEmptyMethodModel()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using (var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!))
        {
            Assert.IsNotEmpty(baseline.ReadyToRunMethods);
        }

        var patched = ReadyToRunImagePatcher.PatchInstanceMethodEntryPoints(
            Samples.ReadyToRunConsoleDll!);
        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);

        var readyToRunInfo = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(readyToRunInfo);
        Assert.AreEqual(ReadyToRunStatus.Valid, readyToRunInfo.Status);
        var instanceSection = readyToRunInfo.Sections.Single(
            static section => section.Type == (int)ReadyToRunSectionType.InstanceMethodEntryPoints);
        Assert.AreEqual(patched.TableOffset, instanceSection.FileOffset);
        var entryOffsets = new R2RNativeHashtable(
            new R2RNativeReader(patched.Image),
            patched.TableOffset,
            patched.TableOffset + instanceSection.Size).AllEntryOffsets().ToArray();
        Assert.AreSequenceEqual([patched.SignatureOffset], entryOffsets);

        Assert.IsEmpty(analyzer.ReadyToRunMethods);
        AssertDepthGuardReached(patched.Image, patched.SignatureOffset, analyzer.GetMetadataReader());
    }

    /// <summary>
    /// Verifies <c>ReadyToRunImportMap.Build</c> drops exactly the import slot whose real signature
    /// RVA is repointed at a depth-129 method fixup while retaining all other named imports.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ImportFixup_Depth129_DropsOnlyAffectedSlot()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImportMethodEntry(Samples.ReadyToRunConsoleDll!);

        using (var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!))
        {
            var baselineMap = ReadyToRunImportMap.Build(baseline);
            Assert.IsNotNull(baselineMap);
            Assert.IsTrue(baselineMap.TryResolve(patched.SlotVirtualAddress, out var original));
            Assert.AreEqual(patched.OriginalName, original.Name);
            Assert.AreEqual(patched.OriginalCount, baselineMap.Count);
        }

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var readyToRunInfo = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(readyToRunInfo);
        Assert.AreEqual(ReadyToRunStatus.Valid, readyToRunInfo.Status);
        var importMap = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotNull(importMap);
        Assert.AreEqual(patched.OriginalCount - 1, importMap.Count);
        Assert.IsFalse(importMap.TryResolve(patched.SlotVirtualAddress, out _));
        AssertDepthGuardReached(patched.Image, patched.SignatureOffset, analyzer.GetMetadataReader());
    }

    /// <summary>
    /// Verifies direct MethodDef and MemberRef fixups reject nil, out-of-range, and wider-than-token
    /// rows through the import-map boundary instead of rendering plausible labels.
    /// </summary>
    /// <param name="memberReference">Whether the fixup selects a MemberRef rather than a MethodDef.</param>
    /// <param name="invalidRow">The invalid-row category to encode.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false, "Nil")]
    [DataRow(false, "OutOfRange")]
    [DataRow(false, "Over24Bit")]
    [DataRow(true, "Nil")]
    [DataRow(true, "OutOfRange")]
    [DataRow(true, "Over24Bit")]
    public void DirectMethodTokenFixup_InvalidRow_DropsOnlyAffectedSlot(
        bool memberReference,
        string invalidRow)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var metadata = baseline.GetMetadataReader();
        Assert.IsNotNull(metadata);
        var rowCount = memberReference
            ? metadata.MemberReferences.Count
            : metadata.MethodDefinitions.Count;
        var row = invalidRow switch
        {
            "Nil" => 0U,
            "OutOfRange" => checked((uint)rowCount + 1),
            "Over24Bit" => 0x0100_0000U,
            _ => throw new ArgumentOutOfRangeException(nameof(invalidRow)),
        };
        var fixupKind = memberReference ? (byte)0x15 : (byte)0x14;
        var patched = ReadyToRunImagePatcher.PatchImportFixup(
            Samples.ReadyToRunConsoleDll!,
            [fixupKind, .. EncodeCompressedUInt(row)]);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var importMap = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotNull(importMap);
        Assert.AreEqual(patched.OriginalCount - 1, importMap.Count);
        Assert.IsFalse(importMap.TryResolve(patched.SlotVirtualAddress, out _));
    }

    /// <summary>Verifies direct MethodDef and MemberRef fixups accept existing row one.</summary>
    /// <param name="memberReference">Whether the fixup selects a MemberRef rather than a MethodDef.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public void DirectMethodTokenFixup_ValidRow_RetainsAffectedSlot(bool memberReference)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var fixupKind = memberReference ? (byte)0x15 : (byte)0x14;
        var patched = ReadyToRunImagePatcher.PatchImportFixup(
            Samples.ReadyToRunConsoleDll!,
            [fixupKind, 0x01]);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var importMap = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotNull(importMap);
        Assert.AreEqual(patched.OriginalCount, importMap.Count);
        Assert.IsTrue(importMap.TryResolve(patched.SlotVirtualAddress, out var resolved));
        Assert.IsNotEmpty(resolved.Name);
    }

    private static void AssertDepthGuardReached(
        byte[] image,
        int signatureOffset,
        System.Reflection.Metadata.MetadataReader? metadata)
    {
        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunSignatureWalker.ParseMethod(
                new R2RNativeReader(image),
                signatureOffset,
                metadata));

        Assert.Contains("type nesting depth 129", exception.Message);
        Assert.Contains("maximum 128", exception.Message);
    }

    private static byte[] EncodeCompressedUInt(uint value)
    {
        if (value <= 0x7F)
        {
            return [(byte)value];
        }

        if (value <= 0x3FFF)
        {
            return [(byte)(0x80 | value >> 8), (byte)value];
        }

        if (value <= 0x1FFF_FFFF)
        {
            return
            [
                (byte)(0xC0 | value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value,
            ];
        }

        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static byte[] BuildAbsentNativeArray(uint count)
    {
        var blockCount = count / 16 + (count % 16 == 0 ? 0U : 1U);
        byte entryIndexSize;
        int entryIndexStride;
        if (blockCount <= byte.MaxValue)
        {
            entryIndexSize = 0;
            entryIndexStride = 1;
        }
        else if (blockCount * sizeof(ushort) <= ushort.MaxValue)
        {
            entryIndexSize = 1;
            entryIndexStride = sizeof(ushort);
        }
        else
        {
            entryIndexSize = 2;
            entryIndexStride = sizeof(uint);
        }

        var rootOffset = checked(blockCount * (uint)entryIndexStride);
        var header = EncodeNativeUnsigned(checked(count << 2) | entryIndexSize);
        var bytes = new byte[checked(header.Length + (int)rootOffset + 1)];
        header.CopyTo(bytes, 0);
        for (var block = 0U; block < blockCount; block++)
        {
            var indexOffset = checked(header.Length + (int)block * entryIndexStride);
            switch (entryIndexSize)
            {
                case 0:
                    bytes[indexOffset] = checked((byte)rootOffset);
                    break;
                case 1:
                    BinaryPrimitives.WriteUInt16LittleEndian(
                        bytes.AsSpan(indexOffset),
                        checked((ushort)rootOffset));
                    break;
                default:
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(indexOffset), rootOffset);
                    break;
            }
        }

        bytes[^1] = 0x80; // special leaf 16: no index in a block is present
        return bytes;
    }

    private static byte[] EncodeNativeUnsigned(uint value)
    {
        if (value <= 0x7F)
        {
            return [checked((byte)(value << 1))];
        }

        if (value <= 0x3FFF)
        {
            return [(byte)((value << 2) | 1), checked((byte)(value >> 6))];
        }

        if (value <= 0x1F_FFFF)
        {
            return
            [
                (byte)((value << 3) | 3),
                checked((byte)(value >> 5)),
                checked((byte)(value >> 13)),
            ];
        }

        if (value <= 0x0FFF_FFFF)
        {
            return
            [
                (byte)((value << 4) | 7),
                checked((byte)(value >> 4)),
                checked((byte)(value >> 12)),
                checked((byte)(value >> 20)),
            ];
        }

        var encoded = new byte[5];
        encoded[0] = 0x0F;
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(1), value);
        return encoded;
    }
}
