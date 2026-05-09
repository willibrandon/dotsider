using System.Buffers.Binary;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Renders a data interpretation panel showing the bytes at the current cursor
/// position as multiple numeric types. The editor document stores 4 row-major
/// lines with tab-separated fields. The <see cref="DataInterpViewRenderer"/>
/// renders them as a 4×4 matrix at proportional column widths using the actual
/// viewport width — no build-time width estimation needed.
/// </summary>
public static class DataInterpretationPanel
{
    /// <summary>
    /// Builds the data interpretation editor widget showing numeric conversions
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

        var hexVal = b0.HasValue ? $"0x{b0.Value:X2}" : "-";
        var octalVal = b0.HasValue ? $"0{Convert.ToString(b0.Value, 8)}" : "-";

        // Row-major document: 4 lines, each with 4 tab-separated fields.
        // The renderer splits on \t and draws each field at proportional
        // column positions using viewport.Width. Tab separators let vim
        // motions (w/b/e) jump between fields naturally, and j/k moves
        // between visual rows because each line = one visual row.
        var text = string.Join("\n",
            $" Int8: {Fmt(i8)}\t Int32: {Fmt(i32)}\t Hex: {hexVal}\t Float32: {FmtF(f32)}",
            $" UInt8: {Fmt(u8)}\t UInt32: {Fmt(u32)}\t Octal: {octalVal}\t Float64: {FmtD(f64)}",
            $" Int16: {Fmt(i16)}\t Int64: {Fmt(i64)}\t Binary: {binaryStr}\t Offset: 0x{byteOffset:X}",
            $" UInt16: {Fmt(u16)}\t UInt64: {Fmt(u64)}\t Length: {doc.ByteCount}\t Endian: {endianLabel} (e)");

        if (state.DataInterpEditorText != text)
        {
            state.DataInterpEditorText = text;
            state.DataInterpEditorState = new EditorState(new Hex1bDocument(text)) { IsReadOnly = true };
        }

        // Adjust word boundaries after double-click (consistent with other info editors)
        if (state.DataInterpEditorState is not null && state.CurrentTab == TabId.HexDump)
        {
            IlInspectorView.AdjustWordSelectionCursorOneShot(
                state.DataInterpEditorState,
                ref state.DataInterpPrevSelectionAnchor,
                ref state.DataInterpPrevCursorPosition);
        }

        return ctx.Border(
            ctx.ThemePanel(t => t
                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
            ctx.Editor(state.DataInterpEditorState!)
                .ViewRenderer(DataInterpViewRenderer.Instance)
                .Decorations(state.DataInterpYankProvider)
                .InputBindings(bindings =>
                {
                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                        bindings,
                        state.DataInterpEditorState!,
                        () => state.VimPending,
                        () => state.VimPendingEditor,
                        () => state.VimPendingCursorOffset,
                        () => state.VimPendingTimestamp,
                        (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                        state.PerformEditorYank,
                        () => state.App.Invalidate());
                })
                .FillWidth().FillHeight())
        ).Title(" Data Interpretation ").FixedHeight(6);
    }
}
