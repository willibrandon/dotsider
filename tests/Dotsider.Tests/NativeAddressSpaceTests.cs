using Dotsider.Core.Analysis;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeAddressSpace"/> Mach-O chained-fixup pointer decoding, using
/// synthetic images so the path is exercised on every platform (a real Mach-O is only built
/// on the macOS CI runner). Covers the two rebase pointer formats .NET AOT emits:
/// image-base-relative offsets (arm64) and absolute targets (x64).
/// </summary>
public class NativeAddressSpaceTests
{
    private const ulong ImageBase = 0x1_0000_0000;

    /// <summary>
    /// Verifies the offset rebase form (DYLD_CHAINED_PTR_64_OFFSET, arm64) decodes a pointer
    /// to the image base plus the target, ignoring the packed next/bind bits, and that the
    /// decoded address maps to its file offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DecodeChainedRebase_OffsetForm_ResolvesToImageBasePlusTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO());
        Assert.NotNull(space);
        Assert.Equal(8, space.PointerSize);
        Assert.True(space.MachOChained);
        Assert.Equal(ImageBase, space.MachOImageBase);

        var raw = (0x123UL << 51) | 0x40; // next bits set; low 36 hold the offset
        var va = NativeAddressSpace.DecodeChainedRebase(raw, offsetForm: true, space.MachOImageBase);
        Assert.Equal(ImageBase + 0x40, va);

        Assert.True(space.TryGetFileOffset(va, out var fileOffset, out _));
        Assert.Equal(0x40, fileOffset);
    }

    /// <summary>
    /// Verifies the absolute rebase form (DYLD_CHAINED_PTR_64, x64) decodes a pointer to the
    /// absolute address in its low bits and that the decoded address maps to its file offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DecodeChainedRebase_AbsoluteForm_ResolvesToTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO());
        Assert.NotNull(space);

        var raw = (0x123UL << 51) | (ImageBase + 0x80); // next bits set; low 36 hold the address
        var va = NativeAddressSpace.DecodeChainedRebase(raw, offsetForm: false, space.MachOImageBase);
        Assert.Equal(ImageBase + 0x80, va);

        Assert.True(space.TryGetFileOffset(va, out var fileOffset, out _));
        Assert.Equal(0x80, fileOffset);
    }

    /// <summary>
    /// Verifies an import bind pointer (high bit set) is left unchanged rather than decoded
    /// as a local target.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DecodeChainedRebase_BindPointer_IsNotDecoded()
    {
        var bind = 0x8000_0000_0000_0000UL | 0x40;
        Assert.Equal(bind, NativeAddressSpace.DecodeChainedRebase(bind, offsetForm: true, ImageBase));
    }

    /// <summary>
    /// Builds a minimal 64-bit Mach-O with one __TEXT segment and an LC_DYLD_CHAINED_FIXUPS
    /// command, so the address space reports a chained image with the __TEXT base.
    /// </summary>
    private static byte[] BuildMachO()
    {
        var image = new byte[0x2000];
        var w = new Writer(image);

        // mach_header_64
        w.U32(0, 0xFEEDFACF);        // magic (64-bit little-endian)
        w.U32(4, 0x0100000C);        // cputype ARM64
        w.U32(16, 2);                // ncmds
        w.U32(20, 88);               // sizeofcmds

        // LC_SEGMENT_64 __TEXT, covering the whole file
        w.U32(32, 0x19);             // cmd
        w.U32(36, 72);               // cmdsize
        System.Text.Encoding.ASCII.GetBytes("__TEXT").CopyTo(image, 40);
        w.U64(56, ImageBase);        // vmaddr
        w.U64(64, 0x2000);           // vmsize
        w.U64(72, 0);                // fileoff
        w.U64(80, 0x2000);           // filesize

        // LC_DYLD_CHAINED_FIXUPS (presence marks the image as chained)
        w.U32(104, 0x80000034);      // cmd
        w.U32(108, 16);              // cmdsize
        w.U32(112, 0x200);           // dataoff
        w.U32(116, 0x100);           // datasize

        return image;
    }

    private readonly ref struct Writer(Span<byte> span)
    {
        private readonly Span<byte> _span = span;

        public void U32(int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(_span[offset..], value);

        public void U64(int offset, ulong value) =>
            BinaryPrimitives.WriteUInt64LittleEndian(_span[offset..], value);
    }
}
