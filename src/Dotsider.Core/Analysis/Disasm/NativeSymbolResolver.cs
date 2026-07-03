using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Resolves a code or data virtual address to the symbol that contains it, so the disassembler can
/// name call/branch/data targets. Returns false when no symbol covers the address. The out
/// <see cref="NativeSymbolRef.Offset"/> lets the caller render <c>Name+0x{offset}</c> for a target
/// that lands inside a symbol rather than at its start.
/// </summary>
/// <param name="virtualAddress">The target virtual address to resolve.</param>
/// <param name="symbol">The containing symbol reference when found.</param>
/// <returns>True when a symbol contains <paramref name="virtualAddress"/>; otherwise false.</returns>
public delegate bool NativeSymbolResolver(ulong virtualAddress, out NativeSymbolRef symbol);
