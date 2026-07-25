namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Describes a function or data symbol recovered from one CodeView module stream.
/// </summary>
/// <param name="Name">The raw symbol name.</param>
/// <param name="Segment">The one-based section index.</param>
/// <param name="Offset">The offset within the section.</param>
/// <param name="Size">The code or data size.</param>
/// <param name="IsData">Whether the symbol represents data.</param>
/// <param name="SourceFile">The source file recovered from C13 line data.</param>
/// <param name="Line">The first source line recovered from C13 line data.</param>
internal readonly record struct CodeViewModuleSymbol(
    string Name,
    int Segment,
    uint Offset,
    uint Size,
    bool IsData,
    string? SourceFile,
    int? Line);
