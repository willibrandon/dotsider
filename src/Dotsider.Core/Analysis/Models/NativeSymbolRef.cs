namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A resolved reference to the symbol containing a target address, with the offset into it — so a
/// call or branch landing inside a function displays honestly as <c>Foo+0x12</c> rather than
/// failing or pretending an exact hit.
/// </summary>
/// <param name="Start">The containing symbol's start virtual address.</param>
/// <param name="Name">The symbol's display name (managed name where available, else the raw name).</param>
/// <param name="Kind">The symbol's kind.</param>
/// <param name="Offset">The target's byte offset from <paramref name="Start"/> (0 when it is the symbol's entry).</param>
public readonly record struct NativeSymbolRef(
    ulong Start,
    string Name,
    NativeSymbolKind Kind,
    long Offset);
