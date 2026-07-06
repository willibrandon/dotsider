namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Entry counts for one node kind in a size diff, split by direction. Grown and shrunk are
/// the two signs of a changed entry; unchanged entries are counted here but never appear in
/// the delta tree.
/// </summary>
/// <param name="Kind">The node kind the counts describe.</param>
/// <param name="Added">Entries present only in the comparison build.</param>
/// <param name="Removed">Entries present only in the baseline build.</param>
/// <param name="Grown">Entries present in both builds whose size increased.</param>
/// <param name="Shrunk">Entries present in both builds whose size decreased.</param>
/// <param name="Unchanged">Entries present in both builds at the same size.</param>
public sealed record SizeDiffKindCounts(
    SizeNodeKind Kind,
    int Added,
    int Removed,
    int Grown,
    int Shrunk,
    int Unchanged);
