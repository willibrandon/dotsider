using System.Buffers.Binary;
using Dotsider.Core.Analysis;

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
    /// Verifies a DYLD_CHAINED_PTR_64_OFFSET rebase pointer (target is an offset from the
    /// image base) resolves to the image base plus the offset and maps to its file offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolvePointer_ChainedOffsetFormat_ResolvesToImageBasePlusTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO(pointerFormat: 6));
        Assert.NotNull(space);
        Assert.Equal(8, space.PointerSize);

        // Offset form: the raw value's low 36 bits are the offset from the image base.
        Assert.Equal(ImageBase + 0x40, space.ResolvePointer(0x40));

        Assert.True(space.TryGetFileOffset(0x40, out var fileOffset, out _));
        Assert.Equal(0x40, fileOffset);
    }

    /// <summary>
    /// Verifies a DYLD_CHAINED_PTR_64 rebase pointer (target is an absolute unslid address)
    /// resolves to that address and maps to its file offset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolvePointer_ChainedAbsoluteFormat_ResolvesToTarget()
    {
        var space = NativeAddressSpace.Create(BuildMachO(pointerFormat: 2));
        Assert.NotNull(space);

        // Absolute form with a nonzero next/high field that must be masked away.
        var raw = (0x123UL << 51) | (ImageBase + 0x80); // next bits set; low 36 hold the address
        Assert.Equal(ImageBase + 0x80, space.ResolvePointer(raw));

        Assert.True(space.TryGetFileOffset(raw, out var fileOffset, out _));
        Assert.Equal(0x80, fileOffset);
    }

    /// <summary>
    /// Verifies an import bind pointer (high bit set) is left unresolved rather than decoded
    /// as a local target.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolvePointer_BindPointer_IsNotDecoded()
    {
        var space = NativeAddressSpace.Create(BuildMachO(pointerFormat: 6));
        Assert.NotNull(space);

        var bind = 0x8000_0000_0000_0000UL | 0x40;
        Assert.Equal(bind, space.ResolvePointer(bind));
    }

    /// <summary>
    /// Builds a minimal 64-bit Mach-O with one __TEXT segment and an LC_DYLD_CHAINED_FIXUPS
    /// command whose starts table declares the given pointer format.
    /// </summary>
    private static byte[] BuildMachO(ushort pointerFormat)
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

        // LC_DYLD_CHAINED_FIXUPS
        w.U32(104, 0x80000034);      // cmd
        w.U32(108, 16);              // cmdsize
        w.U32(112, 0x200);           // dataoff
        w.U32(116, 0x100);           // datasize

        // dyld_chained_fixups_header at 0x200
        w.U32(0x204, 32);            // starts_offset

        // dyld_chained_starts_in_image at 0x220
        w.U32(0x220, 1);             // seg_count
        w.U32(0x224, 12);            // seg_info_offset[0] (relative to starts base)

        // dyld_chained_starts_in_segment at 0x22C
        w.U32(0x22C, 24);            // size
        w.U16(0x230, 0x4000);        // page_size
        w.U16(0x232, pointerFormat); // pointer_format

        return image;
    }

    private readonly ref struct Writer(Span<byte> span)
    {
        private readonly Span<byte> _span = span;

        public void U16(int offset, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(_span[offset..], value);

        public void U32(int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(_span[offset..], value);

        public void U64(int offset, ulong value) =>
            BinaryPrimitives.WriteUInt64LittleEndian(_span[offset..], value);
    }
}
