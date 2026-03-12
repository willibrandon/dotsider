namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the assembly dependency graph.
/// </summary>
/// <param name="Name">Assembly name.</param>
/// <param name="Version">Assembly version string, or <see langword="null"/> if unavailable.</param>
/// <param name="PublicKeyToken">Public key token hex string, or <see langword="null"/>.</param>
/// <param name="IsRoot">Whether this is the root (analyzed) assembly.</param>
/// <param name="X">X coordinate for graph layout rendering.</param>
/// <param name="Y">Y coordinate for graph layout rendering.</param>
public sealed record GraphNode(
    string Name,
    string? Version,
    string? PublicKeyToken,
    bool IsRoot,
    double X,
    double Y);
