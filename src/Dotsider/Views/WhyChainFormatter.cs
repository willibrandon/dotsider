using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// Formats "why is this in my binary" dependency chains for popup display: the root kept
/// step 2, step 2 kept step 3, and so on down to the node that was asked about. Shared by the
/// Size Map and the size-diff treemap so both render one answer the same way.
/// </summary>
public static class WhyChainFormatter
{
    /// <summary>How many chains a multi-node aggregate renders before summarizing the rest.</summary>
    private const int MaxChains = 3;

    /// <summary>
    /// Formats the root-to-node chain for one dependency-graph node name.
    /// </summary>
    /// <param name="dgml">The ILC dependency graph.</param>
    /// <param name="displayPath">The entry's display path, shown as the popup heading.</param>
    /// <param name="nodeName">The dependency-graph node name to explain.</param>
    /// <returns>The formatted chain, or an explanation when the node is not in the graph.</returns>
    public static string FormatWhyChain(DgmlGraph dgml, string displayPath, string nodeName)
    {
        var path = dgml.PathToRoot(nodeName);
        if (path.Count == 0)
        {
            return $"{displayPath}\n\nNot present in the DGML dependency graph. The scan\ngraph can differ from the compiled output; publish\nwith the codegen graph next to the binary for an\nexact join.";
        }

        var lines = new List<string>
        {
            displayPath,
            "",
            "Kept by (root first):",
            "",
        };
        AppendChain(lines, path);
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Formats chains for an aggregated entry that maps to several dependency-graph nodes
    /// (grouped overloads, owner-grouped frozen objects). Renders up to three chains and
    /// summarizes the remainder, so the answer never pretends an aggregate is one node.
    /// </summary>
    /// <param name="dgml">The ILC dependency graph.</param>
    /// <param name="displayPath">The entry's display path, shown as the popup heading.</param>
    /// <param name="nodeNames">Every node name behind the entry.</param>
    /// <returns>The formatted chains, or an explanation when none resolve.</returns>
    public static string FormatWhyChains(
        DgmlGraph dgml, string displayPath, IReadOnlyList<string> nodeNames)
    {
        if (nodeNames.Count == 0)
        {
            return $"{displayPath}\n\nNo dependency-graph node names recorded for this entry\n(format 1.x mstat, or names stripped). Publish with a\n2.0+ SDK to join sizes to dependency chains.";
        }

        if (nodeNames.Count == 1)
            return FormatWhyChain(dgml, displayPath, nodeNames[0]);

        var lines = new List<string>
        {
            displayPath,
            $"({nodeNames.Count} aggregated nodes)",
            "",
        };

        var shown = Math.Min(MaxChains, nodeNames.Count);
        for (var n = 0; n < shown; n++)
        {
            lines.Add($"— {nodeNames[n]} — kept by (root first):");
            var path = dgml.PathToRoot(nodeNames[n]);
            if (path.Count == 0)
                lines.Add("     not present in the DGML dependency graph");
            else
                AppendChain(lines, path);
            lines.Add("");
        }

        if (nodeNames.Count > shown)
            lines.Add($"({nodeNames.Count - shown} more nodes not shown)");

        return string.Join('\n', lines);
    }

    private static void AppendChain(List<string> lines, IReadOnlyList<DgmlPathStep> path)
    {
        for (var i = 0; i < path.Count; i++)
        {
            lines.Add($"{i + 1,3}. {path[i].Label}");
            if (path[i].Reason is { } reason)
                lines.Add($"     reason: {reason}");
        }
    }
}
