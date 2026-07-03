using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Computes IL code size per method and builds a hierarchical size tree
/// for treemap visualization. For a Native AOT binary with an mstat sidecar the tree is
/// built from the compiler's size report instead: native code and MethodTable bytes per
/// assembly, namespace, type, and method, plus the binary's data categories.
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
    /// Builds the size tree of a Native AOT binary from its mstat report: one subtree per
    /// contributing assembly (namespace &gt; type &gt; method, with each type's MethodTable as
    /// an explicit leaf so sums stay exact) beside category nodes for the binary's data
    /// regions. Method sizes include code, GC info, and EH info.
    /// </summary>
    private static SizeNode BuildAotSizeTree(AssemblyAnalyzer analyzer, MstatData mstat)
    {
        var roots = new List<SizeNode>();
        roots.AddRange(BuildAssemblyNodes(mstat));

        // The 2.1+ detail sections re-report bytes that older readers found in these blob
        // buckets; showing both would double-count, so each bucket yields to its detail
        // section when that section has entries.
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (mstat.FrozenObjects.Count > 0) excluded.Add("ArrayOfFrozenObjects");
        if (mstat.RvaFields.Count > 0) excluded.Add("FieldRvaData");
        if (mstat.ManifestResources.Count > 0) excluded.Add("ResourceData");

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
    /// Groups mstat methods and types into assembly &gt; namespace &gt; type subtrees. A type
    /// node's children are its methods plus a MethodTable leaf carrying the type's runtime
    /// structure size and dependency-graph node name.
    /// </summary>
    private static List<SizeNode> BuildAssemblyNodes(MstatData mstat)
    {
        var methodsByType = mstat.Methods
            .GroupBy(m => (m.AssemblyName, m.DeclaringType))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Two constructed types can render to the same display name; fold them into one
        // MethodTable entry, keeping the first node name for the why-chain join.
        var typesByKey = new Dictionary<(string Assembly, string Type), (long Size, string? NodeName, string Namespace)>();
        foreach (var t in mstat.Types)
        {
            var key = (t.AssemblyName, t.Name);
            typesByKey[key] = typesByKey.TryGetValue(key, out var existing)
                ? (existing.Size + t.Size, existing.NodeName, existing.Namespace)
                : (t.Size, t.NodeName, t.Namespace);
        }

        // A type can appear with methods but no MethodTable, or the reverse; join over both.
        var typeKeys = methodsByType.Keys.Union(typesByKey.Keys);

        var assemblies = new Dictionary<string, Dictionary<string, List<SizeNode>>>(StringComparer.Ordinal);
        foreach (var (assemblyName, typeName) in typeKeys)
        {
            var children = new List<SizeNode>();

            var ns = "";
            if (typesByKey.TryGetValue((assemblyName, typeName), out var methodTable))
            {
                ns = methodTable.Namespace;
                if (methodTable.Size > 0)
                {
                    children.Add(new SizeNode(
                        "MethodTable", $"{assemblyName}/{typeName}::MethodTable",
                        methodTable.Size, SizeNodeKind.MethodTable, [], methodTable.NodeName));
                }
            }

            if (methodsByType.TryGetValue((assemblyName, typeName), out var methods))
            {
                ns = methods[0].Namespace;
                children.AddRange(methods
                    .Select(m => (Method: m, Total: (long)m.Size + m.GcInfoSize + m.EhInfoSize))
                    .Where(m => m.Total > 0)
                    .OrderByDescending(m => m.Total)
                    .Select(m => new SizeNode(
                        m.Method.Name, $"{assemblyName}/{typeName}::{m.Method.Name}",
                        m.Total, SizeNodeKind.Method, [], m.Method.NodeName)));
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
    private static string StripNamespace(string typeName, string ns) =>
        ns.Length > 0 && typeName.StartsWith(ns + ".", StringComparison.Ordinal)
            ? typeName[(ns.Length + 1)..]
            : typeName;
}
