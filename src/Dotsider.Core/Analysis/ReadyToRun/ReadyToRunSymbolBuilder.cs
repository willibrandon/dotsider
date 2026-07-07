using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Turns a ReadyToRun image's method map into a <see cref="NativeSymbolInfo"/> so the existing
/// native disassembly, symbol table, and target-resolution machinery light up for R2R. Each code
/// range becomes its own <see cref="NativeSymbol"/> — the hot entry keyed by the method name,
/// funclets and cold ranges suffixed — because a method body is several disjoint ranges, not one.
/// Status-gated: a corrupt/unsupported image with no usable map yields a diagnostic status rather
/// than a misleading empty symbol set.
/// </summary>
internal static class ReadyToRunSymbolBuilder
{
    /// <summary>Builds the R2R native symbol info from the method entries.</summary>
    /// <param name="methods">The precompiled method entries with their code ranges.</param>
    /// <param name="architecture">The image's architecture.</param>
    /// <param name="mapUsable">Whether the method map parsed from a valid image; false → diagnostic status.</param>
    /// <param name="diagnostic">A reason to carry when <paramref name="mapUsable"/> is false.</param>
    public static NativeSymbolInfo Build(
        IReadOnlyList<ReadyToRunMethodEntry> methods,
        NativeArchitecture architecture,
        bool mapUsable,
        string? diagnostic)
    {
        if (!mapUsable)
        {
            return new NativeSymbolInfo(
                [], NativeSymbolSource.ReadyToRun, NativeSymbolStatus.CorruptSymbolFile,
                Path: null, Diagnostic: diagnostic ?? "the ReadyToRun method map is unavailable",
                Architecture: architecture, SourceMap: null);
        }

        var symbols = new List<NativeSymbol>();
        foreach (var method in methods)
        {
            var managedName = method.DeclaringType is not null
                ? $"{method.DeclaringType}.{method.Name}{method.InstantiationDisplay}"
                : null;
            var baseName = managedName ?? $"R2R_token_{method.Token:X8}";

            var funcletIndex = 0;
            foreach (var range in method.CodeRanges)
            {
                var name = range.Kind switch
                {
                    ReadyToRunCodeRangeKind.HotEntry => baseName,
                    ReadyToRunCodeRangeKind.Funclet => $"{baseName}$funclet{++funcletIndex}",
                    _ => $"{baseName}$cold",
                };

                symbols.Add(new NativeSymbol(
                    Name: name,
                    ManagedName: managedName,
                    VirtualAddress: range.VirtualAddress,
                    Rva: (uint)range.StartRva,
                    FileOffset: range.FileOffset,
                    Section: ".text",
                    Size: range.Size,
                    Kind: NativeSymbolKind.Function,
                    SourceFile: null,
                    Line: null,
                    IsExactMatch: range.Kind == ReadyToRunCodeRangeKind.HotEntry,
                    Aliases: []));
            }
        }

        symbols.Sort(static (a, b) => a.VirtualAddress.CompareTo(b.VirtualAddress));

        var disassemblable = architecture != NativeArchitecture.Unknown;
        return new NativeSymbolInfo(
            symbols, NativeSymbolSource.ReadyToRun, NativeSymbolStatus.Loaded,
            Path: null,
            Diagnostic: disassemblable ? null : "precompiled; architecture unknown",
            Architecture: architecture,
            SourceMap: null);
    }
}
