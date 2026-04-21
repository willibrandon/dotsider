using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Builds the full transitive assembly dependency graph rooted at an analyzed assembly.
/// Performs a breadth-first walk through each assembly's <see cref="AssemblyAnalyzer.AssemblyRefs"/>,
/// resolving children by full identity, deduping on <see cref="GraphNode.Id"/>, preserving edges
/// for cycles and diamonds, and classifying unresolvable and identity-mismatched references as
/// non-expanding leaf nodes. Produces a <see cref="DependencyGraphResult"/> containing the
/// public topology plus internal navigation metadata consumed only by the TUI.
/// </summary>
public static class DependencyGraphBuilder
{
    /// <summary>
    /// Builds the transitive dependency graph rooted at <paramref name="analyzer"/>.
    /// </summary>
    /// <param name="analyzer">The root assembly analyzer. The caller retains ownership and disposal
    /// responsibility; the builder does not dispose it.</param>
    /// <returns>The computed nodes, edges, and per-node navigation metadata.</returns>
    public static DependencyGraphResult Build(AssemblyAnalyzer analyzer)
    {
        var byId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();
        var navById = new Dictionary<string, GraphNavigationContext>(StringComparer.Ordinal);
        var depthById = new Dictionary<string, int>(StringComparer.Ordinal);
        var parentOrderById = new Dictionary<string, int>(StringComparer.Ordinal);
        var resolutionCache = new Dictionary<(string ParentId, string ChildId),
            (ResolvedAssembly? Resolved, AssemblyProvenance Provenance, string? CandidateProbePath)>();

        var rootIdentity = IdentityFromAnalyzer(analyzer);
        var rootId = AssemblyIdentityFormat.Format(
            rootIdentity.Name, rootIdentity.Version, rootIdentity.Culture, rootIdentity.PublicKeyToken);

        depthById[rootId] = 0;
        parentOrderById[rootId] = 0;
        byId[rootId] = new GraphNode(
            Id: rootId,
            Name: rootIdentity.Name,
            Version: NullIfEmpty(rootIdentity.Version),
            Culture: rootIdentity.Culture,
            PublicKeyToken: rootIdentity.PublicKeyToken,
            IsRoot: true,
            Depth: 0,
            Unresolved: false);
        navById[rootId] = new GraphNavigationContext(
            Resolved: null,
            ReferencingFilePath: null,
            ReferencingBundlePath: null,
            ReferencingTargetFramework: null,
            ReferencingPreferredRuntimePack: null,
            Provenance: AssemblyProvenance.Root,
            IsFrameworkAssembly: false,
            CandidateProbePath: null);

        var queue = new Queue<(AssemblyAnalyzer Analyzer, string NodeId, bool OwnsDispose)>();
        queue.Enqueue((analyzer, rootId, OwnsDispose: false));

        var parentCounter = 1;

        while (queue.Count > 0)
        {
            var (current, currentId, ownsDispose) = queue.Dequeue();
            var currentDepth = depthById[currentId];

            try
            {
                var refs = current.HasMetadata ? current.AssemblyRefs : [];
                var typeRefs = current.HasMetadata ? current.TypeRefs : [];

                var counts = CountTypeRefsByScopeId(typeRefs);

                foreach (var asmRef in refs)
                {
                    var childId = AssemblyIdentityFormat.Format(
                        asmRef.Name, asmRef.Version, asmRef.Culture, asmRef.PublicKeyToken);

                    counts.TryGetValue(childId, out var count);

                    if (byId.TryGetValue(childId, out var existing))
                    {
                        edges.Add(new GraphEdge(currentId, childId, count));

                        if (existing.Unresolved)
                        {
                            var retry = ResolveAndQueueNew(
                                current, currentId, asmRef, childId,
                                resolutionCache, byId, navById, depthById, parentOrderById,
                                queue, ref parentCounter, currentDepth, upgrade: existing);
                            _ = retry;
                        }

                        continue;
                    }

                    edges.Add(new GraphEdge(currentId, childId, count));

                    ResolveAndQueueNew(
                        current, currentId, asmRef, childId,
                        resolutionCache, byId, navById, depthById, parentOrderById,
                        queue, ref parentCounter, currentDepth, upgrade: null);
                }
            }
            finally
            {
                if (ownsDispose)
                    current.Dispose();
            }
        }

        // Preserve sibling order from traversal so toggling view-layer filters does not
        // cause jitter in the rendered graph — the view layer keys its row packing off
        // this stable ordering.
        var orderedNodes = byId.Values
            .OrderBy(n => n.Depth)
            .ThenBy(n => parentOrderById.TryGetValue(n.Id, out var o) ? o : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        return new DependencyGraphResult(orderedNodes, edges, navById);
    }

    private static bool ResolveAndQueueNew(
        AssemblyAnalyzer parent,
        string parentId,
        AssemblyRefInfo asmRef,
        string childId,
        Dictionary<(string ParentId, string ChildId),
            (ResolvedAssembly? Resolved, AssemblyProvenance Provenance, string? CandidateProbePath)> cache,
        Dictionary<string, GraphNode> byId,
        Dictionary<string, GraphNavigationContext> navById,
        Dictionary<string, int> depthById,
        Dictionary<string, int> parentOrderById,
        Queue<(AssemblyAnalyzer Analyzer, string NodeId, bool OwnsDispose)> queue,
        ref int parentCounter,
        int parentDepth,
        GraphNode? upgrade)
    {
        if (!cache.TryGetValue((parentId, childId), out var resolution))
        {
            resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                parent.FilePath, asmRef,
                parent.TargetFramework, parent.PreferredRuntimePack, parent.SourceBundlePath);
            cache[(parentId, childId)] = resolution;
        }

        // Classify from the requested AssemblyRef identity so unresolved and identity-mismatched
        // framework refs (common when a net6-targeted package runs against net10) are still
        // filtered by the framework toggle — otherwise the filter leaves a pile of BCL leaves
        // visible and defeats its purpose on real package graphs.
        var isFramework = AssemblyAnalyzer.IsFrameworkAssembly(
            resolution.Provenance, asmRef, parent.TargetFramework, parent.PreferredRuntimePack);

        var childNav = new GraphNavigationContext(
            Resolved: resolution.Resolved,
            ReferencingFilePath: parent.FilePath,
            ReferencingBundlePath: parent.SourceBundlePath,
            ReferencingTargetFramework: parent.TargetFramework,
            ReferencingPreferredRuntimePack: parent.PreferredRuntimePack,
            Provenance: resolution.Provenance,
            IsFrameworkAssembly: isFramework,
            CandidateProbePath: resolution.CandidateProbePath);

        var childDepth = parentDepth + 1;

        if (upgrade is null)
        {
            depthById[childId] = childDepth;
            parentOrderById[childId] = parentCounter++;
            byId[childId] = new GraphNode(
                Id: childId,
                Name: asmRef.Name,
                Version: NullIfEmpty(asmRef.Version),
                Culture: string.IsNullOrEmpty(asmRef.Culture) ? "neutral" : asmRef.Culture,
                PublicKeyToken: asmRef.PublicKeyToken,
                IsRoot: false,
                Depth: childDepth,
                Unresolved: resolution.Resolved is null);
            navById[childId] = childNav;
        }
        else if (resolution.Resolved is not null)
        {
            byId[childId] = upgrade with { Unresolved = false };
            navById[childId] = childNav;
        }
        else
        {
            return false;
        }

        if (resolution.Resolved is null)
            return false;

        try
        {
            AssemblyAnalyzer childAnalyzer = resolution.Resolved switch
            {
                ResolvedAssembly.FromFile f => new AssemblyAnalyzer(f.Path),
                ResolvedAssembly.FromBundle b => new AssemblyAnalyzer(
                    b.Bytes, filePath: b.Name, sourceBundlePath: b.BundlePath, displayName: b.Name),
                _ => throw new InvalidOperationException("Unknown ResolvedAssembly variant."),
            };
            queue.Enqueue((childAnalyzer, childId, OwnsDispose: true));
            return true;
        }
        catch
        {
            byId[childId] = byId[childId] with { Unresolved = true };
            navById[childId] = childNav with
            {
                Resolved = null,
                Provenance = AssemblyProvenance.Unresolved,
            };
            return false;
        }
    }

    private static Dictionary<string, int> CountTypeRefsByScopeId(IReadOnlyList<TypeRefInfo> typeRefs)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in typeRefs)
        {
            if (string.IsNullOrEmpty(t.ResolutionScopeId)) continue;
            result.TryGetValue(t.ResolutionScopeId, out var n);
            result[t.ResolutionScopeId] = n + 1;
        }
        return result;
    }

    private static AssemblyRefInfo IdentityFromAnalyzer(AssemblyAnalyzer analyzer)
    {
        var name = analyzer.AssemblyName ?? analyzer.FileName;
        var version = analyzer.AssemblyVersion ?? string.Empty;
        var culture = string.IsNullOrEmpty(analyzer.Culture) ? "neutral" : analyzer.Culture!;
        return new AssemblyRefInfo(name, version, culture, analyzer.PublicKeyToken);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

}
