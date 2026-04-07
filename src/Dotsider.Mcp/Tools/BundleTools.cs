using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for inspecting .NET single-file bundles.
/// </summary>
[McpServerToolType]
public sealed partial class BundleTools
{
    /// <summary>
    /// Checks if a file is a .NET single-file bundle and returns its manifest metadata.
    /// </summary>
    /// <param name="assemblyPath">Path to the file to inspect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with bundle version, entry count, and total size, or an error if not a bundle.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public static partial Task<string> GetBundleInfo(
        string assemblyPath,
        CancellationToken ct = default)
    {
        ToolHelpers.ValidateAssemblyPath(assemblyPath);

        if (!SingleFileBundleReader.IsBundle(assemblyPath, out var headerOffset))
            return Task.FromResult(JsonSerializer.Serialize(
                new { IsBundle = false }, DotsiderJsonOptions.Default));

        var manifest = SingleFileBundleReader.ReadManifest(assemblyPath, headerOffset);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            IsBundle = true,
            manifest.MajorVersion,
            manifest.MinorVersion,
            manifest.FileCount,
            manifest.BundleId,
            TotalSize = manifest.Entries.Sum(e => e.Size)
        }, DotsiderJsonOptions.Default));
    }

    /// <summary>
    /// Lists all entries in a .NET single-file bundle with names, sizes, and types.
    /// </summary>
    /// <param name="assemblyPath">Path to the bundle file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of bundle entries.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public static partial Task<string> ListBundleEntries(
        string assemblyPath,
        CancellationToken ct = default)
    {
        ToolHelpers.ValidateAssemblyPath(assemblyPath);

        if (!SingleFileBundleReader.IsBundle(assemblyPath, out var headerOffset))
            return Task.FromResult("Error: File is not a single-file bundle.");

        var manifest = SingleFileBundleReader.ReadManifest(assemblyPath, headerOffset);
        var entries = manifest.Entries.Select(e => new
        {
            e.RelativePath,
            Type = e.Type.ToString(),
            e.Size,
            e.CompressedSize
        });
        return Task.FromResult(JsonSerializer.Serialize(entries, DotsiderJsonOptions.Default));
    }
}
