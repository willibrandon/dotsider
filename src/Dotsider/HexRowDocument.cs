using Hex1b.Documents;

namespace Dotsider;

/// <summary>
/// Wraps an <see cref="IHex1bDocument"/> so that line-related methods (LineCount,
/// OffsetToPosition, PositionToOffset, GetLineText, GetLineLength) return values
/// based on fixed-width hex rows instead of newline-delimited document lines.
///
/// This fixes EditorNode.EnsureCursorVisible() which uses OffsetToPosition() to
/// compute the cursor's line number for scroll adjustment. Without this wrapper,
/// EnsureCursorVisible computes document lines (based on \n bytes in raw binary data)
/// while the hex renderer interprets scrollOffset as hex dump rows (based on bytesPerRow).
/// </summary>
public sealed class HexRowDocument : IHex1bDocument
{
    private readonly IHex1bDocument _inner;
    private int _bytesPerRow = 16;

    public HexRowDocument(IHex1bDocument inner)
    {
        _inner = inner;
    }

    /// <summary>
    /// Number of bytes per hex row. Updated by the renderer when viewport width changes.
    /// </summary>
    public int BytesPerRow
    {
        get => _bytesPerRow;
        set => _bytesPerRow = Math.Max(1, value);
    }

    // --- Delegated properties and methods (unchanged semantics) ---

    public int Length => _inner.Length;
    public int ByteCount => _inner.ByteCount;
    public long Version => _inner.Version;
    public string GetText() => _inner.GetText();
    public string GetText(DocumentRange range) => _inner.GetText(range);
    public ReadOnlyMemory<byte> GetBytes() => _inner.GetBytes();
    public ReadOnlyMemory<byte> GetBytes(int byteOffset, int count) => _inner.GetBytes(byteOffset, count);
    public Utf8ByteMap GetByteMap() => _inner.GetByteMap();
    public EditResult Apply(EditOperation operation, string? source = null) => _inner.Apply(operation, source);
    public EditResult Apply(IReadOnlyList<EditOperation> operations, string? source = null) => _inner.Apply(operations, source);
    public EditResult ApplyBytes(ByteEditOperation operation, string? source = null) => _inner.ApplyBytes(operation, source);
    public void BeginBatch() => _inner.BeginBatch();
    public void EndBatch() => _inner.EndBatch();
    public DocumentDiagnosticInfo? GetDiagnosticInfo() => _inner.GetDiagnosticInfo();

    public event EventHandler<DocumentChangedEventArgs>? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    // --- Overridden: line semantics use hex rows ---

    public int LineCount => Math.Max(1, (ByteCount + _bytesPerRow - 1) / _bytesPerRow);

    public DocumentPosition OffsetToPosition(DocumentOffset offset)
    {
        if (ByteCount == 0)
            return new DocumentPosition(1, 1);

        var charPos = Math.Min(offset.Value, Length);
        var byteMap = _inner.GetByteMap();
        var byteOffset = charPos < byteMap.CharCount
            ? byteMap.CharToByteStart(charPos)
            : ByteCount;

        var row = Math.Min(byteOffset / _bytesPerRow + 1, LineCount);
        var rowStartChar = GetRowStartChar(row, byteMap);
        var col = Math.Max(1, charPos - rowStartChar + 1);
        return new DocumentPosition(row, col);
    }

    public DocumentOffset PositionToOffset(DocumentPosition position)
    {
        if (ByteCount == 0)
            return DocumentOffset.Zero;

        var line = Math.Clamp(position.Line, 1, LineCount);
        var byteMap = _inner.GetByteMap();
        var rowStartChar = GetRowStartChar(line, byteMap);
        var charOffset = rowStartChar + position.Column - 1;
        return new DocumentOffset(Math.Clamp(charOffset, 0, Length));
    }

    public string GetLineText(int line)
    {
        if (line < 1 || line > LineCount)
            throw new ArgumentOutOfRangeException(nameof(line));

        if (ByteCount == 0) return "";

        var startByte = (line - 1) * _bytesPerRow;
        if (startByte >= ByteCount) return "";

        var endByte = Math.Min(line * _bytesPerRow, ByteCount);
        var byteMap = _inner.GetByteMap();
        var startChar = startByte == 0 ? 0 : byteMap.ByteToChar(startByte).charIndex;
        var endChar = endByte < byteMap.TotalBytes
            ? byteMap.ByteToChar(endByte).charIndex
            : Length;

        return startChar < endChar ? _inner.GetText()[startChar..endChar] : "";
    }

    public int GetLineLength(int line)
    {
        if (line < 1 || line > LineCount)
            throw new ArgumentOutOfRangeException(nameof(line));

        if (ByteCount == 0) return 0;

        var startByte = (line - 1) * _bytesPerRow;
        if (startByte >= ByteCount) return 0;

        var endByte = Math.Min(line * _bytesPerRow, ByteCount);
        var byteMap = _inner.GetByteMap();
        var startChar = startByte == 0 ? 0 : byteMap.ByteToChar(startByte).charIndex;
        var endChar = endByte < byteMap.TotalBytes
            ? byteMap.ByteToChar(endByte).charIndex
            : Length;

        return Math.Max(0, endChar - startChar);
    }

    /// <summary>
    /// Returns the character index where the given hex row starts.
    /// </summary>
    private int GetRowStartChar(int row, Utf8ByteMap byteMap)
    {
        var rowStartByte = (row - 1) * _bytesPerRow;
        if (rowStartByte == 0) return 0;
        return rowStartByte < byteMap.TotalBytes
            ? byteMap.ByteToChar(rowStartByte).charIndex
            : Length;
    }
}
