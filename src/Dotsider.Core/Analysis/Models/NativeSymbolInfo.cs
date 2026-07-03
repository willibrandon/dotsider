namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The native symbols recovered from a binary, plus the provenance and status needed to explain
/// the result. Symbols are ordered by <see cref="NativeSymbol.VirtualAddress"/>, which
/// <see cref="TryFindByAddress"/> relies on to resolve an address to its containing symbol —
/// the lookup the disassembly and hex views use to name code.
/// </summary>
/// <param name="Symbols">The recovered symbols, sorted ascending by virtual address.</param>
/// <param name="Source">Which reader produced the symbols.</param>
/// <param name="Status">The probe outcome; explains an empty result.</param>
/// <param name="Path">The symbol file that was read (PDB, .dbg, or dSYM inner file), or null for self/fallback sources.</param>
/// <param name="Diagnostic">A human-readable note on the outcome — the mismatch detail, the fallback reason, or null on a clean load.</param>
public sealed record NativeSymbolInfo(
    IReadOnlyList<NativeSymbol> Symbols,
    NativeSymbolSource Source,
    NativeSymbolStatus Status,
    string? Path,
    string? Diagnostic)
{
    /// <summary>
    /// Finds the symbol whose range contains <paramref name="virtualAddress"/>. Binary-searches
    /// the address-sorted list for the last symbol starting at or before the address, then
    /// confirms the address falls within that symbol's size.
    /// </summary>
    /// <param name="virtualAddress">The virtual address to resolve.</param>
    /// <param name="symbol">The containing symbol when found.</param>
    /// <returns>True when a symbol contains the address; otherwise false.</returns>
    public bool TryFindByAddress(ulong virtualAddress, out NativeSymbol symbol)
    {
        var lo = 0;
        var hi = Symbols.Count - 1;
        var candidate = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (Symbols[mid].VirtualAddress <= virtualAddress)
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
            var found = Symbols[candidate];
            // A zero-size symbol (unsized public/data) matches only its exact start; a sized
            // symbol matches its half-open [start, start+size) range.
            var end = found.VirtualAddress + (found.Size > 0 ? (ulong)found.Size : 1);
            if (virtualAddress < end)
            {
                symbol = found;
                return true;
            }
        }

        symbol = null!;
        return false;
    }
}
