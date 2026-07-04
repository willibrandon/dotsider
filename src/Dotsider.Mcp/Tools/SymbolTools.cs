using System.Text.Json;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for native symbols: function names, addresses, and sizes read from a Native AOT
/// binary's PDB, DWARF, or dSYM — or unwind-data boundaries when no symbol file exists.
/// </summary>
[McpServerToolType]
public sealed partial class SymbolTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets a binary's native symbols with their provenance (source, status, symbol file path).
    /// </summary>
    /// <param name="assemblyPath">Path to the binary file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the symbol list and its provenance, or an error for managed assemblies.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetNativeSymbols(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return analyzer.NativeSymbols is { } info
                ? JsonSerializer.Serialize(info, DotsiderJsonOptions.Default)
                : "Error: managed assembly; no native symbols to read.";
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-native-symbols" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Disassembles one native function to real assembly, resolving call/branch/data targets to
    /// names. Identify the function by <paramref name="symbolName"/> (managed name, raw name, or
    /// suffix) or <paramref name="address"/> (hex <c>0x…</c> or decimal). An ambiguous name lists the
    /// candidate addresses instead of picking one.
    /// </summary>
    /// <param name="symbolName">The function name to disassemble, or null when using an address.</param>
    /// <param name="address">The function's virtual address, or null when using a name.</param>
    /// <param name="assemblyPath">Path to the binary file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the symbol, architecture, and decoded instructions, or an error.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetNativeDisassembly(
        string? symbolName = null,
        string? address = null,
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        var target = address ?? symbolName;

        if (assemblyPath is not null)
        {
            if (string.IsNullOrEmpty(target))
                return "Error: symbolName or address is required.";

            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            if (analyzer.NativeSymbols is not { } info || info.Symbols.Count == 0)
                return "Error: managed assembly; no native symbols to disassemble.";

            var matches = NativeDisassembler.FindExecutableSymbols(info, target);
            if (matches.Count == 0)
                return $"Error: No native symbol matches '{target}'.";
            if (matches.Count > 1)
            {
                var candidates = matches.OrderBy(m => m.VirtualAddress)
                    .Select(m => new { Address = $"0x{m.VirtualAddress:x}", Name = m.ManagedName ?? m.Name });
                return JsonSerializer.Serialize(new { Error = "ambiguous", Target = target, Candidates = candidates },
                    DotsiderJsonOptions.Default);
            }

            var result = NativeDisassembler.DisassembleSymbol(analyzer, matches[0]);
            if (result is null)
                return $"Error: '{matches[0].ManagedName ?? matches[0].Name}' has no disassemblable bytes.";

            return JsonSerializer.Serialize(
                new { Symbol = matches[0].ManagedName ?? matches[0].Name, analyzer.Architecture, result.Value.Instructions },
                DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value).SendAndUnwrapAsync(
                new DotsiderRequest { Method = "disassemble-native", SymbolName = symbolName, SymbolAddress = address }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
