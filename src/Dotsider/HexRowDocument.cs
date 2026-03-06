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
/// <remarks>
/// Initializes a new instance wrapping the specified document.
/// </remarks>
/// <param name="inner">The underlying document to wrap.</param>
public sealed class HexRowDocument(IHex1bDocument inner) : IHex1bDocument
{
    private readonly IHex1bDocument _inner = inner;
    private int _bytesPerRow = 16;

    /// <summary>
    /// Number of bytes per hex row. Updated by the renderer when viewport width changes.
    /// </summary>
    public int BytesPerRow
    {
        get => _bytesPerRow;
        set => _bytesPerRow = Math.Max(1, value);
    }

    // --- Delegated properties and methods (unchanged semantics) ---

    /// <inheritdoc />
    public int Length => _inner.Length;

    /// <inheritdoc />
    public int ByteCount => _inner.ByteCount;

    /// <inheritdoc />
    public long Version => _inner.Version;

    /// <inheritdoc />
    public string GetText() => _inner.GetText();

    /// <inheritdoc />
    public string GetText(DocumentRange range) => _inner.GetText(range);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetBytes() => _inner.GetBytes();

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetBytes(int byteOffset, int count) => _inner.GetBytes(byteOffset, count);

    /// <inheritdoc />
    public Utf8ByteMap GetByteMap() => _inner.GetByteMap();

    /// <inheritdoc />
    public EditResult Apply(EditOperation operation, string? source = null) => _inner.Apply(operation, source);

    /// <inheritdoc />
    public EditResult Apply(IReadOnlyList<EditOperation> operations, string? source = null) => _inner.Apply(operations, source);

    /// <inheritdoc />
    public EditResult ApplyBytes(ByteEditOperation operation, string? source = null) => _inner.ApplyBytes(operation, source);

    /// <inheritdoc />
    public void BeginBatch() => _inner.BeginBatch();

    /// <inheritdoc />
    public void EndBatch() => _inner.EndBatch();

    /// <inheritdoc />
    public DocumentDiagnosticInfo? GetDiagnosticInfo() => _inner.GetDiagnosticInfo();

    /// <inheritdoc />
    public event EventHandler<DocumentChangedEventArgs>? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    // --- Overridden: line semantics use hex rows ---

    /// <summary>
    /// Gets the total number of hex rows, computed from <see cref="ByteCount"/> and <see cref="BytesPerRow"/>.
    /// </summary>
    public int LineCount => Math.Max(1, (ByteCount + _bytesPerRow - 1) / _bytesPerRow);

    /// <summary>
    /// Converts a character offset into a row/column position based on hex row layout.
    /// </summary>
    /// <param name="offset">The character offset within the document.</param>
    /// <returns>A <see cref="DocumentPosition"/> with 1-based line and column.</returns>
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

    /// <summary>
    /// Converts a row/column position back into a character offset.
    /// </summary>
    /// <param name="position">A 1-based line and column position.</param>
    /// <returns>The corresponding <see cref="DocumentOffset"/>.</returns>
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

    /// <summary>
    /// Returns the text content of the specified hex row.
    /// </summary>
    /// <param name="line">The 1-based row number.</param>
    /// <returns>The text spanning the bytes in the given row.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> is outside <c>1..<see cref="LineCount"/></c>.</exception>
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

    /// <summary>
    /// Returns the character length of the specified hex row.
    /// </summary>
    /// <param name="line">The 1-based row number.</param>
    /// <returns>The number of characters in the given row.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> is outside <c>1..<see cref="LineCount"/></c>.</exception>
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
