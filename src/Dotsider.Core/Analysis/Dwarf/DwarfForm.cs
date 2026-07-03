namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// The <c>DW_FORM_*</c> codes the reader decodes or skips, plus the <c>DW_TAG_*</c> and
/// <c>DW_AT_*</c> codes it recognizes — the DWARF 4/5 vocabulary needed to walk subprograms.
/// </summary>
internal static class DwarfForm
{
    // Forms
    public const ulong Addr = 0x01;
    public const ulong Block2 = 0x03;
    public const ulong Block4 = 0x04;
    public const ulong Data2 = 0x05;
    public const ulong Data4 = 0x06;
    public const ulong Data8 = 0x07;
    public const ulong String = 0x08;
    public const ulong Block = 0x09;
    public const ulong Block1 = 0x0A;
    public const ulong Data1 = 0x0B;
    public const ulong Flag = 0x0C;
    public const ulong Sdata = 0x0D;
    public const ulong Strp = 0x0E;
    public const ulong Udata = 0x0F;
    public const ulong RefAddr = 0x10;
    public const ulong Ref1 = 0x11;
    public const ulong Ref2 = 0x12;
    public const ulong Ref4 = 0x13;
    public const ulong Ref8 = 0x14;
    public const ulong RefUdata = 0x15;
    public const ulong Indirect = 0x16;
    public const ulong SecOffset = 0x17;
    public const ulong Exprloc = 0x18;
    public const ulong FlagPresent = 0x19;
    public const ulong Strx = 0x1A;
    public const ulong Addrx = 0x1B;
    public const ulong RefSup4 = 0x1C;
    public const ulong StrpSup = 0x1D;
    public const ulong Data16 = 0x1E;
    public const ulong LineStrp = 0x1F;
    public const ulong RefSig8 = 0x20;
    public const ulong ImplicitConst = 0x21;
    public const ulong Loclistx = 0x22;
    public const ulong Rnglistx = 0x23;
    public const ulong RefSup8 = 0x24;
    public const ulong Strx1 = 0x25;
    public const ulong Strx2 = 0x26;
    public const ulong Strx3 = 0x27;
    public const ulong Strx4 = 0x28;
    public const ulong Addrx1 = 0x29;
    public const ulong Addrx2 = 0x2A;
    public const ulong Addrx3 = 0x2B;
    public const ulong Addrx4 = 0x2C;

    // Tags
    public const ulong TagCompileUnit = 0x11;
    public const ulong TagSubprogram = 0x2E;

    // Attributes
    public const ulong AtName = 0x03;
    public const ulong AtStmtList = 0x10;
    public const ulong AtLowPc = 0x11;
    public const ulong AtHighPc = 0x12;
    public const ulong AtAbstractOrigin = 0x31;
    public const ulong AtDeclFile = 0x3A;
    public const ulong AtDeclLine = 0x3B;
    public const ulong AtSpecification = 0x47;
    public const ulong AtRanges = 0x55;
    public const ulong AtLinkageName = 0x6E;
    public const ulong AtMipsLinkageName = 0x2007;
    public const ulong AtStrOffsetsBase = 0x72;
    public const ulong AtAddrBase = 0x73;
    public const ulong AtRnglistsBase = 0x74;
}
