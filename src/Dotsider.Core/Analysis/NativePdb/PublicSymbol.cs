namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>Describes a public symbol before its segment and offset are resolved to an RVA.</summary>
/// <param name="Name">The raw symbol name.</param>
/// <param name="Segment">The one-based section index.</param>
/// <param name="Offset">The offset within the section.</param>
/// <param name="IsFunction">Whether the record carries the function flag.</param>
internal readonly record struct PublicSymbol(
    string Name,
    int Segment,
    uint Offset,
    bool IsFunction);
