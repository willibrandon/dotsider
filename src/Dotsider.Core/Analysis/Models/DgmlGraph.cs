namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// An ILC dependency graph read from a DGML file, with the reverse index needed to answer
/// "why is this in my binary": a breadth-first walk from any node toward its dependers ends
/// at a root — a node nothing depends on — and the chain back down is the explanation.
/// </summary>
public sealed class DgmlGraph
{
    private readonly Dictionary<int, int> _indexById;
    private readonly Dictionary<string, int> _indexByLabel;
    private readonly int[] _incomingStarts;
    private readonly int[] _incomingLinks;

    internal DgmlGraph(IReadOnlyList<DgmlNode> nodes, IReadOnlyList<DgmlLink> links)
    {
        Nodes = nodes;
        Links = links;

        _indexById = new Dictionary<int, int>(nodes.Count);
        _indexByLabel = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            _indexById.TryAdd(nodes[i].Id, i);
            _indexByLabel.TryAdd(nodes[i].Label, i);
        }

        // CSR-style reverse adjacency: for each node, the indices of its incoming links.
        // Links with endpoints that resolve to no node are kept in Links but not indexed.
        var counts = new int[nodes.Count + 1];
        foreach (var link in links)
        {
            if (_indexById.TryGetValue(link.TargetId, out var target) && _indexById.ContainsKey(link.SourceId))
                counts[target + 1]++;
        }

        for (var i = 1; i < counts.Length; i++)
            counts[i] += counts[i - 1];

        _incomingStarts = counts;
        _incomingLinks = new int[counts[^1]];
        var cursor = new int[nodes.Count];
        for (var i = 0; i < links.Count; i++)
        {
            if (_indexById.TryGetValue(links[i].TargetId, out var target) && _indexById.ContainsKey(links[i].SourceId))
                _incomingLinks[_incomingStarts[target] + cursor[target]++] = i;
        }
    }

    /// <summary>The graph's nodes.</summary>
    public IReadOnlyList<DgmlNode> Nodes { get; }

    /// <summary>The graph's edges; each source depends on its target.</summary>
    public IReadOnlyList<DgmlLink> Links { get; }

    /// <summary>
    /// Finds the node with the given label, or null when no node carries it. When labels
    /// repeat, the first node wins.
    /// </summary>
    /// <param name="label">The node name to look up — for an mstat entry, its <c>NodeName</c>.</param>
    public DgmlNode? FindNodeByLabel(string label) =>
        _indexByLabel.TryGetValue(label, out var index) ? Nodes[index] : null;

    /// <summary>
    /// Walks from the labeled node to a root and returns the chain root-first, ending at the
    /// queried node. Empty when the label is unknown.
    /// </summary>
    /// <param name="label">The node name to explain.</param>
    public IReadOnlyList<DgmlPathStep> PathToRoot(string label) =>
        _indexByLabel.TryGetValue(label, out var index) ? PathToRootCore(index) : [];

    /// <summary>
    /// Walks from the node to a root and returns the chain root-first, ending at the queried
    /// node. Empty when the id is unknown.
    /// </summary>
    /// <param name="nodeId">The node id to explain.</param>
    public IReadOnlyList<DgmlPathStep> PathToRoot(int nodeId) =>
        _indexById.TryGetValue(nodeId, out var index) ? PathToRootCore(index) : [];

    private List<DgmlPathStep> PathToRootCore(int start)
    {
        // BFS toward dependers, so the first root reached gives a shortest chain. next[n]
        // remembers which node n leads back down to, and via which link, for reconstruction.
        var visited = new HashSet<int> { start };
        var next = new Dictionary<int, (int Child, int Link)>();
        var queue = new Queue<int>();
        queue.Enqueue(start);

        var root = -1;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (InDegree(node) == 0)
            {
                root = node;
                break;
            }

            for (var i = _incomingStarts[node]; i < _incomingStarts[node + 1]; i++)
            {
                var link = _incomingLinks[i];
                var depender = _indexById[Links[link].SourceId];
                if (!visited.Add(depender)) continue;
                next[depender] = (node, link);
                queue.Enqueue(depender);
            }
        }

        if (root < 0)
        {
            // Every depender chain loops back on itself; report the node alone.
            return [new DgmlPathStep(Nodes[start].Label, null)];
        }

        var steps = new List<DgmlPathStep> { new(Nodes[root].Label, null) };
        var current = root;
        while (current != start)
        {
            var (child, link) = next[current];
            steps.Add(new DgmlPathStep(Nodes[child].Label, NormalizeReason(Links[link].Reason)));
            current = child;
        }

        return steps;
    }

    private int InDegree(int node) => _incomingStarts[node + 1] - _incomingStarts[node];

    private static string? NormalizeReason(string? reason) =>
        string.IsNullOrEmpty(reason) ? null : reason;
}
