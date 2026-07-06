using System.Text.Json.Serialization;

namespace Dotsider.Core.Protocol;

/// <summary>
/// JSON request sent to a dotsider diagnostics socket.
/// </summary>
public sealed class DotsiderRequest
{
    /// <summary>Protocol version. Must match <see cref="DotsiderProtocol.Version"/>.</summary>
    [JsonRequired]
    public int V { get; set; } = DotsiderProtocol.Version;

    /// <summary>The method to invoke (e.g. "assembly-info", "list-types", "disassemble").</summary>
    public string Method { get; set; } = "";

    /// <summary>Path to an assembly file (for direct analysis or diff).</summary>
    public string? AssemblyPath { get; set; }

    /// <summary>Full or partial type name for filtering.</summary>
    public string? TypeName { get; set; }

    /// <summary>Full or partial method name for disassembly or filtering.</summary>
    public string? MethodName { get; set; }

    /// <summary>Search query for find-members or search-il-opcodes.</summary>
    public string? Query { get; set; }

    /// <summary>Target name, path, key, or node label for Native AOT size explanation tools.</summary>
    public string? Target { get; set; }

    /// <summary>Metadata token for resolve-token.</summary>
    public int? Token { get; set; }

    /// <summary>Byte offset for read-bytes.</summary>
    public long? Offset { get; set; }

    /// <summary>Byte count for read-bytes.</summary>
    public int? Length { get; set; }

    /// <summary>Left assembly path for diff.</summary>
    public string? LeftPath { get; set; }

    /// <summary>Right assembly path for diff.</summary>
    public string? RightPath { get; set; }

    /// <summary>Maximum number of results to return.</summary>
    public int? MaxResults { get; set; }

    /// <summary>Tab identifier for navigation.</summary>
    public int? TabId { get; set; }

    /// <summary>Trace event category filter.</summary>
    public string? CategoryFilter { get; set; }

    /// <summary>Command-line arguments for starting a trace.</summary>
    public string? Arguments { get; set; }

    /// <summary>Minimum string length for raw string extraction.</summary>
    public int? MinLength { get; set; }

    /// <summary>Assembly name to resolve (e.g. "System.Runtime"), used by resolve-assembly and push-assembly.</summary>
    public string? AssemblyName { get; set; }

    /// <summary>Namespace filter for Native AOT size contributor tools.</summary>
    public string? NamespaceName { get; set; }

    /// <summary>mstat section filter for Native AOT size contributor tools.</summary>
    public string? Section { get; set; }

    /// <summary>Whether IL responses should include portable PDB debug information.</summary>
    public bool IncludeDebugInfo { get; set; }

    /// <summary>Whether member search includes compiler-generated members.</summary>
    public bool IncludeCompilerGenerated { get; set; }

    /// <summary>Native symbol name for disassemble-native (managed name, raw name, or suffix).</summary>
    public string? SymbolName { get; set; }

    /// <summary>Native symbol virtual address (hex <c>0x…</c> or decimal) for disassemble-native.</summary>
    public string? SymbolAddress { get; set; }

    /// <summary>Method name (optionally <c>Type.Method</c>) or <c>0x…</c> native address for correlate-method.</summary>
    public string? MethodOrAddress { get; set; }

    /// <summary>Baseline binary or mstat path for check-size-budgets.</summary>
    public string? BaselinePath { get; set; }

    /// <summary>Budget spec strings for check-size-budgets, in the size-budget grammar.</summary>
    public string[]? Budgets { get; set; }

    /// <summary>An inline size-budget JSON document for check-size-budgets (the budget-file schema).</summary>
    public string? BudgetsJson { get; set; }

    /// <summary>Path to a size-budget JSON file for check-size-budgets.</summary>
    public string? BudgetFilePath { get; set; }

    /// <summary>How many top contributors diff-size and check-size-budgets responses carry.</summary>
    public int? TopN { get; set; }

    /// <summary>Whether Native AOT contributor responses should include DGML why chains.</summary>
    public bool IncludeWhy { get; set; }

    /// <summary>Maximum ambiguous candidates returned by Native AOT explanation tools.</summary>
    public int? MaxCandidates { get; set; }

    /// <summary>Maximum DGML chains returned for one aggregated Native AOT contributor.</summary>
    public int? MaxWhyChains { get; set; }

    /// <summary>Whether a diff-size response includes the delta tree.</summary>
    public bool IncludeTree { get; set; }

    /// <summary>The delta-tree node cap for diff-size when <see cref="IncludeTree"/> is set.</summary>
    public int? MaxNodes { get; set; }
}
