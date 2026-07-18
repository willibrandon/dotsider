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
    public static DependencyGraphResult Build(AssemblyAnalyzer analyzer) =>
        BuildWithCancellation(analyzer, CancellationToken.None);

    /// <summary>
    /// Builds the transitive dependency graph and cooperatively stops at traversal boundaries when
    /// cancellation is requested.
    /// </summary>
    /// <param name="analyzer">
    /// The root assembly analyzer. The caller retains ownership and disposal responsibility.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel graph traversal.</param>
    /// <returns>The computed nodes, edges, and per-node navigation metadata.</returns>
    internal static DependencyGraphResult BuildWithCancellation(
        AssemblyAnalyzer analyzer,
        CancellationToken cancellationToken) =>
        BuildWithCancellation(analyzer, cancellationToken, progressObserver: null);

    /// <summary>
    /// Builds the transitive dependency graph, cooperatively stops when cancellation is requested,
    /// and reports completed traversal units to <paramref name="progressObserver"/>.
    /// </summary>
    /// <param name="analyzer">
    /// The root assembly analyzer. The caller retains ownership and disposal responsibility.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel graph traversal.</param>
    /// <param name="progressObserver">
    /// An optional observer invoked synchronously after each reported unit of work completes.
    /// </param>
    /// <returns>The computed nodes, edges, and per-node navigation metadata.</returns>
    internal static DependencyGraphResult BuildWithCancellation(
        AssemblyAnalyzer analyzer,
        CancellationToken cancellationToken,
        Action<DependencyGraphBuildCheckpoint>? progressObserver)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (analyzer.BinaryKind == BinaryKind.NativeAot)
            return BuildForNativeAot(analyzer, cancellationToken, progressObserver);

        var byId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();
        var navById = new Dictionary<string, GraphNavigationContext>(StringComparer.Ordinal);
        var depthById = new Dictionary<string, int>(StringComparer.Ordinal);
        var parentOrderById = new Dictionary<string, int>(StringComparer.Ordinal);
        var resolutionCache = new Dictionary<(string ParentId, string ChildId), AssemblyResolution>();

        // The root's NetFxBindingContext is shared by every subsequent bind in the graph,
        // matching the CLR's app-domain-wide binding policy semantics.
        var netFxContext = NetFxBindingContext.TryBuild(analyzer);
        cancellationToken.ThrowIfCancellationRequested();

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

        try
        {
            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (current, currentId, ownsDispose) = queue.Dequeue();
                var currentDepth = depthById[currentId];

                try
                {
                    var refs = current.HasMetadata ? current.AssemblyRefs : [];
                    var typeRefs = current.HasMetadata ? current.TypeRefs : [];

                    var counts = CountTypeRefsByScopeId(typeRefs, cancellationToken);

                    foreach (var asmRef in refs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
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
                        var requestedDifferent = !string.Equals(
                            childId,
                            requestedId,
                            StringComparison.Ordinal);

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

                            ReportProgress(
                                progressObserver,
                                DependencyGraphBuildCheckpoint.ManagedAssemblyReferenceProcessed,
                                cancellationToken);
                            continue;
                        }

                        edges.Add(new GraphEdge(
                            currentId, childId, count,
                            RequestedIdentity: requestedDifferent ? asmRef : null));

                        AddNewNodeAndQueue(
                            current, asmRef, childId, childIdentity, resolution,
                            byId, navById, depthById, parentOrderById,
                            queue, ref parentCounter, currentDepth);
                        ReportProgress(
                            progressObserver,
                            DependencyGraphBuildCheckpoint.ManagedAssemblyReferenceProcessed,
                            cancellationToken);
                    }
                }
                finally
                {
                    if (ownsDispose)
                        current.Dispose();
                }
            }
        }
        finally
        {
            while (queue.TryDequeue(out var queued))
            {
                if (queued.OwnsDispose)
                    queued.Analyzer.Dispose();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var orderedNodes = byId.Values
            .OrderBy(n => n.Depth)
            .ThenBy(n => parentOrderById.TryGetValue(n.Id, out var o) ? o : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();

        return new DependencyGraphResult(orderedNodes, edges, navById);
    }

    /// <summary>
    /// Builds the graph of a Native AOT binary. ILC folds every managed assembly into the
    /// image, so nodes come from the mstat sidecar's assembly list (the app's own entry is
    /// the root) with edges aggregated from the DGML dependency graph: each link whose two
    /// endpoints attribute to different assemblies counts toward that assembly pair. The
    /// binary's native import modules join at depth 1. Without sidecars the graph is the
    /// root plus the import star — the only dependency facts the binary itself records.
    /// </summary>
    private static DependencyGraphResult BuildForNativeAot(
        AssemblyAnalyzer analyzer,
        CancellationToken cancellationToken,
        Action<DependencyGraphBuildCheckpoint>? progressObserver)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var navById = new Dictionary<string, GraphNavigationContext>(StringComparer.Ordinal);

        var rootIdentity = IdentityFromAnalyzer(analyzer);
        var mstat = analyzer.Mstat;
        cancellationToken.ThrowIfCancellationRequested();

        // The mstat lists the app's own assembly among its references; promote that identity
        // to the root so the graph is keyed on real assembly identity when available.
        var stem = Path.GetFileNameWithoutExtension(analyzer.FileName);
        AssemblyRefInfo? appIdentity = null;
        if (mstat is not null)
        {
            foreach (var assembly in mstat.Assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(assembly.Name, stem, StringComparison.OrdinalIgnoreCase))
                {
                    appIdentity = assembly;
                    break;
                }
            }
        }

        if (appIdentity is not null) rootIdentity = appIdentity;

        var rootId = AssemblyIdentityFormat.Format(
            rootIdentity.Name, rootIdentity.Version, rootIdentity.Culture, rootIdentity.PublicKeyToken);
        nodes.Add(new GraphNode(
            Id: rootId,
            Name: rootIdentity.Name,
            Version: NullIfEmpty(rootIdentity.Version),
            Culture: string.IsNullOrEmpty(rootIdentity.Culture) ? "neutral" : rootIdentity.Culture,
            PublicKeyToken: rootIdentity.PublicKeyToken,
            IsRoot: true,
            Depth: 0,
            Unresolved: false));
        navById[rootId] = AotNavigationContext(analyzer, AssemblyProvenance.Root, isFramework: false);

        if (mstat is not null)
        {
            var idByAssemblyName = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [rootIdentity.Name] = rootId,
            };

            foreach (var assembly in mstat.Assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReferenceEquals(assembly, appIdentity)) continue;
                var id = AssemblyIdentityFormat.Format(
                    assembly.Name, assembly.Version, assembly.Culture, assembly.PublicKeyToken);
                if (!idByAssemblyName.TryAdd(assembly.Name, id)) continue;

                nodes.Add(new GraphNode(
                    Id: id,
                    Name: assembly.Name,
                    Version: NullIfEmpty(assembly.Version),
                    Culture: string.IsNullOrEmpty(assembly.Culture) ? "neutral" : assembly.Culture,
                    PublicKeyToken: assembly.PublicKeyToken,
                    IsRoot: false,
                    Depth: 1,
                    Unresolved: false));
                navById[id] = AotNavigationContext(
                    analyzer, AssemblyProvenance.CompiledIntoNativeImage,
                    isFramework: AssemblyAnalyzer.IsFrameworkAssembly(
                        AssemblyProvenance.CompiledIntoNativeImage, assembly,
                        analyzer.TargetFramework, analyzer.PreferredRuntimePack));
            }

            AggregateDgmlEdges(
                analyzer,
                mstat,
                idByAssemblyName,
                rootId,
                nodes,
                edges,
                cancellationToken,
                progressObserver);
        }

        // Native import modules: the exe's own dependency facts, present with or without
        // sidecars, at depth 1 off the root. Edge weight = imported function count.
        var imports = analyzer.Imports;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var module in imports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = $"native:{module.ModuleName.ToLowerInvariant()}";
            if (navById.ContainsKey(id)) continue;

            nodes.Add(new GraphNode(
                Id: id,
                Name: module.ModuleName,
                Version: null,
                Culture: "neutral",
                PublicKeyToken: null,
                IsRoot: false,
                Depth: 1,
                Unresolved: false,
                Kind: GraphNodeKind.NativeImport));
            navById[id] = AotNavigationContext(
                analyzer, AssemblyProvenance.CompiledIntoNativeImage, isFramework: false);
            edges.Add(new GraphEdge(rootId, id, module.Functions.Count));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var ordered = nodes
            .OrderBy(n => n.Depth)
            .ThenBy(n => n.Kind)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();
        return new DependencyGraphResult(ordered, edges, navById);
    }

    /// <summary>
    /// Aggregates DGML links to assembly-pair edges: each link whose endpoints join (via the
    /// mstat node names) to entries of two different assemblies increments that pair's count.
    /// Depths then follow from a BFS over the aggregated edges so the layout bands read as
    /// dependency layers rather than a flat row.
    /// </summary>
    private static void AggregateDgmlEdges(
        AssemblyAnalyzer analyzer,
        MstatData mstat,
        Dictionary<string, string> idByAssemblyName,
        string rootId,
        List<GraphNode> nodes,
        List<GraphEdge> edges,
        CancellationToken cancellationToken,
        Action<DependencyGraphBuildCheckpoint>? progressObserver)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dgml = analyzer.Dgml;
        cancellationToken.ThrowIfCancellationRequested();
        if (dgml is null) return;

        // mstat node name -> owning assembly's node id.
        var assemblyByNodeName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var method in mstat.Methods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method.NodeName is { } n && idByAssemblyName.TryGetValue(method.AssemblyName, out var id))
                assemblyByNodeName.TryAdd(n, id);
        }

        foreach (var type in mstat.Types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (type.NodeName is { } n && idByAssemblyName.TryGetValue(type.AssemblyName, out var id))
                assemblyByNodeName.TryAdd(n, id);
        }

        var labelById = new Dictionary<int, string>(dgml.Nodes.Count);
        foreach (var node in dgml.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            labelById.TryAdd(node.Id, node.Label);
        }

        var counts = new Dictionary<(string Source, string Target), int>();
        foreach (var link in dgml.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (labelById.TryGetValue(link.SourceId, out var sourceLabel)
                && labelById.TryGetValue(link.TargetId, out var targetLabel)
                && assemblyByNodeName.TryGetValue(sourceLabel, out var sourceAssembly)
                && assemblyByNodeName.TryGetValue(targetLabel, out var targetAssembly)
                && sourceAssembly != targetAssembly)
            {
                counts.TryGetValue((sourceAssembly, targetAssembly), out var count);
                counts[(sourceAssembly, targetAssembly)] = count + 1;
            }

            ReportProgress(
                progressObserver,
                DependencyGraphBuildCheckpoint.DgmlLinkProcessed,
                cancellationToken);
        }

        foreach (var ((source, target), count) in counts.OrderBy(kvp => kvp.Key.Source, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            edges.Add(new GraphEdge(source, target, count));
        }

        // Re-derive depths from the aggregated topology, keeping every assembly reachable:
        // anything the BFS cannot reach from the root stays at depth 1.
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [rootId] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!adjacency.TryGetValue(edge.SourceId, out var targets))
            {
                targets = [];
                adjacency.Add(edge.SourceId, targets);
            }

            targets.Add(edge.TargetId);
        }

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var children)) continue;
            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (depths.ContainsKey(child)) continue;
                depths[child] = depths[current] + 1;
                queue.Enqueue(child);
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodes[i].Kind == GraphNodeKind.Assembly
                && depths.TryGetValue(nodes[i].Id, out var depth)
                && nodes[i].Depth != depth)
            {
                nodes[i] = nodes[i] with { Depth = depth };
            }
        }
    }

    private static void ReportProgress(
        Action<DependencyGraphBuildCheckpoint>? progressObserver,
        DependencyGraphBuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        progressObserver?.Invoke(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// The navigation context of a Native AOT graph node: nothing to open (the assembly was
    /// compiled into the image), so Enter degrades to an explanatory message.
    /// </summary>
    private static GraphNavigationContext AotNavigationContext(
        AssemblyAnalyzer analyzer, AssemblyProvenance provenance, bool isFramework) =>
        new(
            Resolved: null,
            ReferencingFilePath: analyzer.FilePath,
            ReferencingBundlePath: null,
            ReferencingTargetFramework: analyzer.TargetFramework,
            ReferencingPreferredRuntimePack: analyzer.PreferredRuntimePack,
            Provenance: provenance,
            IsFrameworkAssembly: isFramework,
            CandidateProbePath: null);

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
                ResolvedModule module => new AssemblyAnalyzer(
                    [.. module.Bytes],
                    filePath: module.Path,
                    sourceBundlePath: null,
                    displayName: Path.GetFileName(module.Path),
                    targetFrameworkOverride: module.TargetFramework,
                    preferredRuntimePackOverride: module.PreferredRuntimePack),
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

    private static Dictionary<string, int> CountTypeRefsByScopeId(
        IReadOnlyList<TypeRefInfo> typeRefs,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in typeRefs)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
