namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Describes one module symbol stream and the contained CodeView substream sizes declared by DBI.
/// </summary>
/// <param name="SymbolStream">The module symbol stream index, or -1 when absent.</param>
/// <param name="SymbolByteSize">The byte length of the CodeView symbol records.</param>
/// <param name="C11ByteSize">The byte length of the C11 line information.</param>
/// <param name="C13ByteSize">The byte length of the C13 line information.</param>
internal readonly record struct DbiModule(
    int SymbolStream,
    uint SymbolByteSize,
    uint C11ByteSize,
    uint C13ByteSize);
