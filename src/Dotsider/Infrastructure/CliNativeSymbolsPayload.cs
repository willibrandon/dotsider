using Dotsider.Core.Analysis.Models;

namespace Dotsider.Infrastructure;

/// <summary>
/// Native symbol details written by the CLI.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliNativeSymbolsPayload(
    NativeSymbolSource Source,
    NativeSymbolStatus Status,
    string? Path,
    string? Diagnostic,
    int Count,
    IReadOnlyList<NativeSymbol> Symbols);
