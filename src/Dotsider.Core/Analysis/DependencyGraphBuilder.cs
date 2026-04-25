using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Builds the full transitive assembly dependency graph rooted at an analyzed assembly.
/// Performs a breadth-first walk through each assembly's <see cref="AssemblyAnalyzer.AssemblyRefs"/>,
/// resolving children by full identity, deduping on <see cref="GraphNode.Id"/>, preserving edges
/// for cycles and diamonds, and classifying unresolvable and identity-mismatched references as
/// non-expanding leaf nodes. For .NET Framework roots the resolution routes through
/// <see cref="NetFxBinder"/> so that nodes are keyed on the *bound* identity (post-redirect),
/// collapsing two distinct requested versions onto a single graph node when policy redirects them
/// to the same loaded version. Produces a <see cref="DependencyGraphResult"/> containing the
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
        var resolutionCache = new Dictionary<(string ParentId, string ChildId), AssemblyResolution>();

        // The root's NetFxBindingContext is shared by every subsequent bind in the graph,
        // matching the CLR's app-domain-wide binding policy semantics.
        var netFxContext = NetFxBindingContext.TryBuild(analyzer);

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
                    var requestedId = AssemblyIdentityFormat.Format(
                        asmRef.Name, asmRef.Version, asmRef.Culture, asmRef.PublicKeyToken);

                    // Resolve first so that net48 nodes get keyed on the bound (post-redirect)
                    // identity instead of the requested one. Two parents whose distinct requested
                    // versions both redirect to the same loaded version therefore land on a single
                    // graph node, while each edge still records its own requested identity below.
                    var resolution = ResolveOnce(current, currentId, asmRef, requestedId,
                        netFxContext, resolutionCache);
                    var (childId, childIdentity) = SelectChildId(asmRef, resolution, requestedId);

                    counts.TryGetValue(requestedId, out var count);
                    var requestedDifferent = !string.Equals(childId, requestedId, StringComparison.Ordinal);

                    if (byId.TryGetValue(childId, out var existing))
                    {
                        edges.Add(new GraphEdge(
                            currentId, childId, count,
                            RequestedIdentity: requestedDifferent ? asmRef : null));

                        if (existing.Unresolved && resolution.Resolved is not null)
                        {
                            UpgradeAndQueue(
                                current, asmRef, childId, resolution,
                                byId, navById, queue, existing);
                        }

                        continue;
                    }

                    edges.Add(new GraphEdge(
                        currentId, childId, count,
                        RequestedIdentity: requestedDifferent ? asmRef : null));

                    AddNewNodeAndQueue(
                        current, asmRef, childId, childIdentity, resolution,
                        byId, navById, depthById, parentOrderById,
                        queue, ref parentCounter, currentDepth);
                }
            }
            finally
            {
                if (ownsDispose)
                    current.Dispose();
            }
        }

        var orderedNodes = byId.Values
            .OrderBy(n => n.Depth)
            .ThenBy(n => parentOrderById.TryGetValue(n.Id, out var o) ? o : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        return new DependencyGraphResult(orderedNodes, edges, navById);
    }

    private static AssemblyResolution ResolveOnce(
        AssemblyAnalyzer parent,
        string parentId,
        AssemblyRefInfo asmRef,
        string requestedId,
        NetFxBindingContext? netFxContext,
        Dictionary<(string ParentId, string ChildId), AssemblyResolution> cache)
    {
        if (cache.TryGetValue((parentId, requestedId), out var cached)) return cached;
        var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
            parent.FilePath, asmRef,
            parent.TargetFramework, parent.PreferredRuntimePack, parent.SourceBundlePath,
            netFxContext);
        cache[(parentId, requestedId)] = resolution;
        return resolution;
    }

    private static (string ChildId, AssemblyRefInfo Identity) SelectChildId(
        AssemblyRefInfo asmRef, AssemblyResolution resolution, string requestedId)
    {
        // Net48 success cases carry a LoadedIdentity that may differ from the requested identity
        // (binding redirect collapsed two requested versions onto one bound version). In that
        // case key the node on the bound identity so both edges land on the same node.
        if (resolution.LoadedIdentity is { } loaded)
        {
            var boundId = AssemblyIdentityFormat.Format(
                loaded.Name, loaded.Version, loaded.Culture, loaded.PublicKeyToken);
            return (boundId, loaded);
        }
        return (requestedId, asmRef);
    }

    private static void AddNewNodeAndQueue(
        AssemblyAnalyzer parent,
        AssemblyRefInfo asmRef,
        string childId,
        AssemblyRefInfo childIdentity,
        AssemblyResolution resolution,
        Dictionary<string, GraphNode> byId,
        Dictionary<string, GraphNavigationContext> navById,
        Dictionary<string, int> depthById,
        Dictionary<string, int> parentOrderById,
        Queue<(AssemblyAnalyzer Analyzer, string NodeId, bool OwnsDispose)> queue,
        ref int parentCounter,
        int parentDepth)
    {
        var childDepth = parentDepth + 1;
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
            CandidateProbePath: resolution.CandidateProbePath,
            AppliedPolicy: resolution.AppliedPolicy,
            LoadedIdentity: resolution.LoadedIdentity);

        depthById[childId] = childDepth;
        parentOrderById[childId] = parentCounter++;
        byId[childId] = new GraphNode(
            Id: childId,
            Name: childIdentity.Name,
            Version: NullIfEmpty(childIdentity.Version),
            Culture: string.IsNullOrEmpty(childIdentity.Culture) ? "neutral" : childIdentity.Culture,
            PublicKeyToken: childIdentity.PublicKeyToken,
            IsRoot: false,
            Depth: childDepth,
            Unresolved: resolution.Resolved is null);
        navById[childId] = childNav;

        EnqueueChildAnalyzerIfResolved(resolution, childId, byId, navById, queue, childNav);
    }

    private static void UpgradeAndQueue(
        AssemblyAnalyzer parent,
        AssemblyRefInfo asmRef,
        string childId,
        AssemblyResolution resolution,
        Dictionary<string, GraphNode> byId,
        Dictionary<string, GraphNavigationContext> navById,
        Queue<(AssemblyAnalyzer Analyzer, string NodeId, bool OwnsDispose)> queue,
        GraphNode previousUnresolvedNode)
    {
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
            CandidateProbePath: resolution.CandidateProbePath,
            AppliedPolicy: resolution.AppliedPolicy,
            LoadedIdentity: resolution.LoadedIdentity);

        byId[childId] = previousUnresolvedNode with { Unresolved = false };
        navById[childId] = childNav;

        EnqueueChildAnalyzerIfResolved(resolution, childId, byId, navById, queue, childNav);
    }

    private static void EnqueueChildAnalyzerIfResolved(
        AssemblyResolution resolution,
        string childId,
        Dictionary<string, GraphNode> byId,
        Dictionary<string, GraphNavigationContext> navById,
        Queue<(AssemblyAnalyzer Analyzer, string NodeId, bool OwnsDispose)> queue,
        GraphNavigationContext childNav)
    {
        if (resolution.Resolved is null) return;
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
        }
        catch
        {
            byId[childId] = byId[childId] with { Unresolved = true };
            navById[childId] = childNav with
            {
                Resolved = null,
                Provenance = AssemblyProvenance.Unresolved,
            };
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
