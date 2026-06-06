namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A source sequence point decoded from a portable PDB.
/// </summary>
/// <param name="Offset">The IL offset where the sequence point starts.</param>
/// <param name="Document">The source document path.</param>
/// <param name="StartLine">The source start line.</param>
/// <param name="StartColumn">The source start column.</param>
/// <param name="EndLine">The source end line.</param>
/// <param name="EndColumn">The source end column.</param>
/// <param name="IsHidden">Whether the sequence point is hidden.</param>
/// <param name="SourceLinkUrl">The Source Link URL resolved for the document, or null.</param>
/// <param name="HasEmbeddedSource">Whether the document has embedded source.</param>
public sealed record SequencePointInfo(
    int Offset,
    string? Document,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    bool IsHidden,
    string? SourceLinkUrl,
    bool HasEmbeddedSource);
