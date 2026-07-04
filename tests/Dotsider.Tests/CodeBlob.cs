namespace Dotsider.Tests;

/// <summary>
/// A fluent little-endian machine-code composer for decoder tests — the <c>CodeBlob</c> analog of
/// <see cref="DwarfBlob"/>. Beyond raw bytes it carries x86-64 encoding helpers (REX, ModRM, SIB,
/// displacements, 2/3-byte VEX, EVEX) and an A64 <see cref="Word"/>, so a per-encoding-family test
/// composes an exact instruction and asserts the decode.
/// </summary>
internal sealed class CodeBlob
{
    private readonly List<byte> _bytes = [];

    /// <summary>The byte count written so far.</summary>
    public int Length => _bytes.Count;

    /// <summary>Writes one byte.</summary>
    public CodeBlob U8(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    /// <summary>Writes a little-endian u16.</summary>
    public CodeBlob U16(ushort value)
    {
        _bytes.Add((byte)value);
        _bytes.Add((byte)(value >> 8));
        return this;
    }

    /// <summary>Writes a little-endian u32.</summary>
    public CodeBlob U32(uint value)
    {
        for (var i = 0; i < 4; i++) _bytes.Add((byte)(value >> (8 * i)));
        return this;
    }

    /// <summary>Writes a little-endian u64.</summary>
    public CodeBlob U64(ulong value)
    {
        for (var i = 0; i < 8; i++) _bytes.Add((byte)(value >> (8 * i)));
        return this;
    }

    /// <summary>Writes a signed byte.</summary>
    public CodeBlob I8(sbyte value) => U8((byte)value);

    /// <summary>Writes a little-endian signed i32.</summary>
    public CodeBlob I32(int value) => U32((uint)value);

    /// <summary>Appends raw bytes.</summary>
    public CodeBlob Bytes(params byte[] data)
    {
        _bytes.AddRange(data);
        return this;
    }

    /// <summary>Writes a REX prefix (<c>0x40 | WRXB</c>).</summary>
    public CodeBlob Rex(bool w = false, bool r = false, bool x = false, bool b = false) =>
        U8((byte)(0x40 | (w ? 8 : 0) | (r ? 4 : 0) | (x ? 2 : 0) | (b ? 1 : 0)));

    /// <summary>Writes a ModRM byte from mod (0-3), reg (0-7), rm (0-7).</summary>
    public CodeBlob ModRM(int mod, int reg, int rm) =>
        U8((byte)(((mod & 3) << 6) | ((reg & 7) << 3) | (rm & 7)));

    /// <summary>Writes a SIB byte from scale (1/2/4/8), index (0-7), base (0-7).</summary>
    public CodeBlob Sib(int scale, int index, int @base)
    {
        var s = scale switch { 1 => 0, 2 => 1, 4 => 2, 8 => 3, _ => 0 };
        return U8((byte)((s << 6) | ((index & 7) << 3) | (@base & 7)));
    }

    /// <summary>Writes an 8-bit displacement.</summary>
    public CodeBlob Disp8(sbyte value) => I8(value);

    /// <summary>Writes a 32-bit displacement.</summary>
    public CodeBlob Disp32(int value) => I32(value);

    /// <summary>
    /// Writes a 2-byte VEX prefix (<c>0xC5</c>): implies map <c>0F</c> and <c>W=0</c>. Fields are
    /// stored inverted per the encoding.
    /// </summary>
    /// <param name="r">REX.R (true = extends the reg field to r8+).</param>
    /// <param name="vvvv">The additional source register (0-15).</param>
    /// <param name="l">Vector length bit (0 = 128, 1 = 256).</param>
    /// <param name="pp">Mandatory prefix (0=none, 1=66, 2=F3, 3=F2).</param>
    public CodeBlob Vex2(bool r, int vvvv, int l, int pp)
    {
        U8(0xC5);
        var b = ((r ? 0 : 1) << 7) | ((~vvvv & 0xF) << 3) | ((l & 1) << 2) | (pp & 3);
        return U8((byte)b);
    }

    /// <summary>
    /// Writes a 3-byte VEX prefix (<c>0xC4</c>). Fields are stored inverted per the encoding.
    /// </summary>
    /// <param name="r">REX.R.</param>
    /// <param name="x">REX.X.</param>
    /// <param name="b">REX.B.</param>
    /// <param name="map">Opcode map (1=0F, 2=0F38, 3=0F3A).</param>
    /// <param name="w">VEX.W.</param>
    /// <param name="vvvv">The additional source register (0-15).</param>
    /// <param name="l">Vector length bit (0 = 128, 1 = 256).</param>
    /// <param name="pp">Mandatory prefix (0=none, 1=66, 2=F3, 3=F2).</param>
    public CodeBlob Vex3(bool r, bool x, bool b, int map, bool w, int vvvv, int l, int pp)
    {
        U8(0xC4);
        var b1 = ((r ? 0 : 1) << 7) | ((x ? 0 : 1) << 6) | ((b ? 0 : 1) << 5) | (map & 0x1F);
        var b2 = ((w ? 1 : 0) << 7) | ((~vvvv & 0xF) << 3) | ((l & 1) << 2) | (pp & 3);
        return U8((byte)b1).U8((byte)b2);
    }

    /// <summary>
    /// Writes a 4-byte EVEX prefix (<c>0x62</c>). Extension bits default to low registers/masks;
    /// fields are stored inverted per the encoding.
    /// </summary>
    /// <param name="map">Opcode map (1=0F, 2=0F38, 3=0F3A).</param>
    /// <param name="pp">Mandatory prefix (0=none, 1=66, 2=F3, 3=F2).</param>
    /// <param name="w">EVEX.W.</param>
    /// <param name="ll">Vector length (0=128, 1=256, 2=512).</param>
    /// <param name="vvvv">The additional source register (0-31).</param>
    /// <param name="aaa">The mask register (0-7; 0 = no masking).</param>
    /// <param name="z">Zeroing flag.</param>
    /// <param name="broadcast">Broadcast/RC/SAE flag (EVEX.b).</param>
    /// <param name="r">REX.R.</param>
    /// <param name="x">REX.X.</param>
    /// <param name="b">REX.B.</param>
    /// <param name="r2">EVEX.R' (high bit of reg).</param>
    /// <param name="v2">EVEX.V' (high bit of vvvv).</param>
    public CodeBlob Evex(
        int map, int pp, bool w, int ll, int vvvv, int aaa = 0, bool z = false, bool broadcast = false,
        bool r = false, bool x = false, bool b = false, bool r2 = false, bool v2 = false)
    {
        U8(0x62);
        var p0 = ((r ? 0 : 1) << 7) | ((x ? 0 : 1) << 6) | ((b ? 0 : 1) << 5) | ((r2 ? 0 : 1) << 4) | (map & 3);
        var p1 = ((w ? 1 : 0) << 7) | ((~vvvv & 0xF) << 3) | (1 << 2) | (pp & 3);
        var p2 = ((z ? 1 : 0) << 7) | (((ll >> 1) & 1) << 6) | ((ll & 1) << 5) | ((broadcast ? 1 : 0) << 4)
            | ((v2 ? 0 : 1) << 3) | (aaa & 7);
        return U8((byte)p0).U8((byte)p1).U8((byte)p2);
    }

    /// <summary>Writes a little-endian A64 32-bit instruction word.</summary>
    public CodeBlob Word(uint value) => U32(value);

    /// <summary>Materializes the composed bytes.</summary>
    public byte[] ToArray() => [.. _bytes];
}
