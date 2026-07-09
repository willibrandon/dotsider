using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="EhFrameReader"/> — the ELF boundary fallback — driven with hand-built
/// <c>.eh_frame</c> blobs covering the pointer-encoding matrix (absolute, PC-relative, fixed,
/// LEB128, signed), CIE augmentation shapes, and the terminator and damage behaviors.
/// </summary>
[TestClass]
public class EhFrameReaderTests
{
    private static byte[] Entry(DwarfBlob content) =>
        new DwarfBlob().U32((uint)content.Length).Bytes(content.ToArray()).ToArray();

    /// <summary>Builds a CIE whose augmentation declares the FDE pointer encoding.</summary>
    private static byte[] Cie(byte fdeEncoding, string augmentation = "zR", byte version = 1)
    {
        var b = new DwarfBlob()
            .U32(0)             // CIE id
            .U8(version)
            .CStr(augmentation)
            .ULeb(1)            // code alignment
            .SLeb(-8);          // data alignment
        if (version == 1) b.U8(16);
        else b.ULeb(16);        // return address register

        if (augmentation.StartsWith('z'))
        {
            var data = new DwarfBlob();
            foreach (var ch in augmentation[1..])
            {
                switch (ch)
                {
                    case 'P': data.U8(0x00).U64(0x12345678); break; // absptr personality
                    case 'L': data.U8(0x00); break;                 // LSDA encoding
                    case 'R': data.U8(fdeEncoding); break;
                }
            }

            b.ULeb((ulong)data.Length).Bytes(data.ToArray());
        }

        return Entry(b);
    }

    private static byte[] Fde(uint ciePointer, DwarfBlob pointers) =>
        Entry(new DwarfBlob().U32(ciePointer).Bytes(pointers.ToArray()));

    private static byte[] Image(ulong ehFrameAddress, params byte[][] entries)
    {
        var section = new List<byte>();
        foreach (var e in entries) section.AddRange(e);
        section.AddRange([0, 0, 0, 0]); // terminator
        return SyntheticImageBuilders.BuildElf(
            (".text", 0x401000, new byte[0x100]),
            (".eh_frame", ehFrameAddress, section.ToArray()));
    }

    /// <summary>
    /// Verifies absolute 64-bit pointers decode and the boundary maps to its containing section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_Absptr_DecodesAndMapsSection()
    {
        var cie = Cie(0x00);
        var fde = Fde((uint)(cie.Length + 4), new DwarfBlob().U64(0x401010).U64(0x40));

        var boundaries = EhFrameReader.ReadBoundaries(Image(0x500000, cie, fde));

        var b = Assert.ContainsSingle(boundaries);
        Assert.AreEqual("sub_401010", b.Name);
        Assert.AreEqual(0x401010UL, b.VirtualAddress);
        Assert.AreEqual(0x40, b.Size);
        Assert.AreEqual(".text", b.Section);
        Assert.IsTrue(b.IsBoundary);
        Assert.IsNotNull(b.FileOffset);
    }

    /// <summary>
    /// Verifies the common <c>pcrel|sdata4</c> encoding: the location is relative to the
    /// pointer field's own address, and the range is a plain length.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_PcrelSdata4_ResolvesAgainstFieldAddress()
    {
        const ulong ehFrameAddress = 0x500000;
        var cie = Cie(0x1B); // pcrel | sdata4

        // First FDE: id at cie.Length + 4; initial_location field 4 bytes later.
        var field1 = ehFrameAddress + (ulong)cie.Length + 8;
        var delta1 = unchecked((uint)(int)(0x401010 - (long)field1));
        var fde1 = Fde((uint)(cie.Length + 4), new DwarfBlob().U32(delta1).U32(0x40));

        var field2 = ehFrameAddress + (ulong)(cie.Length + fde1.Length) + 8;
        var delta2 = unchecked((uint)(int)(0x401080 - (long)field2));
        var fde2 = Fde((uint)(cie.Length + fde1.Length + 4), new DwarfBlob().U32(delta2).U32(0x10));

        var boundaries = EhFrameReader.ReadBoundaries(Image(ehFrameAddress, cie, fde1, fde2));

        Assert.HasCount(2, boundaries);
        Assert.AreEqual(0x401010UL, boundaries[0].VirtualAddress);
        Assert.AreEqual(0x40, boundaries[0].Size);
        Assert.AreEqual(0x401080UL, boundaries[1].VirtualAddress);
        Assert.AreEqual(0x10, boundaries[1].Size);
    }

    /// <summary>Verifies the fixed udata4 and variable ULEB128 formats decode absolutely.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_Udata4AndUleb_Decode()
    {
        var cie4 = Cie(0x03); // udata4 absolute
        var fde4 = Fde((uint)(cie4.Length + 4), new DwarfBlob().U32(0x401020).U32(0x20));
        var one = Assert.ContainsSingle(EhFrameReader.ReadBoundaries(Image(0x500000, cie4, fde4)));
        Assert.AreEqual(0x401020UL, one.VirtualAddress);
        Assert.AreEqual(0x20, one.Size);

        var cieLeb = Cie(0x01); // uleb128 absolute
        var fdeLeb = Fde((uint)(cieLeb.Length + 4), new DwarfBlob().ULeb(0x401030).ULeb(0x30));
        var leb = Assert.ContainsSingle(EhFrameReader.ReadBoundaries(Image(0x500000, cieLeb, fdeLeb)));
        Assert.AreEqual(0x401030UL, leb.VirtualAddress);
        Assert.AreEqual(0x30, leb.Size);
    }

    /// <summary>
    /// Verifies a <c>zPLR</c> augmentation with a personality pointer parses the encoding behind
    /// it, on a version-3 CIE whose return register is a ULEB.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_ZplrVersion3_ReadsEncodingBehindPersonality()
    {
        var cie = Cie(0x03, augmentation: "zPLR", version: 3);
        var fde = Fde((uint)(cie.Length + 4), new DwarfBlob().U32(0x401040).U32(0x18));

        var b = Assert.ContainsSingle(EhFrameReader.ReadBoundaries(Image(0x500000, cie, fde)));
        Assert.AreEqual(0x401040UL, b.VirtualAddress);
        Assert.AreEqual(0x18, b.Size);
    }

    /// <summary>
    /// Verifies the zero-length terminator stops the walk, and entries after it are ignored.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_Terminator_StopsWalk()
    {
        var cie = Cie(0x00);
        var fde = Fde((uint)(cie.Length + 4), new DwarfBlob().U64(0x401010).U64(0x40));
        var section = new List<byte>();
        section.AddRange(cie);
        section.AddRange(fde);
        section.AddRange([0, 0, 0, 0]); // terminator
        section.AddRange(fde);          // unreachable duplicate

        var image = SyntheticImageBuilders.BuildElf((".eh_frame", 0x500000, section.ToArray()));

        Assert.ContainsSingle(EhFrameReader.ReadBoundaries(image));
    }

    /// <summary>
    /// Verifies defective entries are skipped, not misread: a zero location, an FDE naming an
    /// unknown CIE, and a truncated tail after a good FDE.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadBoundaries_DefectiveEntries_SkippedOrKeptPartial()
    {
        var cie = Cie(0x00);
        var zeroLocation = Fde((uint)(cie.Length + 4), new DwarfBlob().U64(0).U64(0x40));
        var orphan = Fde(2, new DwarfBlob().U64(0x401050).U64(0x40)); // points into no CIE
        Assert.IsEmpty(EhFrameReader.ReadBoundaries(Image(0x500000, cie, zeroLocation)));
        Assert.IsEmpty(EhFrameReader.ReadBoundaries(Image(0x500000, orphan)));

        var good = Fde((uint)(cie.Length + 4), new DwarfBlob().U64(0x401010).U64(0x40));
        var truncated = new DwarfBlob().U32(0xFFF0).U32(0).ToArray(); // claims more than present
        var section = new List<byte>();
        section.AddRange(cie);
        section.AddRange(good);
        section.AddRange(truncated);
        var image = SyntheticImageBuilders.BuildElf((".eh_frame", 0x500000, section.ToArray()));

        Assert.ContainsSingle(EhFrameReader.ReadBoundaries(image));

        Assert.IsEmpty(EhFrameReader.ReadBoundaries(
            SyntheticImageBuilders.BuildElf((".text", 0x401000, new byte[8]))));
    }
}
