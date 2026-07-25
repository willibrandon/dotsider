using Dotsider.Core.Analysis;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeAddressSpace"/> Mach-O chained-fixup pointer decoding, using
/// synthetic images so the path is exercised on every platform (a real Mach-O is only built
/// on the macOS CI runner). Covers the two rebase pointer formats .NET AOT emits:
/// image-base-relative offsets (arm64) and absolute targets (x64).
/// </summary>
[TestClass]
public sealed class NativeAddressSpaceTests
{
    private const ulong ImageBase = 0x1_0000_0000;

    /// <summary>
    /// Verifies the offset rebase form (DYLD_CHAINED_PTR_64_OFFSET, arm64) decodes a pointer
    /// to the image base plus the target, ignoring the packed next/bind bits, and that the
    /// decoded address maps to its file offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DecodeChainedRebase_OffsetForm_ResolvesToImageBasePlusTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO());
        Assert.IsNotNull(space);
        Assert.AreEqual(8, space.PointerSize);
        Assert.IsTrue(space.MachOChained);
        Assert.AreEqual(ImageBase, space.MachOImageBase);

        var raw = (0x123UL << 51) | 0x40; // next bits set; low 36 hold the offset
        Assert.IsTrue(NativeAddressSpace.TryDecodeChainedRebase(
            raw,
            offsetForm: true,
            space.MachOImageBase,
            out var va));
        Assert.AreEqual(ImageBase + 0x40, va);

        Assert.IsTrue(space.TryGetFileOffset(va, out var fileOffset, out _));
        Assert.AreEqual(0x40, fileOffset);
    }

    /// <summary>
    /// Verifies the absolute rebase form (DYLD_CHAINED_PTR_64, x64) decodes a pointer to the
    /// absolute address in its low bits and that the decoded address maps to its file offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DecodeChainedRebase_AbsoluteForm_ResolvesToTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO());
        Assert.IsNotNull(space);

        var raw = (0x123UL << 51) | (ImageBase + 0x80); // next bits set; low 36 hold the address
        Assert.IsTrue(NativeAddressSpace.TryDecodeChainedRebase(
            raw,
            offsetForm: false,
            space.MachOImageBase,
            out var va));
        Assert.AreEqual(ImageBase + 0x80, va);

        Assert.IsTrue(space.TryGetFileOffset(va, out var fileOffset, out _));
        Assert.AreEqual(0x80, fileOffset);
    }

    /// <summary>
    /// Verifies an import bind pointer (high bit set) is left unchanged rather than decoded
    /// as a local target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DecodeChainedRebase_BindPointer_IsNotDecoded()
    {
        var bind = 0x8000_0000_0000_0000UL | 0x40;
        Assert.IsTrue(NativeAddressSpace.TryDecodeChainedRebase(
            bind,
            offsetForm: true,
            ImageBase,
            out var address));
        Assert.AreEqual(bind, address);
    }

    /// <summary>
    /// Verifies an image-base-relative target whose addition would overflow remains unmapped
    /// rather than wrapping to a plausible low address.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DecodeChainedRebase_OverflowingOffset_DoesNotWrap()
    {
        const ulong raw = 0x40;

        Assert.IsFalse(NativeAddressSpace.TryDecodeChainedRebase(
            raw,
            offsetForm: true,
            ulong.MaxValue - 0x20,
            out _));
    }

    /// <summary>
    /// Builds a minimal 64-bit Mach-O with one __TEXT segment and an LC_DYLD_CHAINED_FIXUPS
    /// command, so the address space reports a chained image with the __TEXT base.
    /// </summary>
    private static byte[] BuildMachO()
    {
        var image = new byte[0x2000];

        // mach_header_64
        BinaryPrimitives.WriteUInt32LittleEndian(image, 0xFEEDFACF); // magic
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), 0x0100000C); // ARM64
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(16), 2); // ncmds
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), 88); // sizeofcmds

        // LC_SEGMENT_64 __TEXT, covering the whole file
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(32), 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(36), 72);
        System.Text.Encoding.ASCII.GetBytes("__TEXT").CopyTo(image, 40);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(56), ImageBase);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(64), 0x2000);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(72), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(80), 0x2000);

        // LC_DYLD_CHAINED_FIXUPS (presence marks the image as chained)
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(104), 0x80000034);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(108), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(112), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(116), 0x100);

        return image;
    }
}
