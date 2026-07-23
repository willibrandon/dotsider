using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

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

        BundleManifest manifest;
        try
        {
            manifest = SingleFileBundleReader.ReadManifest(assemblyPath, headerOffset);
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new { IsBundle = false, Error = "Invalid single-file bundle manifest." },
                DotsiderJsonOptions.Default));
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            IsBundle = true,
            manifest.MajorVersion,
            manifest.MinorVersion,
            manifest.FileCount,
            manifest.BundleId,
            TotalSize = CalculateTotalSize(manifest.Entries)
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

        BundleManifest manifest;
        try
        {
            manifest = SingleFileBundleReader.ReadManifest(assemblyPath, headerOffset);
        }
        catch (InvalidDataException)
        {
            return Task.FromResult("Error: Invalid single-file bundle manifest.");
        }

        var entries = manifest.Entries.Select(e => new
        {
            e.RelativePath,
            Type = e.Type.ToString(),
            e.Size,
            e.CompressedSize
        });
        return Task.FromResult(JsonSerializer.Serialize(entries, DotsiderJsonOptions.Default));
    }

    private static long CalculateTotalSize(IEnumerable<BundleEntry> entries)
    {
        var totalSize = 0L;
        foreach (var entry in entries)
        {
            if (entry.Size > long.MaxValue - totalSize)
                return long.MaxValue;

            totalSize += entry.Size;
        }

        return totalSize;
    }
}
