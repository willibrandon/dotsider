using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for analyzing NuGet packages (.nupkg files).
/// </summary>
[McpServerToolType]
public sealed partial class NuGetTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Analyzes a NuGet package and returns its metadata, DLL list, and contents.
    /// </summary>
    /// <param name="nupkgPath">Path to the .nupkg file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with package identity, authors, description, and DLL file listing.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> AnalyzeNupkg(
        string nupkgPath,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "analyze-nupkg", AssemblyPath = nupkgPath }, ct);
        }

        // Direct mode
        ToolHelpers.ValidateFilePath(nupkgPath, "nupkgPath");
        using var package = new NuGetPackageAnalyzer(nupkgPath);
        return McpJson.Serialize(new NuGetPackagePayload(
            package.PackageId,
            package.PackageVersion,
            package.Authors,
            package.Description,
            package.DllFiles));
    }
}
