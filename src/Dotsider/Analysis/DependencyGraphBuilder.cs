using Dotsider.Analysis.Models;

namespace Dotsider.Analysis;

/// <summary>
/// Builds a dependency graph from an assembly's references and type refs.
/// Uses a hierarchical tree layout with the root assembly at top center.
/// </summary>
public static class DependencyGraphBuilder
{
    public static (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) Build(
        AssemblyAnalyzer analyzer)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        // Count type refs per assembly for edge weights
        var typeRefCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var typeRef in analyzer.TypeRefs)
        {
            var scope = typeRef.ResolutionScope;
            if (!string.IsNullOrEmpty(scope))
            {
                typeRefCounts.TryGetValue(scope, out var count);
                typeRefCounts[scope] = count + 1;
            }
        }

        var refs = analyzer.AssemblyRefs;
        var totalNodes = refs.Count + 1;

        // Root node at top center
        nodes.Add(new GraphNode(
            analyzer.AssemblyName ?? analyzer.FileName,
            analyzer.AssemblyVersion,
            analyzer.PublicKeyToken,
            IsRoot: true,
            X: 0.5,
            Y: 0.1));

        // Child nodes evenly spaced below
        for (var i = 0; i < refs.Count; i++)
        {
            var asmRef = refs[i];
            var x = refs.Count == 1 ? 0.5 : (double)i / (refs.Count - 1);
            // Multiple rows if many refs
            var row = i / 8;
            var col = i % 8;
            var colCount = Math.Min(8, refs.Count - row * 8);

            if (refs.Count <= 8)
            {
                x = refs.Count == 1 ? 0.5 : (double)i / (refs.Count - 1);
                nodes.Add(new GraphNode(
                    asmRef.Name, asmRef.Version, asmRef.PublicKeyToken,
                    IsRoot: false, X: 0.05 + x * 0.9, Y: 0.5));
            }
            else
            {
                x = colCount == 1 ? 0.5 : (double)col / (colCount - 1);
                nodes.Add(new GraphNode(
                    asmRef.Name, asmRef.Version, asmRef.PublicKeyToken,
                    IsRoot: false,
                    X: 0.05 + x * 0.9,
                    Y: 0.4 + row * 0.2));
            }

            typeRefCounts.TryGetValue(asmRef.Name, out var refCount);
            edges.Add(new GraphEdge(
                nodes[0].Name, asmRef.Name, refCount));
        }

        return (nodes, edges);
    }
}
