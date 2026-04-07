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
}
