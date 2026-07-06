using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Computes IL code size per method and builds a hierarchical size tree
/// for treemap visualization. For a Native AOT binary with an mstat sidecar the tree is
/// built from the compiler's size report instead: native code and MethodTable bytes per
/// assembly, namespace, type, and method, plus the binary's data categories. Without an
/// mstat, the binary's merged native symbols carry the tree.
/// </summary>
public static class SizeAnalyzer
{
    /// <summary>
    /// Builds a hierarchical size tree from the assembly's methods grouped by type and namespace.
    /// </summary>
    /// <param name="analyzer">The assembly analyzer to read method metadata from.</param>
    /// <returns>The root <see cref="SizeNode"/> representing the entire assembly.</returns>
    public static SizeNode BuildSizeTree(AssemblyAnalyzer analyzer)
    {
        if (analyzer.BinaryKind == BinaryKind.NativeAot && analyzer.Mstat is { } mstat)
            return BuildAotSizeTree(analyzer, mstat);

        // No mstat: the merged native symbols carry the tree at symbol fidelity.
        if (analyzer.BinaryKind == BinaryKind.NativeAot
            && analyzer.NativeSymbols is { Symbols.Count: > 0 } symbols)
        {
            return BuildFromSymbols(analyzer.FileName, analyzer.RecoveredTypes, symbols);
        }

        // ReadyToRun: size the precompiled native code per method rather than IL bytes.
        if (analyzer.IsReadyToRun && analyzer.ReadyToRunIndex is { Methods.Count: > 0 })
            return BuildReadyToRunSizeTree(analyzer);

        // Get method sizes
        var methodSizes = new List<(MethodDefInfo Method, long Size)>();
        foreach (var method in analyzer.MethodDefs)
        {
            long size = 0;
            if (method.Rva != 0)
            {
                try
                {
                    var body = analyzer.GetMethodBody(method);
                    if (body is not null)
                        size = body.GetILBytes()?.Length ?? 0;
                }
                catch
                {
                    // Skip methods with unreadable bodies
                }
            }
            methodSizes.Add((method, size));
        }

        // Group by declaring type, then by namespace
        var byType = methodSizes
            .GroupBy(m => m.Method.DeclaringType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var typesByNamespace = analyzer.TypeDefs
            .GroupBy(td => string.IsNullOrEmpty(td.Namespace) ? "(global)" : td.Namespace)
            .OrderBy(g => g.Key);

        var namespaceNodes = new List<SizeNode>();
        foreach (var nsGroup in typesByNamespace)
        {
            var typeNodes = new List<SizeNode>();
            foreach (var typeDef in nsGroup)
            {
                if (!byType.TryGetValue(typeDef.FullName, out var methods))
                    continue;

                var methodNodes = methods
                    .Where(m => m.Size > 0)
                    .OrderByDescending(m => m.Size)
                    .Select(m => new SizeNode(
                        m.Method.Name,
                        $"{typeDef.FullName}::{m.Method.Name}@0x{m.Method.Token:X8}",
                        m.Size,
                        SizeNodeKind.Method,
                        []))
                    .ToList();

                var typeSize = methods.Sum(m => m.Size);
                if (typeSize > 0)
                {
                    typeNodes.Add(new SizeNode(
                        typeDef.Name,
                        typeDef.FullName,
                        typeSize,
                        SizeNodeKind.Type,
                        methodNodes));
                }
            }

            if (typeNodes.Count > 0)
            {
                namespaceNodes.Add(new SizeNode(
                    nsGroup.Key,
                    nsGroup.Key,
                    typeNodes.Sum(t => t.Size),
                    SizeNodeKind.Namespace,
                    [.. typeNodes.OrderByDescending(t => t.Size)]));
            }
        }

        return new SizeNode(
            analyzer.AssemblyName ?? analyzer.FileName,
            analyzer.FileName,
            namespaceNodes.Sum(n => n.Size),
            SizeNodeKind.Assembly,
            [.. namespaceNodes.OrderByDescending(n => n.Size)]);
    }

    /// <summary>
    /// Builds the size tree of a ReadyToRun image from its precompiled method sizes: namespace →
    /// type → method, each method sized by its total native code across all ranges. Distinct from
    /// the IL-byte tree so a reader sees the precompiled native footprint, not IL size.
    /// </summary>
    private static SizeNode BuildReadyToRunSizeTree(AssemblyAnalyzer analyzer)
    {
        // Build the tree from the method entries themselves — names and owning assembly come from the
        // entries (for a composite, resolved from each component's metadata). This is independent of
        // the analyzed file's own metadata, so a composite opened directly (which has none) still maps.
        var assemblyNodes = new List<SizeNode>();
        var byAssembly = analyzer.ReadyToRunMethods
            .Where(m => m.TotalSize > 0)
            .GroupBy(m => string.IsNullOrEmpty(m.AssemblyName) ? "(unknown)" : m.AssemblyName)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var asmGroup in byAssembly)
        {
            var namespaceNodes = new List<SizeNode>();
            foreach (var nsGroup in asmGroup.GroupBy(m => NamespaceOf(m.DeclaringType)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var typeNodes = nsGroup
                    .GroupBy(m => m.DeclaringType ?? "(unresolved)")
                    .Select(typeGroup =>
                    {
                        var methodNodes = typeGroup
                            .OrderByDescending(m => m.TotalSize)
                            .Select(m => new SizeNode(
                                $"{m.Name}{m.InstantiationDisplay}",
                                $"{asmGroup.Key}/{typeGroup.Key}::{m.Name}@0x{m.Token:X8}",
                                m.TotalSize, SizeNodeKind.Method, []))
                            .ToList();
                        return new SizeNode(
                            StripNamespace(typeGroup.Key, nsGroup.Key == "(global)" ? "" : nsGroup.Key),
                            $"{asmGroup.Key}/{typeGroup.Key}",
                            methodNodes.Sum(n => n.Size), SizeNodeKind.Type, [.. methodNodes]);
                    })
                    .OrderByDescending(t => t.Size)
                    .ToList();

                namespaceNodes.Add(new SizeNode(
                    nsGroup.Key, $"{asmGroup.Key}/{nsGroup.Key}",
                    typeNodes.Sum(t => t.Size), SizeNodeKind.Namespace, [.. typeNodes]));
            }

            assemblyNodes.Add(new SizeNode(
                asmGroup.Key, asmGroup.Key,
                namespaceNodes.Sum(n => n.Size), SizeNodeKind.Assembly,
                [.. namespaceNodes.OrderByDescending(n => n.Size)]));
        }

        return new SizeNode(
            $"{analyzer.AssemblyName ?? analyzer.FileName} (R2R native code)",
            analyzer.FileName,
            assemblyNodes.Sum(n => n.Size),
            SizeNodeKind.Assembly,
            [.. assemblyNodes.OrderByDescending(n => n.Size)]);
    }

    /// <summary>The namespace prefix of a declaring-type display name, or <c>(global)</c>.</summary>
    private static string NamespaceOf(string? declaringType)
    {
        if (string.IsNullOrEmpty(declaringType)) return "(global)";
        var lastDot = declaringType.LastIndexOf('.');
        return lastDot > 0 ? declaringType[..lastDot] : "(global)";
    }

    /// <summary>
    /// Builds the size tree of a Native AOT binary from its mstat report: one subtree per
    /// contributing assembly (namespace &gt; type &gt; method, with each type's MethodTable as
    /// an explicit leaf so sums stay exact) beside category nodes for the binary's data
    /// regions. Method sizes include code, GC info, and EH info.
    /// </summary>
    private static SizeNode BuildAotSizeTree(AssemblyAnalyzer analyzer, MstatData mstat)
    {
        var index = MstatSizeIndex.Create(mstat);
        var roots = new List<SizeNode>();
        roots.AddRange(BuildAssemblyNodes(index));

        // The 2.1+ detail sections re-report bytes that older readers found in these blob
        // buckets; showing both would double-count, so each bucket yields to its detail
        // section when that section has entries. The policy is shared with MstatDiffer and
        // budget evaluation so every consumer draws the same totals.
        var excluded = index.Policy.ExcludedBlobNames();

        var blobs = mstat.Blobs
            .Where(b => b.Size > 0 && !excluded.Contains(b.Name))
            .OrderByDescending(b => b.Size)
            .Select(b => new SizeNode(b.Name, $"Blobs/{b.Name}", b.Size, SizeNodeKind.Blob, []))
            .ToList();
        if (blobs.Count > 0)
            roots.Add(new SizeNode("Blobs", "Blobs", blobs.Sum(b => b.Size), SizeNodeKind.Category, blobs));

        var frozen = mstat.FrozenObjects
            .Where(f => f.Size > 0)
            .OrderByDescending(f => f.Size)
            .Select((f, i) => new SizeNode(
                f.TypeName, f.NodeName ?? $"Frozen Objects/{f.TypeName}#{i}", f.Size,
                SizeNodeKind.FrozenObject, [], f.NodeName))
            .ToList();
        if (frozen.Count > 0)
            roots.Add(new SizeNode("Frozen Objects", "Frozen Objects", frozen.Sum(f => f.Size), SizeNodeKind.Category, frozen));

        var rvaFields = mstat.RvaFields
            .Where(f => f.Size > 0)
            .OrderByDescending(f => f.Size)
            .Select(f => new SizeNode(
                f.Name, f.NodeName ?? $"RVA Fields/{f.Name}", f.Size, SizeNodeKind.RvaField, [], f.NodeName))
            .ToList();
        if (rvaFields.Count > 0)
            roots.Add(new SizeNode("RVA Fields", "RVA Fields", rvaFields.Sum(f => f.Size), SizeNodeKind.Category, rvaFields));

        var resources = mstat.ManifestResources
            .Where(r => r.Size > 0)
            .OrderByDescending(r => r.Size)
            .Select(r => new SizeNode(r.Name, $"Resources/{r.Name}", r.Size, SizeNodeKind.Resource, []))
            .ToList();
        if (resources.Count > 0)
            roots.Add(new SizeNode("Resources", "Resources", resources.Sum(r => r.Size), SizeNodeKind.Category, resources));

        return new SizeNode(
            analyzer.FileName,
            analyzer.FileName,
            roots.Sum(n => n.Size),
            SizeNodeKind.Assembly,
            [.. roots.OrderByDescending(n => n.Size)]);
    }

    /// <summary>
    /// Groups the index's method and MethodTable entries into assembly &gt; namespace &gt; type
    /// subtrees. A type node's children are its methods plus a MethodTable leaf carrying the
    /// type's runtime structure size and dependency-graph node name. The index has already
    /// folded display collisions and summed method totals, so the tree and the diff engine
    /// cannot disagree about a byte.
    /// </summary>
    private static List<SizeNode> BuildAssemblyNodes(MstatSizeIndex index)
    {
        var methodsByType = new Dictionary<(string Assembly, string Type), List<MstatSizeEntry>>();
        var tablesByType = new Dictionary<(string Assembly, string Type), MstatSizeEntry>();
        foreach (var entry in index.Entries)
        {
            var key = (entry.AssemblyName, entry.TypeName);
            switch (entry.Section)
            {
                case MstatSectionKind.Method:
                    (methodsByType.TryGetValue(key, out var list) ? list : methodsByType[key] = []).Add(entry);
                    break;
                case MstatSectionKind.MethodTable:
                    tablesByType[key] = entry;
                    break;
            }
        }

        // A type can appear with methods but no MethodTable, or the reverse; join over both.
        var typeKeys = methodsByType.Keys.Union(tablesByType.Keys);

        var assemblies = new Dictionary<string, Dictionary<string, List<SizeNode>>>(StringComparer.Ordinal);
        foreach (var (assemblyName, typeName) in typeKeys)
        {
            var children = new List<SizeNode>();

            var ns = "";
            if (tablesByType.TryGetValue((assemblyName, typeName), out var methodTable))
            {
                ns = methodTable.Namespace;
                if (methodTable.Size > 0)
                {
                    children.Add(new SizeNode(
                        "MethodTable", $"{assemblyName}/{typeName}::MethodTable",
                        methodTable.Size, SizeNodeKind.MethodTable, [],
                        methodTable.NodeNames.Count > 0 ? methodTable.NodeNames[0] : null));
                }
            }

            if (methodsByType.TryGetValue((assemblyName, typeName), out var methods))
            {
                ns = methods[0].Namespace;
                children.AddRange(methods
                    .Where(m => m.Size > 0)
                    .OrderByDescending(m => m.Size)
                    .Select(m => new SizeNode(
                        m.DisplayName, $"{assemblyName}/{typeName}::{m.DisplayName}",
                        m.Size, SizeNodeKind.Method, [],
                        m.NodeNames.Count > 0 ? m.NodeNames[0] : null)));
            }

            if (children.Count == 0) continue;

            var typeNode = new SizeNode(
                StripNamespace(typeName, ns), $"{assemblyName}/{typeName}",
                children.Sum(c => c.Size), SizeNodeKind.Type,
                [.. children.OrderByDescending(c => c.Size)]);

            var namespaces = assemblies.TryGetValue(assemblyName, out var existing)
                ? existing
                : assemblies[assemblyName] = new Dictionary<string, List<SizeNode>>(StringComparer.Ordinal);
            var nsKey = string.IsNullOrEmpty(ns) ? "(global)" : ns;
            (namespaces.TryGetValue(nsKey, out var list) ? list : namespaces[nsKey] = []).Add(typeNode);
        }

        var result = new List<SizeNode>(assemblies.Count);
        foreach (var (assemblyName, namespaces) in assemblies)
        {
            var namespaceNodes = namespaces
                .Select(kvp => new SizeNode(
                    kvp.Key, $"{assemblyName}/{kvp.Key}",
                    kvp.Value.Sum(t => t.Size), SizeNodeKind.Namespace,
                    [.. kvp.Value.OrderByDescending(t => t.Size)]))
                .OrderByDescending(n => n.Size)
                .ToList();

            result.Add(new SizeNode(
                assemblyName, assemblyName,
                namespaceNodes.Sum(n => n.Size), SizeNodeKind.Assembly, namespaceNodes));
        }

        return result;
    }

    /// <summary>Strips the namespace prefix from a namespace-qualified type display name.</summary>
    internal static string StripNamespace(string typeName, string ns) =>
        ns.Length > 0 && typeName.StartsWith(ns + ".", StringComparison.Ordinal)
            ? typeName[(ns.Length + 1)..]
            : typeName;

    /// <summary>
    /// Builds the size tree of a Native AOT binary from its merged native symbols: functions
    /// joined to managed names land under assembly &gt; namespace &gt; type, and the
    /// compiler-generated node kinds under explicit categories — unjoined names in
    /// <c>Runtime</c>, nameless boundaries in <c>Unattributed</c>. Each merged symbol is summed
    /// exactly once, so no byte is counted twice.
    /// </summary>
    /// <param name="fileName">The analyzed binary's file name, for the root node.</param>
    /// <param name="recoveredTypes">The binary's recovered metadata, for assembly attribution.</param>
    /// <param name="info">The merged native symbols.</param>
    internal static SizeNode BuildFromSymbols(
        string fileName, IReadOnlyList<RecoveredType> recoveredTypes, NativeSymbolInfo info)
    {
        // FullName -> assembly scope, for attributing a joined method to its assembly subtree
        // and for finding the type/method split point inside a joined name.
        var assemblyByType = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in recoveredTypes)
            assemblyByType.TryAdd(type.FullName, type.AssemblyName ?? "(unknown assembly)");

        // assembly -> namespace -> type FullName -> method leaves
        var assemblies = new Dictionary<string, Dictionary<string, Dictionary<string, List<SizeNode>>>>(StringComparer.Ordinal);
        var categories = new Dictionary<string, List<SizeNode>>(StringComparer.Ordinal);

        void Categorize(string category, NativeSymbol symbol, SizeNodeKind kind) =>
            (categories.TryGetValue(category, out var list) ? list : categories[category] = [])
                .Add(new SizeNode(
                    symbol.ManagedName ?? symbol.Name,
                    $"{category}/{symbol.Name}@0x{symbol.VirtualAddress:x}",
                    symbol.Size, kind, []));

        foreach (var symbol in info.Symbols)
        {
            if (symbol.Size <= 0) continue;
            switch (symbol.Kind)
            {
                case NativeSymbolKind.Function
                    when symbol.ManagedName is not null
                        && TrySplitManagedName(symbol.ManagedName, assemblyByType,
                            out var assembly, out var ns, out var type, out var method):
                {
                    var namespaces = assemblies.TryGetValue(assembly, out var a)
                        ? a
                        : assemblies[assembly] = new Dictionary<string, Dictionary<string, List<SizeNode>>>(StringComparer.Ordinal);
                    var types = namespaces.TryGetValue(ns, out var n)
                        ? n
                        : namespaces[ns] = new Dictionary<string, List<SizeNode>>(StringComparer.Ordinal);
                    (types.TryGetValue(type, out var methods) ? methods : types[type] = [])
                        .Add(new SizeNode(
                            method, $"{assembly}/{type}::{method}@0x{symbol.VirtualAddress:x}",
                            symbol.Size, SizeNodeKind.Function, []));
                    break;
                }

                case NativeSymbolKind.Function:
                    Categorize("Runtime", symbol, SizeNodeKind.Function);
                    break;
                case NativeSymbolKind.Boundary:
                    Categorize("Unattributed", symbol, SizeNodeKind.Function);
                    break;
                case NativeSymbolKind.MethodTable:
                    Categorize("MethodTables", symbol, SizeNodeKind.MethodTable);
                    break;
                case NativeSymbolKind.FrozenObject:
                    Categorize("Frozen Objects", symbol, SizeNodeKind.FrozenObject);
                    break;
                case NativeSymbolKind.Stub:
                    Categorize("Stubs", symbol, SizeNodeKind.Function);
                    break;
                case NativeSymbolKind.GenericDictionary:
                    Categorize("Generic Dictionaries", symbol, SizeNodeKind.Blob);
                    break;
                case NativeSymbolKind.Statics:
                    Categorize("Statics", symbol, SizeNodeKind.Blob);
                    break;
                default:
                    Categorize("Data", symbol, SizeNodeKind.Blob);
                    break;
            }
        }

        var roots = new List<SizeNode>();
        foreach (var (assemblyName, namespaces) in assemblies)
        {
            var namespaceNodes = namespaces
                .Select(kvp => new SizeNode(
                    kvp.Key, $"{assemblyName}/{kvp.Key}",
                    kvp.Value.Values.SelectMany(m => m).Sum(x => x.Size), SizeNodeKind.Namespace,
                    [.. kvp.Value
                        .Select(t => new SizeNode(
                            StripNamespace(t.Key, kvp.Key == "(global)" ? "" : kvp.Key),
                            $"{assemblyName}/{t.Key}",
                            t.Value.Sum(m => m.Size), SizeNodeKind.Type,
                            [.. t.Value.OrderByDescending(m => m.Size)]))
                        .OrderByDescending(t => t.Size)]))
                .OrderByDescending(n => n.Size)
                .ToList();

            roots.Add(new SizeNode(
                assemblyName, assemblyName,
                namespaceNodes.Sum(n => n.Size), SizeNodeKind.Assembly, namespaceNodes));
        }

        foreach (var (category, entries) in categories)
        {
            roots.Add(new SizeNode(
                category, category, entries.Sum(e => e.Size), SizeNodeKind.Category,
                [.. entries.OrderByDescending(e => e.Size)]));
        }

        return new SizeNode(
            fileName, fileName,
            roots.Sum(r => r.Size), SizeNodeKind.Assembly,
            [.. roots.OrderByDescending(r => r.Size)]);
    }

    /// <summary>
    /// Splits a joined name (<c>{type.FullName}.{method}</c>) at the recovered type boundary.
    /// Method names may contain dots (<c>.ctor</c>), so split points are probed right to left
    /// against the recovered type names rather than guessed.
    /// </summary>
    private static bool TrySplitManagedName(
        string managedName, Dictionary<string, string> assemblyByType,
        out string assembly, out string ns, out string type, out string method)
    {
        for (var i = managedName.LastIndexOf('.'); i > 0; i = managedName.LastIndexOf('.', i - 1))
        {
            var candidate = managedName[..i];
            if (!assemblyByType.TryGetValue(candidate, out var owner)) continue;

            assembly = owner;
            type = candidate;
            method = managedName[(i + 1)..];

            // The namespace is the outermost scope's dotted prefix; nested types ('+') keep
            // their full display under the type node.
            var plus = candidate.IndexOf('+');
            var scope = plus >= 0 ? candidate[..plus] : candidate;
            var dot = scope.LastIndexOf('.');
            ns = dot > 0 ? scope[..dot] : "(global)";
            return method.Length > 0;
        }

        assembly = ns = type = method = "";
        return false;
    }
}
