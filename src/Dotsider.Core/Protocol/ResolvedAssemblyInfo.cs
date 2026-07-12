namespace Dotsider.Core.Protocol;

/// <summary>
/// Serialization-safe representation of an assembly resolution result.
/// Used in protocol and MCP responses where <see cref="Analysis.Models.ResolvedAssembly"/>
/// cannot be serialized directly because bundle and module results contain raw bytes.
/// </summary>
/// <param name="Kind">Resolution kind: "file", "bundle", or "module".</param>
/// <param name="Path">Full file path for file- and module-backed results, or null for bundle-backed.</param>
/// <param name="Name">Entry name for bundle-backed results (e.g. "System.Runtime.dll"), or null.</param>
/// <param name="BundlePath">Path to the containing bundle for bundle-backed results, or null.</param>
public sealed record ResolvedAssemblyInfo(
    string Kind,
    string? Path,
    string? Name,
    string? BundlePath);
