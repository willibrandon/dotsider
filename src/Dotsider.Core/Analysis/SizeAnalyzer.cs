using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Computes IL code size per method and builds a hierarchical size tree
/// for treemap visualization.
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
}
