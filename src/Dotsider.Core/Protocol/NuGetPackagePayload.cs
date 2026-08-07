using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// NuGet package identity and managed payload files.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NuGetPackagePayload(
    string? PackageId,
    string? PackageVersion,
    string? Authors,
    string? Description,
    IReadOnlyList<NuGetFileEntry> DllFiles);
