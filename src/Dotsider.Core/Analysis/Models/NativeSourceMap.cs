namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// An address-sorted map from virtual address to source file and line, aggregated from a native
/// binary's debug sidecar. <see cref="TryGetLine"/> resolves an instruction address to its source
/// location the way <see cref="NativeSymbolInfo.TryFindByAddress"/> resolves an address to a symbol,
/// letting the disassembler annotate the listing with <c>// file:line</c> where the sidecar has data.
/// </summary>
/// <param name="Lines">The source rows, sorted ascending by <see cref="NativeSourceLine.Address"/>.</param>
public sealed record NativeSourceMap(IReadOnlyList<NativeSourceLine> Lines)
{
    /// <summary>Resolves a virtual address to its source file and 1-based line, if mapped.</summary>
    /// <param name="virtualAddress">The instruction virtual address.</param>
    /// <param name="file">The source file when found.</param>
    /// <param name="line">The 1-based source line when found.</param>
    /// <returns>True when the address falls within a mapped row; otherwise false.</returns>
    public bool TryGetLine(ulong virtualAddress, out string file, out int line)
    {
        var lo = 0;
        var hi = Lines.Count - 1;
        var candidate = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (Lines[mid].Address <= virtualAddress)
            {
                candidate = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (candidate >= 0)
        {
            var found = Lines[candidate];
            var end = found.Address + (found.Length > 0 ? found.Length : 1u);
            if (virtualAddress < end)
            {
                file = found.File;
                line = found.Line;
                return true;
            }
        }

        file = string.Empty;
        line = 0;
        return false;
    }
}
