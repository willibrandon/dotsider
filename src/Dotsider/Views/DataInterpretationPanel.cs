using System.Buffers.Binary;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Renders a 4×4 data interpretation panel showing the bytes at the current cursor
/// position as multiple numeric types. Updates every frame (immediate mode).
/// </summary>
public static class DataInterpretationPanel
{
    /// <summary>
    /// Builds the data interpretation grid widget showing numeric conversions
    /// of the bytes at the current hex cursor position.
    /// </summary>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var doc = state.HexEditorState.Document;
        int byteOffset;
        if (state.HexEditorState.ByteCursorOffset is int bco)
            byteOffset = Math.Clamp(bco, 0, doc.ByteCount - 1);
        else
        {
            var byteMap = doc.GetByteMap();
            var charOff = Math.Min(state.HexEditorState.Cursor.Position.Value, byteMap.CharCount);
            byteOffset = charOff < byteMap.CharCount
                ? byteMap.CharToByteStart(charOff)
                : Math.Max(0, doc.ByteCount - 1);
        }

        var available = Math.Min(8, doc.ByteCount - byteOffset);
        ReadOnlySpan<byte> bytes = available > 0 ? doc.GetBytes(byteOffset, available).Span : [];
        var le = state.HexEndianness == HexEndianness.Little;
        var endianLabel = le ? "LE" : "BE";

        // Read values with partial-read safety
        var b0 = available >= 1 ? bytes[0] : (byte?)null;
        var i8 = b0.HasValue ? (sbyte)b0.Value : (sbyte?)null;
        var u8 = b0;

        short? i16 = available >= 2
            ? (le ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : BinaryPrimitives.ReadInt16BigEndian(bytes))
            : null;
        ushort? u16 = available >= 2
            ? (le ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes))
            : null;

        int? i32 = available >= 4
            ? (le ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes))
            : null;
        uint? u32 = available >= 4
            ? (le ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes))
            : null;

        long? i64 = available >= 8
            ? (le ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : BinaryPrimitives.ReadInt64BigEndian(bytes))
            : null;
        ulong? u64 = available >= 8
            ? (le ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : BinaryPrimitives.ReadUInt64BigEndian(bytes))
            : null;

        float? f32 = available >= 4
            ? (le ? BinaryPrimitives.ReadSingleLittleEndian(bytes) : BinaryPrimitives.ReadSingleBigEndian(bytes))
            : null;
        double? f64 = available >= 8
            ? (le ? BinaryPrimitives.ReadDoubleLittleEndian(bytes) : BinaryPrimitives.ReadDoubleBigEndian(bytes))
            : null;

        var binaryStr = b0.HasValue ? Convert.ToString(b0.Value, 2).PadLeft(8, '0') : "-";

        string Fmt<T>(T? val) where T : struct => val.HasValue ? val.Value.ToString()! : "-";
        string FmtF(float? val) => val.HasValue ? val.Value.ToString("G6") : "-";
        string FmtD(double? val) => val.HasValue ? val.Value.ToString("G6") : "-";

        return ctx.Border(
            ctx.Grid(g =>
            {
                g.Columns.Add(SizeHint.Fill);
                g.Columns.Add(SizeHint.Fill);
                g.Columns.Add(SizeHint.Fill);
                g.Columns.Add(SizeHint.Fill);
                return
                [
                    // Row 0
                    g.Cell(c => c.Text($" Int8: {Fmt(i8)}")).Row(0).Column(0),
                    g.Cell(c => c.Text($" Int32: {Fmt(i32)}")).Row(0).Column(1),
                    g.Cell(c => c.Text($" Hex: {(b0.HasValue ? $"0x{b0.Value:X2}" : "-")}")).Row(0).Column(2),
                    g.Cell(c => c.Text($" Float32: {FmtF(f32)}")).Row(0).Column(3),
                    // Row 1
                    g.Cell(c => c.Text($" UInt8: {Fmt(u8)}")).Row(1).Column(0),
                    g.Cell(c => c.Text($" UInt32: {Fmt(u32)}")).Row(1).Column(1),
                    g.Cell(c => c.Text($" Octal: {(b0.HasValue ? $"0{Convert.ToString(b0.Value, 8)}" : "-")}")).Row(1).Column(2),
                    g.Cell(c => c.Text($" Float64: {FmtD(f64)}")).Row(1).Column(3),
                    // Row 2
                    g.Cell(c => c.Text($" Int16: {Fmt(i16)}")).Row(2).Column(0),
                    g.Cell(c => c.Text($" Int64: {Fmt(i64)}")).Row(2).Column(1),
                    g.Cell(c => c.Text($" Binary: {binaryStr}")).Row(2).Column(2),
                    g.Cell(c => c.Text($" Offset: 0x{byteOffset:X}")).Row(2).Column(3),
                    // Row 3
                    g.Cell(c => c.Text($" UInt16: {Fmt(u16)}")).Row(3).Column(0),
                    g.Cell(c => c.Text($" UInt64: {Fmt(u64)}")).Row(3).Column(1),
                    g.Cell(c => c.Text($" Length: {doc.ByteCount}")).Row(3).Column(2),
                    g.Cell(c => c.Text($" Endian: {endianLabel} (e)")).Row(3).Column(3),
                ];
            })
        ).Title(" Data Interpretation ").FixedHeight(6);
    }
}
